using System.Collections.Generic;
using UnityEngine;

// Phase 2: the player plays in the hitting position while the coach's
// paddle path for one reference stroke is shown as a line, with a ghost
// paddle at the coach's contact pose.  After each ball contact the
// player's own paddle path is drawn next to it and the stroke is scored
// against the coach's (StrokeCompare).  GhostCoach listens to
// stroke_scored to give the feedback.
public class PathGuide : MonoBehaviour {

    public Play play;
    public Transform table;
    public float path_lead = 0.6f;          // Seconds of backswing drawn before contact.
    public float path_follow = 0.35f;       // Seconds of follow-through drawn after contact.
    public float history_seconds = 2f;      // Player motion kept for scoring.
    public int stroke = 0;                  // Which stroke of the clip is the reference.

    public System.Action<StrokeScore> stroke_scored;
    public StrokeScore last_score;

    MotionClip coach;
    float coach_contact;
    MotionClip player_recent = new MotionClip();
    List<float> pending_contacts = new List<float>();

    Transform root, contact_paddle, stand_marker;
    LineRenderer coach_line, player_line;

    public bool showing {
        get { return root != null && root.gameObject.activeSelf; }
    }

    // Hitting position where the coach stood, on the floor, world coordinates.
    public Vector3 player_spot {
        get { return stand_marker.position; }
    }

    public int coach_path_points {
        get { return (coach_line != null ? coach_line.positionCount : 0); }
    }

    void OnEnable() {
        if (play != null)
            play.player_paddle_hit += paddle_hit;
    }

    void OnDisable() {
        if (play != null)
            play.player_paddle_hit -= paddle_hit;
    }

    public void show(MotionClip c) {
        if (c == null || !choose_reference_stroke(c)) {
            hide();
            return;
        }
        if (root == null)
            build();
        coach = c;
        draw_coach_path();
        player_line.positionCount = 0;
        pending_contacts.Clear();
        root.gameObject.SetActive(true);
    }

    public void hide() {
        if (root != null)
            root.gameObject.SetActive(false);
        pending_contacts.Clear();
    }

    // Use the chosen stroke if it has a full swing around it, else the first
    // ball contact that has, or the middle of the clip if it has no contacts.
    bool choose_reference_stroke(MotionClip c) {
        float lo = StrokeCompare.before_contact, hi = c.duration - StrokeCompare.after_contact;
        if (hi <= lo)
            return false;
        List<MotionEvent> strokes = c.strokes();
        if (strokes.Count > 0) {
            float t = strokes[stroke % strokes.Count].t;
            if (t >= lo && t <= hi) {
                coach_contact = t;
                return true;
            }
        }
        foreach (float t in c.contact_times())
            if (t >= lo && t <= hi) {
                coach_contact = t;
                return true;
            }
        coach_contact = 0.5f * (lo + hi);
        return true;
    }

    void draw_coach_path() {
        set_path(coach_line, coach, coach_contact);
        MotionFrame f = coach.sample(coach_contact);
        contact_paddle.SetPositionAndRotation(TableSpace.to_world(table, f.paddle_position),
                                              TableSpace.to_world(table, f.paddle_rotation));
        // Player stands where the coach stood.
        Vector3 forward, right;
        Vector3 stance = GhostVisuals.coach_stance(coach, coach_contact - path_lead,
                                                   coach_contact + path_follow, out forward, out right);
        GhostVisuals.place_on_floor(stand_marker, table, stance);
    }

    void set_path(LineRenderer line, MotionClip clip, float contact) {
        float start = Mathf.Max(0f, contact - path_lead);
        float end = Mathf.Min(clip.duration, contact + path_follow);
        int n = Mathf.Max(2, Mathf.CeilToInt((end - start) / 0.01f) + 1);
        line.positionCount = n;
        for (int i = 0 ; i < n ; ++i) {
            float t = start + (end - start) * i / (n - 1);
            line.SetPosition(i, TableSpace.to_world(table, clip.sample(t).paddle_position));
        }
    }

    // Keep the player's recent paddle motion, after Play.Update() moved it.
    void LateUpdate() {
        if (!showing)
            return;
        MotionFrame f = new MotionFrame();
        f.t = Time.time;
        Transform paddle = play.paddle_hand.held_paddle.transform;
        f.paddle_position = TableSpace.to_table(table, paddle.position);
        f.paddle_rotation = TableSpace.to_table(table, paddle.rotation);
        player_recent.frames.Add(f);
        int old = 0;
        while (old < player_recent.frames.Count && player_recent.frames[old].t < Time.time - history_seconds)
            old += 1;
        if (old > 0)
            player_recent.frames.RemoveRange(0, old);
    }

    void paddle_hit(Ball b) {
        if (showing)
            pending_contacts.Add(Time.time);
    }

    // Score a stroke once its follow-through has been recorded.
    void Update() {
        if (!showing || pending_contacts.Count == 0)
            return;
        float t = pending_contacts[0];
        if (Time.time < t + path_follow + 0.02f)
            return;
        pending_contacts.RemoveAt(0);
        last_score = StrokeCompare.compare(coach, coach_contact, player_recent, t);
        set_path(player_line, player_recent, t);
        if (stroke_scored != null)
            stroke_scored(last_score);
    }

    void build() {
        root = new GameObject("PathGuide visuals").transform;
        coach_line = GhostVisuals.line("coach paddle path", GhostVisuals.material("ghost"), 0.03f, root);
        player_line = GhostVisuals.line("player paddle path", GhostVisuals.material("player_path"), 0.015f, root);
        contact_paddle = GhostVisuals.copy_meshes(play.paddle_hand.held_paddle.transform,
                                                  "coach contact paddle", GhostVisuals.material("ghost"), root);
        stand_marker = GhostVisuals.stand_marker("hitting position marker", root);
    }
}
