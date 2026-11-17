using UnityEngine;

// Result of comparing one player stroke with the coach's stroke.
public class StrokeScore {
    public int score;              // 0-100, higher is closer to the coach.
    public float shape_error;      // RMS paddle path difference in meters, contact points aligned.
    public float face_angle;       // Degrees between paddle face normals at contact.
    public float speed_ratio;      // Player paddle speed / coach paddle speed at contact.
    public float contact_offset;   // Meters between player and coach contact points.
    public string tip;             // One short correction, or praise when the stroke is good.
}

// Compares a player's paddle motion around a ball contact with the coach's
// motion around a contact.  Both are MotionClips in table coordinates.
// The paths are compared over the same time window relative to contact,
// with the contact points aligned, so the score reflects the swing itself
// rather than where the ball happened to arrive.
public static class StrokeCompare {

    public const float before_contact = 0.35f;   // Seconds of swing compared before contact.
    public const float after_contact = 0.20f;    // Seconds of follow-through compared.
    const float sample_step = 0.01f;

    // Errors that give zero points for each part of the score.
    const float max_shape_error = 0.12f;          // Meters.
    const float max_face_angle = 40f;             // Degrees.
    const float max_speed_difference = 0.6f;      // Fraction of the coach's speed.
    public const int good_score = 80;

    public static StrokeScore compare(MotionClip coach, float coach_contact,
                                      MotionClip player, float player_contact) {
        StrokeScore s = new StrokeScore();

        // Path shape with contact points aligned.
        Vector3 c0 = coach.sample(coach_contact).paddle_position;
        Vector3 p0 = player.sample(player_contact).paddle_position;
        s.contact_offset = (p0 - c0).magnitude;
        float sum = 0f;
        int n = 0;
        for (float dt = -before_contact; dt <= after_contact + 1e-4f; dt += sample_step) {
            Vector3 c = coach.sample(coach_contact + dt).paddle_position - c0;
            Vector3 p = player.sample(player_contact + dt).paddle_position - p0;
            sum += (p - c).sqrMagnitude;
            n += 1;
        }
        s.shape_error = Mathf.Sqrt(sum / n);

        // Paddle face at contact.
        Vector3 cn = face_normal(coach.sample(coach_contact));
        Vector3 pn = face_normal(player.sample(player_contact));
        s.face_angle = Vector3.Angle(cn, pn);

        // Swing speed and direction at contact.
        Vector3 cv = velocity(coach, coach_contact);
        Vector3 pv = velocity(player, player_contact);
        s.speed_ratio = (cv.magnitude > 0.01f ? pv.magnitude / cv.magnitude : 1f);

        float shape_part = Mathf.Clamp01(1f - s.shape_error / max_shape_error);
        float angle_part = Mathf.Clamp01(1f - s.face_angle / max_face_angle);
        float speed_part = Mathf.Clamp01(1f - Mathf.Abs(s.speed_ratio - 1f) / max_speed_difference);
        s.score = Mathf.RoundToInt(100f * (0.5f * shape_part + 0.3f * angle_part + 0.2f * speed_part));

        s.tip = choose_tip(s.score, 0.5f * (1f - shape_part), 0.3f * (1f - angle_part),
                           0.2f * (1f - speed_part), cn, pn, cv, pv, s.speed_ratio);
        return s;
    }

    // Forehand side normal of the paddle blade (see Paddle.forehand_normal).
    static Vector3 face_normal(MotionFrame f) {
        return f.paddle_rotation * new Vector3(0f, 0f, -1f);
    }

    static Vector3 velocity(MotionClip clip, float t) {
        float h = 0.02f;
        return (clip.sample(t + h).paddle_position - clip.sample(t - h).paddle_position) / (2f * h);
    }

    // One short tip for the part of the score that lost the most points,
    // so the player only has one thing to think about.
    static string choose_tip(int score, float shape_loss, float angle_loss, float speed_loss,
                             Vector3 coach_normal, Vector3 player_normal,
                             Vector3 coach_velocity, Vector3 player_velocity, float speed_ratio) {
        if (score >= good_score)
            return "Bra slag!";
        if (angle_loss >= shape_loss && angle_loss >= speed_loss)
            // A face pointing more upward than the coach's is too open.
            return (player_normal.y > coach_normal.y ? "Stäng racketen mer" : "Öppna racketen mer");
        // A clearly different tempo also makes the path differ, and is the
        // easier thing to correct, so it goes before the path shape.
        if (speed_loss >= shape_loss || Mathf.Abs(speed_ratio - 1f) > 0.3f)
            return (speed_ratio < 1f ? "Sving snabbare" : "Sving lugnare");
        // Shape: compare how steeply the paddle rises through contact.
        float coach_rise = coach_velocity.normalized.y, player_rise = player_velocity.normalized.y;
        if (player_rise < coach_rise - 0.15f)
            return "Sving mer uppåt";
        if (player_rise > coach_rise + 0.15f)
            return "Sving mer framåt";
        return "Följ coachens bana";
    }
}
