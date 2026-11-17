using System.Collections.Generic;
using UnityEngine;

// Adds a made-up ball to a coach clip that has none, such as a Qualisys
// take (mocap only follows the body and the racket).  For every stroke in
// the clip the ball comes from the opponent's side, bounces once on the
// coach's half, meets the racket exactly at the stroke, bounces once on
// the opponent's half and flies on to the opponent.  The ball hits the
// racket face that points toward the opponent, so forehand and backhand
// strokes both work.
//
// Between two strokes the ball takes the same time as the coach's stroke
// interval, split as: racket -> far bounce 27%, far bounce -> opponent 20%,
// opponent -> near bounce 30%, near bounce -> racket 23%.  The flight
// itself is plausible rather than measured: the ball is not recorded.
public static class BallSynth {

    const float gravity = 9.8f;
    const float default_interval = 1.4f;      // For the first stroke, seconds.
    const float far_bounce = 0.27f, opponent_hit = 0.47f, near_bounce = 0.77f;   // Fractions of the interval.

    class Flight {
        public float t0, t1;
        public Vector3 from, to;
    }

    public static void add(MotionClip clip, Table table, float ball_radius, float paddle_half_thickness) {
        float top_y;
        Vector3 center = TableSpace.center(table, out top_y);
        add(clip, center, top_y, 0.5f * table.length, ball_radius, paddle_half_thickness);
    }

    // center: middle of the table on the floor, top_y: table surface height,
    // half_length: net to end line; table coordinates.
    public static void add(MotionClip clip, Vector3 center, float top_y, float half_length,
                           float ball_radius, float paddle_half_thickness) {
        if (clip.has_ball)
            return;
        List<float> times = new List<float>();
        foreach (MotionEvent e in clip.events)
            if (e.type == "contact" || e.type == "backhand")
                times.Add(e.t);
        times.Sort();
        if (times.Count == 0 || clip.frames.Count == 0)
            return;

        // Where the ball is at each stroke: on the racket face toward the
        // opponent.
        Vector3[] contact = new Vector3[times.Count];
        for (int i = 0 ; i < times.Count ; ++i) {
            MotionFrame f = clip.sample(times[i]);
            Vector3 normal = f.paddle_rotation * new Vector3(0f, 0f, -1f);
            if (normal.z < 0f)
                normal = -normal;
            contact[i] = f.paddle_position + normal * (ball_radius + paddle_half_thickness);
        }

        float surface = top_y + ball_radius;
        List<Flight> flights = new List<Flight>();
        for (int i = 0 ; i < times.Count ; ++i) {
            float interval = (i > 0 ? times[i] - times[i-1] : default_interval);
            float start = times[i] - interval;                   // Previous stroke (virtual for the first).
            // Where the previous stroke sent the ball.
            Vector3 previous = (i > 0 ? contact[i-1] : new Vector3(center.x + 0.2f * (contact[i].x - center.x), top_y, center.z - half_length));
            float far_x = Mathf.Clamp(center.x - 0.5f * (previous.x - center.x), center.x - 0.6f, center.x + 0.6f);
            Vector3 far = new Vector3(far_x, surface, center.z + 0.6f * half_length);
            float opponent_x = 0.8f * far_x + 0.2f * center.x;
            Vector3 opponent = new Vector3(opponent_x, top_y + 0.25f, center.z + half_length + 0.25f);
            Vector3 near = new Vector3(Mathf.Lerp(opponent_x, contact[i].x, 0.55f), surface, center.z - 0.5f * half_length);

            if (i > 0) {
                flights.Add(flight(start, 0f, far_bounce, contact[i-1], far, interval));
                flights.Add(flight(start, far_bounce, opponent_hit, far, opponent, interval));
            }
            flights.Add(flight(start, opponent_hit, near_bounce, opponent, near, interval));
            flights.Add(flight(start, near_bounce, 1f, near, contact[i], interval));
        }
        // After the last stroke: to the far bounce and on to the opponent.
        {
            int last = times.Count - 1;
            float interval = (last > 0 ? times[last] - times[last-1] : default_interval);
            float far_x = Mathf.Clamp(center.x - 0.5f * (contact[last].x - center.x), center.x - 0.6f, center.x + 0.6f);
            Vector3 far = new Vector3(far_x, surface, center.z + 0.6f * half_length);
            Vector3 opponent = new Vector3(0.8f * far_x + 0.2f * center.x, top_y + 0.25f, center.z + half_length + 0.25f);
            flights.Add(flight(times[last], 0f, far_bounce, contact[last], far, interval));
            flights.Add(flight(times[last], far_bounce, opponent_hit, far, opponent, interval));
        }

        foreach (MotionFrame f in clip.frames) {
            f.ball_in_play = false;
            foreach (Flight fl in flights)
                if (f.t >= fl.t0 && f.t <= fl.t1) {
                    f.ball_in_play = true;
                    f.ball_position = position(fl, f.t);
                    break;
                }
        }
        clip.has_ball = true;
    }

    // Flight from time start + a * interval to start + b * interval.
    static Flight flight(float start, float a, float b, Vector3 from, Vector3 to, float interval) {
        Flight fl = new Flight();
        fl.t0 = start + a * interval;
        fl.t1 = start + b * interval;
        fl.from = from;
        fl.to = to;
        return fl;
    }

    // Straight line over the ground, and the height a ball thrown under
    // gravity would have to arrive at the target at the end time.
    static Vector3 position(Flight fl, float t) {
        float duration = fl.t1 - fl.t0;
        float s = t - fl.t0;
        Vector3 p = Vector3.Lerp(fl.from, fl.to, Mathf.Clamp01(s / duration));
        float vy = (fl.to.y - fl.from.y + 0.5f * gravity * duration * duration) / duration;
        p.y = fl.from.y + vy * s - 0.5f * gravity * s * s;
        return p;
    }
}
