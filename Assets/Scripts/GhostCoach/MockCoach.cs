using UnityEngine;

// A made-up coach clip used until a real recording exists: a right handed
// player doing forehand drives against the robot, one every 1.6 seconds.
// The paddle goes back and down, then forward and up through the ball,
// follows through toward the left shoulder and recovers.  The ball flies
// robot -> bounce -> contact -> bounce -> robot.  It is plausible rather
// than a real stroke, so the ghost and scoring can be tried without data.
public static class MockCoach {

    const float period = 1.6f;          // Seconds between strokes.
    const float first_contact = 1.0f;
    const float frame_step = 1f / 90f;
    const float gravity = 9.8f;

    // Key paddle poses relative to contact, in table coordinates with the
    // player at the -z end facing +z.  Times in seconds from contact.
    static readonly float[] key_times = { -0.7f, -0.12f, 0f, 0.15f, 0.9f };
    static readonly Vector3[] key_offsets = {
        new Vector3(0.10f, -0.05f, -0.25f),    // Ready.
        new Vector3(0.35f, -0.20f, -0.35f),    // End of backswing.
        Vector3.zero,                          // Contact.
        new Vector3(-0.35f, 0.40f, 0.25f),     // Follow-through.
        new Vector3(0.10f, -0.05f, -0.25f),    // Ready again.
    };
    // Forehand rubber normals: facing right on the backswing, forward and
    // slightly closed at contact, turning left on the follow-through.
    static readonly Vector3[] key_normals = {
        new Vector3(0.2f, -0.2f, 0.95f),
        new Vector3(0.5f, -0.3f, 0.8f),
        new Vector3(0f, -0.34f, 0.94f),
        new Vector3(-0.6f, -0.1f, 0.8f),
        new Vector3(0.2f, -0.2f, 0.95f),
    };

    // Build the clip for the scene's table.
    public static MotionClip forehand_drive(Table table, float ball_radius, int strokes = 5) {
        float top_y;
        Vector3 center = TableSpace.center(table, out top_y);
        return forehand_drive(center, top_y, 0.5f * table.length, ball_radius, strokes);
    }

    // center: middle of the table on the floor, top_y: height of the table
    // surface, half_length: net to end line, all in table coordinates.
    public static MotionClip forehand_drive(Vector3 center, float top_y, float half_length,
                                            float ball_radius, int strokes = 5) {
        MotionClip clip = new MotionClip();
        clip.name = "mock_forehand_drive";
        clip.source = "mock";
        clip.left_handed = false;
        clip.recorded = System.DateTime.Now.ToString("o");

        float end = center.z - half_length;                       // Player's end line.
        Vector3 contact = new Vector3(center.x + 0.3f, top_y + 0.2f, end - 0.15f);
        Vector3 stance = new Vector3(center.x - 0.15f, 1.55f, end - 0.45f);
        Vector3 robot_hit = new Vector3(center.x + 0.1f, top_y + 0.2f, center.z + half_length + 0.2f);
        Vector3 player_bounce = new Vector3(center.x + 0.25f, top_y + ball_radius, center.z - 0.55f * half_length);
        Vector3 robot_bounce = new Vector3(center.x - 0.1f, top_y + ball_radius, center.z + 0.6f * half_length);

        float duration = first_contact + (strokes - 1) * period + 0.9f;
        for (float t = 0f ; t <= duration ; t += frame_step) {
            // Time relative to the nearest stroke's contact.
            int k = Mathf.Clamp(Mathf.RoundToInt((t - first_contact) / period), 0, strokes - 1);
            float dt = t - (first_contact + k * period);

            MotionFrame f = new MotionFrame();
            f.t = t;
            f.paddle_position = contact + spline(dt);
            f.paddle_rotation = paddle_rotation(dt);
            f.ball_in_play = true;
            f.ball_position = ball(dt, robot_hit, player_bounce, contact, robot_bounce);
            f.robot_paddle_position = robot_paddle(dt, robot_hit);
            f.robot_paddle_rotation = Quaternion.LookRotation(Vector3.forward, new Vector3(0.7f, 0.7f, 0f));

            // Head sways a little with the stroke and watches the ball.
            f.head_position = stance + new Vector3(0.1f * (f.paddle_position.x - contact.x),
                                                   -0.04f * Mathf.Exp(-dt * dt / 0.02f), 0f);
            Vector3 look = f.ball_position - f.head_position;
            look.y = Mathf.Min(look.y, 0f);                     // Keep the gaze level or down.
            f.head_rotation = Quaternion.LookRotation(Vector3.Lerp(Vector3.forward, look.normalized, 0.6f),
                                                      Vector3.up);
            clip.frames.Add(f);
        }
        for (int k = 0 ; k < strokes ; ++k) {
            MotionEvent e = new MotionEvent();
            e.t = first_contact + k * period;
            e.type = "contact";
            clip.events.Add(e);
        }
        return clip;
    }

