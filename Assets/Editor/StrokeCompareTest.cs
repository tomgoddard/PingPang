using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Checks the phase 2 stroke scoring with synthetic strokes whose correct
// result is known.  Run from the command line with
//   Unity -batchmode -quit -projectPath . -executeMethod StrokeCompareTest.run
// Exit code 0 means all checks passed.
public static class StrokeCompareTest {

    static List<string> failures = new List<string>();

    [MenuItem("GhostCoach/Run stroke scoring test")]
    public static void run() {
        failures.Clear();
        MotionClip coach = stroke(Vector3.zero, 0f, 1f);
        float contact = 0.5f;

        StrokeScore same = StrokeCompare.compare(coach, contact, coach, contact);
        check(same.score == 100 && same.tip == "Bra slag!", "identical stroke scores 100", same);

        // Same swing hit 30 cm to the side: only the contact offset changes.
        StrokeScore moved = StrokeCompare.compare(coach, contact, stroke(new Vector3(0.3f, 0f, 0f), 0f, 1f), contact);
        check(moved.score >= 99 && Mathf.Abs(moved.contact_offset - 0.3f) < 0.01f,
              "shifted stroke keeps score, offset 0.3 m", moved);

        // Paddle face 30 degrees more open (tilted upward).
        StrokeScore open = StrokeCompare.compare(coach, contact, stroke(Vector3.zero, 30f, 1f), contact);
        check(Mathf.Abs(open.face_angle - 30f) < 1f && open.score < StrokeCompare.good_score
              && open.tip == "Stäng racketen mer", "open face lowers score and says close", open);

        // Face 30 degrees more closed.
        StrokeScore closed = StrokeCompare.compare(coach, contact, stroke(Vector3.zero, -30f, 1f), contact);
        check(closed.tip == "Öppna racketen mer", "closed face says open", closed);

        // Half speed swing.
        StrokeScore slow = StrokeCompare.compare(coach, contact, stroke(Vector3.zero, 0f, 0.5f), contact);
        check(Mathf.Abs(slow.speed_ratio - 0.5f) < 0.05f && slow.score < 100,
              "half speed swing gives speed ratio 0.5", slow);
        check(slow.tip == "Sving snabbare", "half speed swing says swing faster", slow);

        StrokeScore fast = StrokeCompare.compare(coach, contact, stroke(Vector3.zero, 0f, 1.6f), contact);
        check(fast.tip == "Sving lugnare", "much faster swing says swing calmer", fast);

        // Scores stay in range for a very different stroke.
        StrokeScore wild = StrokeCompare.compare(coach, contact, stroke(new Vector3(0f, 0.5f, 0f), 70f, 3f), contact);
        check(wild.score >= 0 && wild.score < 40, "very different stroke scores low", wild);

        check_mock_coach();

        bool ok = (failures.Count == 0);
        Debug.Log("StrokeCompareTest " + (ok ? "PASSED" : "FAILED") + ", " + failures.Count + " failed checks");
        if (Application.isBatchMode)
            EditorApplication.Exit(ok ? 0 : 1);
    }

