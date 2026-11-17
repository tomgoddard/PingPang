using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Training flow decided by the team:
//   Phase 1 Coach: the ghost coach plays at the table in the hitting
//     position while the player stands beside it, facing the same way,
//     and watches.
//   Phase 2 Path: the player plays in the hitting position with the
//     coach's paddle path shown as support, and gets a score and one tip
//     per stroke (PathGuide, StrokeCompare).
// The ball is always in play.  The player switches phase with a button
// (right controller A, P on the keyboard).  The right controller B button
// (K on the keyboard) starts and stops recording a coach clip.  Clicking
// the right thumbstick (L on the keyboard) changes the coach's playback
// speed.  Left X (G on the keyboard) hides or shows the racket and ball in
// the hand.  Backspace (desktop only) resets phase, speed and view.  Holding the left thumbstick in (N on the keyboard) starts a
// session for the next test participant (Experiment).
//
// Added at run time to the object holding the PlayerInput so it receives
// the On<Action> messages from PlayControls.inputactions.
public class GhostCoach : MonoBehaviour {

    public enum Phase { Coach, Path }

    public Play play;
    public MotionRecorder recorder;
    public CoachGhost coach_ghost;
    public PathGuide path_guide;
    public HandVisuals hand_visuals;
    public BallFeeder ball_feeder;
    public Table table;
    public Phase phase = Phase.Coach;
    public MotionClip recorded_clip;     // As recorded (or the mock coach).
    public MotionClip coach_clip;        // As shown: mirrored if the player uses the other hand.
    public Experiment experiment;
    public bool allow_recording = false;
    public int stroke = 0;               // The stroke of the clip repeated and followed.
    bool player_left_handed;
    bool bindings_set = false;
    bool variant_applied = false;
    Dictionary<MeshRenderer, bool> robot_visible = new Dictionary<MeshRenderer, bool>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void create() {
        Buttons buttons = FindFirstObjectByType<Buttons>();
        Play play = FindFirstObjectByType<Play>();
        Table table = FindFirstObjectByType<Table>();
        if (buttons == null || play == null || table == null)
            return;

        GameObject g = buttons.gameObject;
        MotionRecorder recorder = g.AddComponent<MotionRecorder>();
        recorder.enabled = false;         // Hook up references before OnEnable.
        recorder.play = play;
        recorder.table = table.transform;
        Camera head = (play.passthrough_camera != null ? play.passthrough_camera : Camera.main);
        recorder.head = (head != null ? head.transform : null);
        recorder.enabled = true;

        CoachGhost ghost = g.AddComponent<CoachGhost>();
        ghost.play = play;
        ghost.table = table.transform;

        PathGuide guide = g.AddComponent<PathGuide>();
        guide.enabled = false;            // Hook up references before OnEnable.
        guide.play = play;
        guide.table = table.transform;
        guide.enabled = true;

        HandVisuals hands = g.AddComponent<HandVisuals>();
        hands.play = play;

        BallFeeder feeder = g.AddComponent<BallFeeder>();
        feeder.play = play;
        feeder.table = table;

        GhostCoach coach = g.AddComponent<GhostCoach>();
        coach.hand_visuals = hands;
        coach.ball_feeder = feeder;
        coach.play = play;
        coach.recorder = recorder;
        coach.coach_ghost = ghost;
        coach.path_guide = guide;
        coach.table = table;
        guide.stroke_scored += coach.stroke_scored;

        TableSpawn spawn = g.AddComponent<TableSpawn>();
        spawn.play = play;

        ControllerInput controllers = g.AddComponent<ControllerInput>();
        controllers.play = play;
        controllers.buttons = buttons;
        controllers.coach = coach;
    }