    // Catmull-Rom style curve through the key offsets with non-uniform key times.
    static Vector3 spline(float t) {
        int n = key_times.Length;
        if (t <= key_times[0]) return key_offsets[0];
        if (t >= key_times[n-1]) return key_offsets[n-1];
        int i = 0;
        while (key_times[i+1] < t)
            i += 1;
        float t0 = key_times[i], t1 = key_times[i+1];
        float h = t1 - t0, u = (t - t0) / h;
        Vector3 p0 = key_offsets[i], p1 = key_offsets[i+1];
        Vector3 m0 = tangent(i) * h, m1 = tangent(i+1) * h;
        float u2 = u * u, u3 = u2 * u;
        return (2*u3 - 3*u2 + 1) * p0 + (u3 - 2*u2 + u) * m0 + (-2*u3 + 3*u2) * p1 + (u3 - u2) * m1;
    }

    // Velocity at a key: zero at the ends (paddle at rest when ready).
    static Vector3 tangent(int i) {
        int n = key_times.Length;
        if (i == 0 || i == n - 1)
            return Vector3.zero;
        return (key_offsets[i+1] - key_offsets[i-1]) / (key_times[i+1] - key_times[i-1]);
    }

    static Quaternion paddle_rotation(float t) {
        int n = key_times.Length;
        int i = 0;
        while (i < n - 2 && key_times[i+1] < t)
            i += 1;
        float u = Mathf.Clamp01((t - key_times[i]) / (key_times[i+1] - key_times[i]));
        u = u * u * (3f - 2f * u);
        Quaternion a = rotation_for_normal(key_normals[i]), b = rotation_for_normal(key_normals[i+1]);
        return Quaternion.Slerp(a, b, u);
    }

    // Paddle z axis points to the backhand side, so it is minus the forehand
    // normal; the blade tip (y axis) points up and to the left.
    static Quaternion rotation_for_normal(Vector3 forehand_normal) {
        return Quaternion.LookRotation(-forehand_normal.normalized, new Vector3(-0.7f, 0.7f, 0f));
    }

    // Ball flight in parabolic pieces: robot hit (-0.8 s), bounce on the
    // player's side (-0.3), contact (0), bounce on the robot's side (+0.35)
    // and back to the robot (+0.8, the next stroke's -0.8).
    static Vector3 ball(float t, Vector3 robot_hit, Vector3 player_bounce, Vector3 contact, Vector3 robot_bounce) {
        if (t < -0.8f) t += period;           // Before the first stroke: previous flight.
        if (t < -0.3f) return arc(robot_hit, player_bounce, t + 0.8f, 0.5f);
        if (t < 0f) return arc(player_bounce, contact, t + 0.3f, 0.3f);
        if (t < 0.35f) return arc(contact, robot_bounce, t, 0.35f);
        return arc(robot_bounce, robot_hit, t - 0.35f, 0.45f);
    }

    static Vector3 arc(Vector3 from, Vector3 to, float t, float duration) {
        float f = Mathf.Clamp01(t / duration);
        Vector3 p = Vector3.Lerp(from, to, f);
        float vy = (to.y - from.y + 0.5f * gravity * duration * duration) / duration;
        p.y = from.y + vy * t - 0.5f * gravity * t * t;
        return p;
    }

    // Robot paddle rests a little behind its hitting point and lunges to
    // the ball at its hit time (0.8 s after the player's contact).
    static Vector3 robot_paddle(float t, Vector3 robot_hit) {
        float since = t - 0.8f;
        if (since < -period / 2f) since += period;
        float pulse = Mathf.Exp(-since * since / 0.02f);
        return robot_hit + new Vector3(0f, -0.05f, 0.03f + 0.3f * (1f - pulse));
    }
}