    // The made-up coach used before real recordings exist should be a
    // plausible stroke: repeated identically, ball meets the paddle at
    // contact, and a drive-like paddle speed.
    static void check_mock_coach() {
        MotionClip mock = MockCoach.forehand_drive(Vector3.zero, 0.76f, 1.37f, 0.02f);
        List<float> contacts = mock.contact_times();
        check_value(contacts.Count == 5, "mock coach has 5 strokes", contacts.Count);
        StrokeScore same = StrokeCompare.compare(mock, contacts[0], mock, contacts[2]);
        check(same.score == 100, "mock strokes are repeated identically", same);
        MotionFrame f = mock.sample(contacts[1]);
        float ball_gap = (f.ball_position - f.paddle_position).magnitude;
        check_value(ball_gap < 0.05f, "mock ball meets paddle at contact, gap m", ball_gap);
        float h = 0.01f;
        float speed = (mock.sample(contacts[1] + h).paddle_position
                       - mock.sample(contacts[1] - h).paddle_position).magnitude / (2f * h);
        check_value(speed > 3f && speed < 10f, "mock paddle speed at contact m/s", speed);
        float lowest = 10f;
        foreach (MotionFrame m in mock.frames)
            if (Mathf.Abs(m.ball_position.z) < 1.37f)
                lowest = Mathf.Min(lowest, m.ball_position.y);
        check_value(lowest > 0.76f, "mock ball stays above the table surface, lowest m", lowest);
        check_value(f.head_position.y > 1.3f && f.head_position.z < -1.37f,
                    "mock coach head behind the end line, z", f.head_position.z);

        check_ball_synth();

        // Left handed version: mirrored about the table center line.
        MotionClip left = mock.mirrored(0f);
        MotionFrame lf = left.sample(contacts[1]);
        check_value(left.left_handed && Mathf.Abs(lf.paddle_position.x + f.paddle_position.x) < 1e-4f,
                    "mirrored coach hits on the other side, x", lf.paddle_position.x);
        StrokeScore mirrored_same = StrokeCompare.compare(left, contacts[0], left, contacts[2]);
        check(mirrored_same.score == 100, "mirrored strokes still compare identically", mirrored_same);
        Vector3 n = f.paddle_rotation * Vector3.back, ln = lf.paddle_rotation * Vector3.back;
        check_value(Mathf.Abs(n.x + ln.x) < 1e-4f && Mathf.Abs(n.y - ln.y) < 1e-4f && Mathf.Abs(n.z - ln.z) < 1e-4f,
                    "mirrored paddle face is the mirror image, normal x", ln.x);
        MotionFrame back = left.mirrored(0f).sample(contacts[1]);
        check_value((back.paddle_position - f.paddle_position).magnitude < 1e-4f
                    && Quaternion.Angle(back.paddle_rotation, f.paddle_rotation) < 0.01f,
                    "mirroring twice gives the original, error m", (back.paddle_position - f.paddle_position).magnitude);
    }