    void Start() {
        // A clip shipped with the app (mocap), else a made-up forehand drive
        // so both phases can be tried.  Only if recording is turned on in
        // experiment.json is a coach recorded on this headset used first.
        ExperimentConfig config = ExperimentConfig.load_or_create();
        allow_recording = config.allow_recording;
        GhostVisuals.show_stand_marker = config.show_stand_marker;
        GhostVisuals.round_racket = config.round_racket;
        GhostVisuals.make_racket_round(play.paddle_hand.held_paddle.transform);
        GhostVisuals.make_racket_round(play.robot.paddle.transform);
        hand_visuals.apply_hidden(ExperimentState.load().hide_hand_visuals);
        string record_hint = (allow_recording ? "   B: spela in egen coach" : "   B: byta slag");
        MotionClip c = (allow_recording ? MotionClip.load_latest() : null);
        if (c == null) {
            c = MotionClip.load_bundled();
            if (c != null)
                show_message("Coach: " + c.name + "\nA: byt fas" + record_hint);
        }
        if (c == null) {
            float ball_radius = (play.ball_in_play != null ? play.ball_in_play.radius : 0.02f);
            c = MockCoach.forehand_drive(table, ball_radius);
            show_message("Demo-coach (påhittad forehand)\nA: byt fas" + record_hint);
        }
        if (!c.has_ball) {
            // Mocap has no ball: add one that meets the coach's racket.
            float radius = (play.ball_in_play != null ? play.ball_in_play.radius : 0.02f);
            BallSynth.add(c, table, radius, 0.5f * play.paddle_hand.held_paddle.thickness);
        }
        Debug.Log("GhostCoach coach clip " + c.name + " (" + c.source + "), "
                  + c.contact_times().Count + " strokes");
        set_coach(c);
        experiment = new Experiment(this);
        experiment.start_session();
    }

    void OnDestroy() {
        if (experiment != null)
            experiment.end_session();
    }

    // Use the settings of the experiment's current variant.
    void apply_variant() {
        variant_applied = true;
        Variant v = experiment.variant;
        coach_ghost.stand_on_paddle_side = v.coach_on_paddle_side;
        coach_ghost.stand_behind = v.coach_behind;
        if (DesktopRig.active == null)        // The editor has no passthrough.
            play.enable_show_room(v.mixed_reality);
        update_ghost();
    }

    public void new_session() {
        experiment.start_session();
        apply_variant();
        show_message("Ny deltagare " + experiment.participant
                     + (experiment.finished ? "" : "\nBlock " + experiment.block_label));
        play.paddle_hand.wand.haptic_pulses(3, 0.08f, 0.8f, 0.08f);
    }

    // Use a clip as the coach, mirrored when the coach played with the
    // other hand than the player, so the player never has to mirror the
    // movement in their head.
    void set_coach(MotionClip c) {
        recorded_clip = c;
        if (player_left_handed != play.paddle_hand.wand.left || !bindings_set)
            mirror_controller_buttons(play.paddle_hand.wand.left);
        player_left_handed = play.paddle_hand.wand.left;
        if (c != null && c.left_handed != player_left_handed) {
            float top_y;
            coach_clip = c.mirrored(TableSpace.center(table, out top_y).x);
        } else
            coach_clip = c;
        update_ghost();
    }

    // The button bindings in PlayControls.inputactions assume a right
    // handed player: ball and serve on the left (free) hand.  For a left
    // handed player swap every left and right hand binding so the same
    // jobs stay on the same hand, except the menu button, which only the
    // left controller has.
    void mirror_controller_buttons(bool left_handed) {
        bindings_set = true;
        PlayerInput input = GetComponent<PlayerInput>();
        if (input == null || input.actions == null)
            return;
        foreach (InputAction action in input.actions) {
            for (int i = 0 ; i < action.bindings.Count ; ++i) {
                string path = action.bindings[i].path;
                bool handed = (path.Contains("{LeftHand}") || path.Contains("{RightHand}"));
                if (!handed || path.EndsWith("/start"))
                    continue;
                if (left_handed)
                    action.ApplyBindingOverride(i, path.Contains("{LeftHand}")
                                                ? path.Replace("{LeftHand}", "{RightHand}")
                                                : path.Replace("{RightHand}", "{LeftHand}"));
                else
                    action.RemoveBindingOverride(i);
            }
        }
    }

