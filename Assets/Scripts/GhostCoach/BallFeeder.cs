using UnityEngine;

// Standalone ball-feeding game mode: spawns a ball on the far side of the
// table every `feed_interval` seconds and "serves" it (bouncing on the far
// side, then over the net to the same spot on the player's side) with little
// spin.  Runs on a fixed timer regardless of whether or how the player
// returns the ball.
public class BallFeeder : MonoBehaviour {

    public Play play;
    public Table table;

    public float feed_interval = 3.0f;     // Seconds between feeds.
    public float feed_speed = 4.2f;        // Horizontal speed (m/s).
    public float feed_topspin = 1.0f;      // Mild topspin (m/s at ball surface).

    // Coordinates relative to the full table center (at the net; y relative to top_y).
    public Vector3 spawn_offset = new Vector3(0.0f, 0.35f, 1.45f);
    public Vector3 far_bounce_offset = new Vector3(0.18f, 0.0f, 0.65f);

    public bool feeding { get; private set; }
    public int fed_count { get; private set; }
    public Vector3 last_spawn_position { get; private set; }
    public Vector3 last_launch_velocity { get; private set; }

    float timer = 0f;

    void Update() {
        if (!feeding)
            return;

        timer -= Time.deltaTime;
        if (timer <= 0f) {
            feed_ball();
            timer += feed_interval;
        }
    }

    public void start_feeding() {
        if (feeding)
            return;
        feeding = true;
        timer = 0f;

        if (play != null) {
            play.robot_active = false;
            if (play.robot != null)
                play.robot.auto_serve = false;
        }

        // Run standalone: hide GhostCoach visuals if present.
        GhostCoach coach = GetComponent<GhostCoach>();
        if (coach != null) {
            if (coach.coach_ghost != null)
                coach.coach_ghost.hide();
            if (coach.path_guide != null)
                coach.path_guide.hide();
        }

        show_message("Ball Feed: ON (every " + feed_interval.ToString("F1") + " s)");
    }

    public void stop_feeding() {
        if (!feeding)
            return;
        feeding = false;
        show_message("Ball Feed: OFF");
    }

    public void toggle_feeding() {
        if (feeding)
            stop_feeding();
        else
            start_feeding();
    }

    public void OnToggleBallFeed() {
        if (!ButtonDedup.accept("ToggleBallFeed"))
            return;
        toggle_feeding();
    }

    public Ball feed_ball() {
        if (play == null || play.balls == null || table == null)
            return null;

        Ball b = play.balls.new_ball();
        if (b == null)
            return null;

        float top_y;
        Vector3 center = TableSpace.center(table, out top_y);
        // TableSpace.center() returns the center of table_top (the near half);
        // shift to the middle of the full table (the net) so far_bounce lands on the far half.
        center.z += 0.5f * table.table_top.transform.lossyScale.z;
        Vector3 spawn = TableSpace.to_world(table.transform,
                                            new Vector3(center.x + spawn_offset.x,
                                                        top_y + spawn_offset.y,
                                                        center.z + spawn_offset.z));
        Vector3 far_bounce = TableSpace.to_world(table.transform,
                                                 new Vector3(center.x + far_bounce_offset.x,
                                                             top_y + b.radius + far_bounce_offset.y,
                                                             center.z + far_bounce_offset.z));

        Vector3 line = far_bounce - spawn;
        Vector3 plane_move = new Vector3(line.x, 0f, line.z);
        float range = plane_move.magnitude;
        Vector3 velocity;
        Vector3 angular_velocity;

        if (range > 0.001f) {
            float vy = b.vertical_speed_for_range(feed_speed, feed_topspin, -line.y, range);
            velocity = feed_speed * plane_move.normalized + vy * Vector3.up;
            Vector3 topspin_axis = Vector3.Cross(velocity, Vector3.up).normalized;
            angular_velocity = (-feed_topspin * topspin_axis) / b.radius;
        } else {
            velocity = Vector3.zero;
            angular_velocity = Vector3.zero;
        }

        last_spawn_position = spawn;
        last_launch_velocity = velocity;

        b.set_motion(new BallState(b, Time.time, spawn, velocity, angular_velocity));
        play.start_ball_feed(b);
        fed_count += 1;
        return b;
    }

    void show_message(string text) {
        Debug.Log("BallFeeder: " + text);
        if (play != null && play.billboard != null)
            play.billboard.text = text;
    }
}
