using System;
using UnityEngine;

// Records head, paddle and ball motion every frame into a MotionClip, in
// table coordinates, with an event for each ball contact on the player's
// paddle.  Used to record a coach on the Quest.
public class MotionRecorder : MonoBehaviour {

    public Play play;
    public Transform table;
    public Transform head;

    MotionClip clip;
    float start_time;
    public string discarded;      // Why the last recording was not saved, or null.

    public bool recording {
        get { return clip != null; }
    }

    void OnEnable() {
        if (play != null)
            play.player_paddle_hit += paddle_hit;
    }

    void OnDisable() {
        if (play != null)
            play.player_paddle_hit -= paddle_hit;
    }

    public void start_recording() {
        clip = new MotionClip();
        clip.name = "clip_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        clip.source = "quest";
        clip.left_handed = play.paddle_hand.wand.left;
        clip.recorded = DateTime.Now.ToString("o");
        start_time = Time.time;
    }

    // Stops recording and saves the clip.  Returns the clip, or null if
    // nothing was recorded.
    public MotionClip stop_recording() {
        MotionClip c = clip;
        clip = null;
        discarded = null;
        if (c == null || c.frames.Count == 0) {
            discarded = "Inget inspelat";
            return null;
        }
        if (!c.usable_as_coach) {
            discarded = "Spela minst " + Mathf.RoundToInt(MotionClip.min_coach_seconds)
                        + " s med några bollträffar";
            return null;
        }
        c.save();
        return c;
    }

    // Stop without saving.
    public void cancel_recording() {
        clip = null;
    }

    // Record after Play.Update() has moved the paddle and ball.
    void LateUpdate() {
        if (clip == null)
            return;
        MotionFrame f = new MotionFrame();
        f.t = Time.time - start_time;
        if (head != null) {
            f.head_position = TableSpace.to_table(table, head.position);
            f.head_rotation = TableSpace.to_table(table, head.rotation);
        }
        Transform paddle = play.paddle_hand.held_paddle.transform;
        f.paddle_position = TableSpace.to_table(table, paddle.position);
        f.paddle_rotation = TableSpace.to_table(table, paddle.rotation);
        Ball ball = play.ball_in_play;
        f.ball_in_play = (ball != null && ball.gameObject.activeSelf && !play.ball_held(ball));
        if (ball != null)
            f.ball_position = TableSpace.to_table(table, ball.transform.position);
        Transform robot_paddle = play.robot.paddle.transform;
        f.robot_paddle_position = TableSpace.to_table(table, robot_paddle.position);
        f.robot_paddle_rotation = TableSpace.to_table(table, robot_paddle.rotation);
        clip.frames.Add(f);
    }

    void paddle_hit(Ball b) {
        if (clip == null)
            return;
        MotionEvent e = new MotionEvent();
        e.t = Time.time - start_time;
        e.type = "contact";
        clip.events.Add(e);
    }
}