    // The player can switch hand in the settings menu at any time.
    void Update() {
        // After all Start() calls, so the settings menu does not undo it.
        if (!variant_applied && experiment != null)
            apply_variant();
        if (play.paddle_hand.wand.left != player_left_handed) {
            set_coach(recorded_clip);
            show_message(player_left_handed ? "Vänsterhänt: coachen spegelvänd" : "Högerhänt");
            experiment.log_event("hand", player_left_handed ? "left" : "right");
        }
    }

    public void set_phase(Phase p) {
        phase = p;
        update_ghost();
        if (coach_clip == null)
            show_message("Ingen coach inspelad än\nTryck B för att spela in");
        else
            show_message(phase == Phase.Coach
                         ? "Titta på coachen\nA: spela mot roboten   B: byta slag"
                         : "Spela mot roboten\nA: titta på coachen   B: byta slag");
        // Two short pulses, different from the single pulse of a ball hit.
        play.paddle_hand.wand.haptic_pulses(2, 0.04f, 0.6f, 0.08f);
        experiment.log_event("phase", phase == Phase.Coach ? "coach" : "path");
    }

    // Repeat the next stroke of the clip: the coach shows it in phase 1 and
    // its paddle path is the reference in phase 2.
    public void next_stroke() {
        if (coach_clip == null)
            return;
        List<MotionEvent> strokes = coach_clip.strokes();
        if (strokes.Count < 2) {
            show_message("Bara ett slag finns");
            return;
        }
        stroke = (stroke + 1) % strokes.Count;
        coach_ghost.stroke = stroke;
        path_guide.stroke = stroke;
        update_ghost();
        string kind = (strokes[stroke].type == "backhand" ? "backhand" : "forehand");
        show_message("Slag " + (stroke + 1) + " av " + strokes.Count + ": " + kind);
        play.paddle_hand.wand.haptic_pulses(1, 0.04f, 0.5f, 0f);
        experiment.log_event("stroke_select", (stroke + 1) + " " + kind);
    }

    public void toggle_recording() {
        if (!allow_recording) {
            show_message("Inspelning av coach är avstängd\n(allow_recording i experiment.json)");
            return;
        }
        if (!recorder.recording) {
            recorder.start_recording();
            update_ghost();           // Hide the replay and the path, and do not score.
            show_message("Spelar in coach...\nTryck B igen för att sluta");
            play.paddle_hand.wand.haptic_pulses(1, 0.1f, 0.8f, 0f);
            experiment.log_event("recording_start", "");
        } else {
            MotionClip c = recorder.stop_recording();
            experiment.log_event("recording_stop", c != null ? c.name : "not saved: " + recorder.discarded);
            if (c != null) {
                set_coach(c);
                show_message("Sparade " + c.name + "\n" + c.contact_times().Count + " slag, "
                             + c.duration.ToString("F0") + " s");
            } else
                show_message("Ingen coach sparad\n" + recorder.discarded);
            play.paddle_hand.wand.haptic_pulses(3, 0.04f, 0.8f, 0.06f);
            if (c == null)
                update_ghost();
        }
    }

    // The coach stands beside the player in both phases and repeats the
    // chosen stroke.  In phase 1 (watch) the coach hits its own ball and
    // the robot rests; in phase 2 (play) the robot plays, the coach's ball
    // is gone and the path is shown.  Nothing shows while recording.
    void update_ghost() {
        bool live = (coach_clip != null && !recorder.recording);
        coach_ghost.show_ball = (phase == Phase.Coach);
        if (live)
            coach_ghost.show(coach_clip);
        else
            coach_ghost.hide();
        set_robot(phase == Phase.Path);
        if (live && phase == Phase.Path)
            path_guide.show(coach_clip);
        else
            path_guide.hide();

        // Without a headset, move the view to where the player should stand.
        // In phase 1 turn halfway toward the coach, who is beside the player
        // and would be outside a flat screen's field of view.
        if (DesktopRig.active != null && live) {
            Vector3 forward = table.transform.forward;
            // The coach is beside the player and would be outside a flat
            // screen's field of view: turn halfway toward the coach.
            Vector3 to_coach = Vector3.ProjectOnPlane(coach_ghost.coach_position - coach_ghost.player_spot, Vector3.up);
            forward = (forward + to_coach.normalized).normalized;
            DesktopRig.active.stand_at(coach_ghost.player_spot, forward);
        }
    }