    // A clip without a ball, as from mocap, gets a ball that meets the
    // racket at every stroke, forehand and backhand.
    static void check_ball_synth() {
        float top_y = 0.76f, half = 1.37f, radius = 0.02f, half_thickness = 0.008f;
        MotionClip clip = MockCoach.forehand_drive(Vector3.zero, top_y, half, radius);
        clip.events[2].type = "backhand";
        foreach (MotionFrame f in clip.frames) {
            f.ball_in_play = false;
            f.ball_position = Vector3.zero;
        }
        clip.has_ball = false;
        BallSynth.add(clip, Vector3.zero, top_y, half, radius, half_thickness);
        check_value(clip.has_ball, "clip without a ball gets one", clip.has_ball ? 1f : 0f);

        float worst_gap = 0f, worst_side = 1f, below = 0f, fastest = 0f;
        int missing = 0;
        foreach (MotionEvent e in clip.events) {
            MotionFrame f = clip.sample(e.t);
            if (!f.ball_in_play) { missing += 1; continue; }
            float gap = (f.ball_position - f.paddle_position).magnitude;
            worst_gap = Mathf.Max(worst_gap, Mathf.Abs(gap - (radius + half_thickness)));
            worst_side = Mathf.Min(worst_side, f.ball_position.z - f.paddle_position.z);
        }
        check_value(missing == 0, "ball is in play at every stroke, strokes without", missing);
        check_value(worst_gap < 0.002f, "ball touches the racket face at every stroke, error m", worst_gap);
        check_value(worst_side > 0f, "ball is on the opponent's side of the racket, smallest z gap m", worst_side);

        MotionFrame previous = null;
        foreach (MotionFrame f in clip.frames) {
            if (f.ball_in_play && Mathf.Abs(f.ball_position.x) < 0.7625f && Mathf.Abs(f.ball_position.z) < half)
                below = Mathf.Min(below, f.ball_position.y - (top_y + radius) + 0.01f);
            if (previous != null && previous.ball_in_play && f.ball_in_play)
                fastest = Mathf.Max(fastest, (f.ball_position - previous.ball_position).magnitude / (f.t - previous.t));
            previous = f;
        }
        check_value(below >= 0f, "ball never goes through the table, deepest m", below);
        check_value(fastest < 14f, "ball never teleports, fastest m/s", fastest);

        int bounces = 0;
        float lowest = 10f;
        foreach (MotionFrame f in clip.frames)
            if (f.ball_in_play && Mathf.Abs(f.ball_position.x) < 0.7625f && Mathf.Abs(f.ball_position.z) < half)
                lowest = Mathf.Min(lowest, f.ball_position.y - (top_y + radius));
        check_value(lowest < 0.05f, "ball comes down to the table surface, closest m above it", lowest);
        for (int i = 1 ; i < clip.frames.Count - 1 ; ++i) {
            MotionFrame a = clip.frames[i-1], b = clip.frames[i], c = clip.frames[i+1];
            if (a.ball_in_play && b.ball_in_play && c.ball_in_play
                && b.ball_position.y <= top_y + radius + 0.06f      // A bounce falls between frames.
                && b.ball_position.y < a.ball_position.y && b.ball_position.y <= c.ball_position.y)
                bounces += 1;
        }
        // Two bounces per interval: one on each half, for 5 strokes.
        check_value(bounces >= 8 && bounces <= 10, "ball bounces on the table twice per stroke, bounces", bounces);

        MotionClip again = clip.mirrored(0f);
        MotionFrame m = again.sample(clip.events[0].t), o = clip.sample(clip.events[0].t);
        check_value(again.has_ball && Mathf.Abs(m.ball_position.x + o.ball_position.x) < 1e-4f,
                    "mirrored clip keeps the ball, mirrored x", m.ball_position.x);
    }

    static void check_value(bool ok, string what, float value) {
        Debug.Log("StrokeCompareTest " + (ok ? "PASS " : "FAIL ") + what + " " + value.ToString("F3"));
        if (!ok)
            failures.Add(what);
    }

    static void check(bool ok, string what, StrokeScore s) {
        Debug.Log("StrokeCompareTest " + (ok ? "PASS " : "FAIL ") + what
                  + " (score " + s.score + ", shape " + s.shape_error.ToString("F3")
                  + " m, angle " + s.face_angle.ToString("F1") + ", speed " + s.speed_ratio.ToString("F2")
                  + ", offset " + s.contact_offset.ToString("F2") + " m, tip \"" + s.tip + "\")");
        if (!ok)
            failures.Add(what);
    }

    // Forehand drive-like swing: the paddle moves forward (+z) and up
    // through contact at t = 0.5 s, at the given speed factor, with the
    // face tilted open (positive) or closed (negative) by face_tilt degrees.
    static MotionClip stroke(Vector3 offset, float face_tilt, float speed) {
        MotionClip c = new MotionClip();
        for (int i = 0 ; i <= 100 ; ++i) {
            float t = i * 0.01f;
            float s = speed * (t - 0.5f);                 // Swing progress, 0 at contact.
            MotionFrame f = new MotionFrame();
            f.t = t;
            f.paddle_position = offset + new Vector3(0.2f * s, 0.9f + 0.6f * s, -1.4f + 2.5f * s);
            // Blade upright, forehand rubber facing +z, tilted about x.
            Quaternion upright = Quaternion.LookRotation(Vector3.back, Vector3.up);
            f.paddle_rotation = Quaternion.AngleAxis(-face_tilt, Vector3.right) * upright;
            f.head_rotation = Quaternion.identity;
            c.frames.Add(f);
        }
        return c;
    }
}