    // The robot only plays in phase 2: no serve or return, and its paddle
    // is hidden, in phase 1.
    void set_robot(bool active) {
        play.robot_active = active;
        foreach (MeshRenderer r in play.robot.paddle.GetComponentsInChildren<MeshRenderer>(true)) {
            if (active) {
                bool was;
                if (robot_visible.TryGetValue(r, out was)) {
                    r.enabled = was;
                    robot_visible.Remove(r);
                }
            } else if (!robot_visible.ContainsKey(r)) {
                robot_visible[r] = r.enabled;
                r.enabled = false;
            }
        }
    }

    // Phase 2 feedback after each stroke, as the current variant says: the
    // score and one tip as short text, and/or a vibration in the free hand
    // so it does not mix with the ball hit pulse in the paddle hand.  One
    // long pulse means a good stroke, two short pulses mean look at the tip.
    public void stroke_scored(StrokeScore s) {
        Variant v = experiment.variant;
        show_message(v.feedback_text ? "Score " + s.score + "\n" + s.tip : "");
        if (v.feedback_haptic) {
            if (s.score >= StrokeCompare.good_score)
                play.free_hand.wand.haptic_pulses(1, 0.15f, 0.6f, 0f);
            else
                play.free_hand.wand.haptic_pulses(2, 0.05f, 0.8f, 0.08f);
        }
        string block_message = experiment.stroke(s, v.feedback_text, v.feedback_haptic);
        if (block_message != null) {
            apply_variant();
            show_message(block_message);
        }
    }

    // Back to the start: phase 1, normal coach speed, view at the stand
    // marker.  For when buttons have been pressed at random.
    public void reset() {
        recorder.cancel_recording();
        coach_ghost.speed = 0.5f;
        stroke = coach_ghost.stroke = path_guide.stroke = 0;
        phase = Phase.Coach;
        update_ghost();
        show_message("Återställd\nTitta på coachen");
        experiment.log_event("reset", "");
    }

    // Hide or show the racket and the ball in the hand.  Remembered.
    public void toggle_hand_visuals() {
        bool hide = !hand_visuals.hidden;
        hand_visuals.apply_hidden(hide);
        ExperimentState state = ExperimentState.load();
        state.hide_hand_visuals = hide;
        state.save();
        show_message(hide ? "Racket och boll i handen dolda" : "Racket och boll i handen visas");
        experiment.log_event("hand_visuals", hide ? "hidden" : "shown");
    }

    public void change_speed() {
        float s = coach_ghost.next_speed();
        show_message("Coachens hastighet " + Mathf.RoundToInt(100f * s) + " %");
        experiment.log_event("coach_speed", Mathf.RoundToInt(100f * s) + "%");
    }

    void show_message(string text) {
        Debug.Log("GhostCoach: " + text.Replace("\n", " "));
        if (play.billboard != null)
            play.billboard.text = text;
    }

    // Input System messages (PlayControls.inputactions, PlayActions map).
    public void OnGhostTogglePhase() {
        if (!ButtonDedup.accept("GhostTogglePhase"))
            return;
        set_phase(phase == Phase.Coach ? Phase.Path : Phase.Coach);
    }

    // Right B: change the stroke, or record a coach if that is turned on.
    public void OnGhostRecord() {
        if (!ButtonDedup.accept("GhostRecord"))
            return;
        if (allow_recording)
            toggle_recording();
        else
            next_stroke();
    }

    public void OnGhostSpeed() {
        if (!ButtonDedup.accept("GhostSpeed"))
            return;
        change_speed();
    }

    public void OnGhostToggleHands() {
        if (!ButtonDedup.accept("GhostToggleHands"))
            return;
        toggle_hand_visuals();
    }

    public void OnGhostReset() {
        reset();
    }

    public void OnGhostNewSession() {
        if (!ButtonDedup.accept("GhostNewSession"))
            return;
        new_session();
    }
}
