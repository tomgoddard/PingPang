using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Robot : MonoBehaviour {
    public Paddle paddle;              // Robot's paddle
    public Paddle player_paddle;
    public Play play;

    public Serves serves;
    Serve [] active_serves;
    Serve cur_serve;
    float wait_to_serve = 0f;
    int return_count = 0;        // Number of returns made in rally.
    int serve_repeat_count = 1;
    int repeated_serve = 0;
    public bool auto_serve = false;

    public float swing_duration = 0.3f;       // seconds
    public float forehand_coverage = 0.75f;   // Fraction of table covered with forehand
    float backswing_pause_time = 0.0f;  // Time from end of backswing to start of stroke.
    float recovery_time = 0.5f;            // Time from end of swing to reaching recovery paddle position.
    Vector3 recovery_position = new Vector3 (0f, .85f, 2.5f);
    float max_grazing_slope = 1f;

    bool limit_acceleration = true;
    float max_backswing_acceleration = 40f;
    float max_stroke_acceleration = 100f;

    public BallTracking ball_tracking;
    public Table table;  // Use table dimensions

    [System.NonSerialized]  // Prevent changing these values in Unity inspector.
    public string [] patterns = { "Forehand", "Backhand", "Random forehand", "Random", "Diagonal", "Down line", "Elbow", "No return" };
    [System.NonSerialized]
    public int pattern = 0;

    [System.NonSerialized]
    public string [] speeds = { "Slow", "Medium", "Fast" };
    [System.NonSerialized]
    public int speed = 1;

    [System.NonSerialized]
    public string[] spins = { "Top spin", "No spin", "Back spin", "1 back spin, then top spin", "Varied", "Varied back spin", "Random top or back spin" };
    [System.NonSerialized]
    public int spin = 0;

    [System.NonSerialized]
    public string[] movement_speeds = { "Slow", "Medium", "Fast", "Unlimited" };
    [System.NonSerialized]
    public int movement_speed = 1;

    public bool drop = true;    // Allow robot drop shots.
    public bool smash = true;
    public bool loop = true;
    public bool chop = true;
    public bool block = true;
    
    float time_now = 0;
    List<PaddleMotion> motions = new List<PaddleMotion>();

    public Stroke return_ball(BallTrack ball_track) {
        return_count += 1;

        if (patterns [pattern] == "No return")
            return null;
        
        Stroke s = return_stroke(ball_track, return_count);
        set_stroke (s, ball_track.initial.time);
        return s;
    }

    Stroke return_stroke(BallTrack bt, int num_hit) {

	// Choose shot including the position on incoming ball path to hit
	// the ball and the desired ball speed, top spin and side spin after hit.
        BallState incoming_ball;
        Vector3 table_target;
        float speed, topspin, sidespin;
        choose_shot(bt, num_hit, out incoming_ball, out table_target,
		    out speed, out topspin, out sidespin);

	// Compute velocity and spin of outgoing ball needed
	// to strike the target table position.
        Vector3 bv, bav;
	aim_ball(incoming_ball, table_target, speed, topspin, sidespin,
		 out bv, out bav);
        //Debug.Log("Robot outgoing ball speed " + bv.magnitude + " spin " + bav.magnitude * b.radius);

	// Compute paddle motion to achieve outgoing ball velocity and spin.
	Vector3 paddle_velocity, paddle_normal;
	bool backhand;
	paddle_motion(incoming_ball, bv, bav,
		      out paddle_velocity, out paddle_normal, out backhand);

	// Compute paddle stroke.
        Stroke s = paddle_stroke(incoming_ball, paddle_velocity, paddle_normal, backhand);

        // If stroke acceleration is too high, try reducing target ball spin.
	s = limit_stroke_acceleration(s, incoming_ball, bv, bav);

        return s;
    }

    //
    // Compute velocity and spin of outgoing ball needed
    // to strike the target table position.
    //
    void aim_ball(BallState incoming_ball, Vector3 table_target,
		  float speed, float topspin, float sidespin,
		  out Vector3 bv, out Vector3 bav) {
	    
	// Compute table plane ball motion and vertical ball motion separately.
        Vector3 line = table_target - incoming_ball.position;
        Vector3 plane_move = new Vector3(line.x, 0f, line.z);
        float range = plane_move.magnitude;
        float vhorz = speed;
        Ball b = incoming_ball.ball;
        float vy = b.vertical_speed_for_range(vhorz, topspin, -line.y, range);
        bv = vhorz*plane_move.normalized + vy*Vector3.up;

	// Set desired outgoing spin.
	Vector3 topspin_axis = Vector3.Cross(bv, Vector3.up).normalized;
	Vector3 spin = -topspin * topspin_axis + sidespin * Vector3.up;
        bav = spin / b.radius;

	// Adjust ball velocity to compensate for sideways ball drift
	// due to Magnus force from side spin.
        float ks = 1f / 1500f;
        float a = -ks * range * bav.y / bv.magnitude;
        float adeg = a * 180f / Mathf.PI;
        bv = Quaternion.AngleAxis(adeg, Vector3.up) * bv;
    }

    void choose_shot(BallTrack bt, int num_hit,
                     out BallState incoming_ball, out Vector3 table_target,
		     out float speed, out float topspin, out float sidespin) {


	// Consider smash at top of bounce.
        BallState tbs = bt.top_of_bounce ();
	float ball_radius = tbs.ball.radius;

	// Reverse the incoming side spin.
	// TODO: Allow strokes to apply side spin.
        // Don't try to zero side spin since that leads to crazy looking sideways strokes.
	sidespin = -Vector3.Dot (tbs.angular_velocity, Vector3.up) * ball_radius;

        if (smash) {
          if (tbs.position.y >= table.height + 0.40f
	      && tbs.position.z <= 0.5f * table.length + 0.50f)
          {
            // TODO: Should choose lower hit height if bounce is too high.
	    incoming_ball = tbs;
	    table_target = placement(tbs);
            // Stroke calc results in hitting longer than requested
            // due to 1-dimensional drag approximation being violated.
            // So request a short hit depth.
            float hl = 0.5f * table.length;
            table_target.z = -0.5f*hl;
            float s = speed_level();
            topspin = 4f + 2f * s;
            speed = 7f + 8f * s;
	    Debug.Log("smash");
            return;
          }
        }

	// Consider hitting where ball crosses current paddle position behind table.
	BallState thbs = bt.crossing_point(table.height, Vector3.up);
        if (loop || chop) {
          if (thbs != null &&
	      thbs.position.z >= 0.6f * table.length &&
	      thbs.position.z <= paddle.position.z)
          {
	    incoming_ball = thbs;
	    table_target = placement(thbs);
            float s = speed_level();
	    bool loop_it = (loop && chop ? Random.value > 0.5f : loop);
	    if (loop_it) {
		speed = 6f + 6f * s;
		topspin = 8f + 4f * s;
	    } else {
		speed = 4f + 4f * s;
		topspin = -4f - 2f * s;
	    }
	    // Debug.Log((loop_it ? "loop" : "chop"));
            return;
          }
        }
//	if (thbs != null)
//	    Debug.Log("Not looping, ball at table height " + thbs.position.z + " paddle pos " + paddle.position.z);
	// If top of bounce is behind paddle, then consider blocking before top of bounce.
	bool top_behind = (tbs.position.z > paddle.position.z
			   && tbs.position.z > 0.5f * table.length);
	BallState cbs = bt.crossing_point(paddle.position.z, Vector3.forward);
//	if (cbs != null)
//	    Debug.Log("top behind " + top_behind + " bvel " + cbs.velocity.magnitude);
	if (block) {
	    if (top_behind && cbs != null && cbs.velocity.magnitude > 6f)
	    {
		// Block return is modeled on the following simple scenario:
		// Paddle motion directly opposite ball motion, and return ball
		// moves directly opposite incoming ball.  With these assumptions
		// the spin is reduced by sqrt(rebound energy factor), outgoing
		// ball speed can be any value.
		incoming_ball = cbs;
		table_target = placement(cbs);
		float f = Mathf.Sqrt(0.7f);
		float bspeed = cbs.velocity.magnitude;
		speed = Mathf.Max(f * bspeed, 8.0f);  // Don't move paddle backwards.
		Vector3 bspin = cbs.angular_velocity * ball_radius;
		Vector3 top_axis = Vector3.Cross (cbs.velocity, Vector3.up).normalized;
		float btop = Vector3.Dot(bspin, top_axis);
		topspin = -f * btop;
		// Debug.Log("block");
		return;
	    }
	}
	
        if (drop) {
          // Drop return if receive short backspin not too high.
          if (tbs.position.z <= 1f &&
	      tbs.position.y <= table.height + 0.30f &&
	      tbs.angular_velocity.x * ball_radius < 1f)
          {
	    incoming_ball = tbs;
	    table_target = placement(tbs);
            table_target.z = Random.Range(-0.80f, -0.50f);   // Depth
            float s = speed_level();
            topspin = -3f - 2f*s;
            speed = 3f + 2f*s;
	    // Debug.Log("drop");
            return;
          }
        }

	// Hit at top of bounce if it is in front of paddle,
	// otherwise hit at current paddle distance behind table.
	BallState bs = (top_behind && cbs != null ? cbs : tbs);
	incoming_ball = bs;
	table_target = placement(bs);
        topspin = top_spin (num_hit);
        float distance = (table_target - bs.position).magnitude;
        speed = speed_for_distance(distance, topspin);
	// Debug.Log("hit");
    }

    Vector3 reduce_spin(Vector3 bav, Vector3 bv, float delta_av) {
        return bav + delta_av * Vector3.Cross(bv, Vector3.up).normalized;
    }

    //
    // Determine paddle velocity and orientation to strike incoming ball
    // to obtain the specified outgoing ball velocity and angular velocity.
    //
    void paddle_motion(BallState incoming_ball,
		       Vector3 outgoing_ball_velocity,
		       Vector3 outgoing_ball_angular_velocity,
		       out Vector3 paddle_velocity,
		       out Vector3 paddle_normal,
		       out bool backhand) {

        BallState bs = incoming_ball;

        // Find paddle motion to produce target ball velocity and spin
        Bouncer bn = paddle.forehand_rubber();
        Ball b = incoming_ball.ball;
        bn.wall_motion (bs.velocity, outgoing_ball_velocity,
			bs.angular_velocity, outgoing_ball_angular_velocity,
                        b.radius, b.inertia_coef, max_grazing_slope,
                        out paddle_velocity, out paddle_normal);

        // Decide which side of paddle to use.
        backhand = (bs.position.x > (forehand_coverage - 0.5f)*table.width);
    }
    
    Stroke paddle_stroke(BallState incoming_ball,
			 Vector3 paddle_velocity, Vector3 paddle_normal,
			 bool backhand) {


	Vector3 pv = paddle_velocity;
	Vector3 pn = paddle_normal;
        BallState bs = incoming_ball;
        Ball b = incoming_ball.ball;

        // Get center of paddle blade by offset from ball position.
        Vector3 paddle_pos = bs.position - pn * (b.radius + 0.5f * paddle.thickness);

        // Shorten swing if needed to avoid hitting table.
        float pre_swing_duration = 0.5f*swing_duration;
        float post_swing_duration = 0.5f*swing_duration;
        float d;
        float ave_speed = 0.5f * pv.magnitude;
        if (table_strike_distance(paddle_pos, -pv, pn, out d)
            && ave_speed * pre_swing_duration > d)
            pre_swing_duration = d / ave_speed;
        if (table_strike_distance(paddle_pos, pv, pn, out d)
            && ave_speed * post_swing_duration > d)
            post_swing_duration = d / ave_speed;

        /*
	// Peak accel for sinusoidal profile.
        float a = (Mathf.PI / 2) * (pv.magnitude / pre_swing_duration);
        Debug.Log("Paddle speed " + pv.magnitude + " height " + (bs.position.y - table.height)
	          + " time " + pre_swing_duration + " accel " + a
		  + " spin " + outgoing_ball_angular_velocity.magnitude * b.radius
		  + " ball speed " + outgoing_ball_velocity.magnitude);
        */

        // Create paddle stroke to produce hit.
        Stroke s = new Stroke (bs.time, paddle_pos, pn, pv,
			       pre_swing_duration, post_swing_duration,
			       backhand);
        return s;
    }

    //
    // If stroke acceleration is larger than maximum allowed
    // try reducing target ball spin to reduce paddle speed and
    // required acceleration.
    //
    Stroke limit_stroke_acceleration(Stroke s, BallState incoming_ball, Vector3 bv, Vector3 bav) {

	float a = s.pre_swing_acceleration();
        if (a <= max_stroke_acceleration)
	    return s;

	float f = max_stroke_acceleration / a;
	float dv = (1f - Mathf.Sqrt(f)) * s.velocity.magnitude;
	float dw = 2f*dv/incoming_ball.ball.radius;
	Vector3 bav2 = reduce_spin(bav, bv, dw);
	Vector3 paddle_velocity, paddle_normal;
	bool backhand;
	paddle_motion(incoming_ball, bv, bav2,
		      out paddle_velocity, out paddle_normal, out backhand);
	Stroke ls = paddle_stroke(incoming_ball, paddle_velocity, paddle_normal, backhand);
	// Debug.Log("Reduced ball spin to reduce accel " + a + " to " + s.pre_swing_acceleration()
	//    + " target " + max_stroke_acceleration);
	return ls;
    }

    //
    // Compute travel distance of paddle backwards from ball contact where paddle hits the table.
    // This is used to adjust stroke so paddle does not strike table.
    //
    bool table_strike_distance(Vector3 hit_position, Vector3 paddle_velocity, Vector3 paddle_normal,
			       out float d)
    {
        d = 0;
        float h = table.height;
        if (paddle_velocity.y >= 0)
            return false;
        float ph = Mathf.Sqrt(1.0f - paddle_normal.y * paddle_normal.y) * 0.5f * paddle.width;
        float above_table = hit_position.y - ph - table.height;
        if (above_table <= 0)
            return false;
        float t = above_table / -paddle_velocity.y;
        Vector3 tpos = hit_position + t*paddle_velocity;
        // Add paddle width to edges of table to avoid paddle passing through edge.
        bool over_table = (Mathf.Abs(tpos.x) <= 0.5f * table.width + paddle.width
                            && Mathf.Abs(tpos.z) <= 0.5f * table.length);
        if (over_table) {
            d = t * paddle_velocity.magnitude;
            return true;
        }
        return false;
    }

    public void new_rally() {
           return_count = 0;
        }
           
    Vector3 placement(BallState bs) {
        // Place on table ball should strike.
        // Table half-width is 0.762 meters.
        float x = 0, z = -1.1f;
        string p = patterns [pattern];
        if (p == "Forehand")
            x = 0.5f;
        else if (p == "Backhand")
            x = -0.5f;
        else if (p == "Random forehand")
            x = Random.Range (0f, 0.6f);
        else if (p == "Random")
            x = Random.Range (-0.6f, 0.6f);
        else if (p == "Diagonal")
            x = (bs.position.x >= 0 ? -0.5f : 0.5f);
        else if (p == "Down line")
            x = (bs.position.x >= 0 ? 0.5f : -0.5f);
        else if (p == "Elbow") {
            Vector3 b = bs.position;
            Vector3 elbow_offset = new Vector3 (0, -0.45f, 0);
            Vector3 e = player_paddle.transform.TransformPoint (elbow_offset);
            float f = (z - e.z) / (b.z - e.z);
            x = (1 - f) * e.x + f * b.x;
        }
        float y = table.height + bs.ball.radius;
        Vector3 table_target = new Vector3 (x, y, z);
        return table_target;
    }

    float top_spin(int num_hit) {
        // Spin
        float top_spin = 0f;
        float spin_mag = 0f;
        if (speeds [speed] == "Slow")
            spin_mag = 4f;
        else if (speeds [speed] == "Medium")
            spin_mag = 5f;
        else if (speeds [speed] == "Fast")
            spin_mag = 7f;
        if (spins [spin] == "Top spin")
            top_spin = spin_mag;
        else if (spins [spin] == "No spin")
            top_spin = 0;
        else if (spins [spin] == "Back spin")
            top_spin = -spin_mag;
        else if (spins [spin] == "1 back spin, then top spin")
            top_spin = (num_hit == 1 ? -spin_mag : spin_mag);
        else if (spins [spin] == "Varied")
            top_spin = Random.Range (-spin_mag, spin_mag);
        else if (spins [spin] == "Varied back spin")
            top_spin = Random.Range (-spin_mag, 0f);
        else if (spins [spin] == "Random top or back spin")
            top_spin = (Random.Range (0f, 1f) > 0.5f ? spin_mag : -spin_mag);
        return top_spin;
    }

    float speed_for_distance(float distance, float topspin) {
        // Distance for fixed elevation angle with no drag of spin, d ~ v*v/g.
        // So use v ~ sqrt(d).
        float s = speed_level();
        float speed = (3f + s + 0.1f * topspin) * Mathf.Sqrt(distance);
        return speed;
    }

    float speed_level() {
        float s = 0f;
        if (speeds [speed] == "Slow")
            s = 0f;
        else if (speeds [speed] == "Medium")
            s = 0.5f;
        else if (speeds [speed] == "Fast")
            s = 1.0f;
        return s;
    }

    Stroke block_return(BallState bs, Ball b) {
        Vector3 spin = b.radius * bs.angular_velocity;
        //Debug.Log ("Top of bounce time " + bs.time + " position " + bs.position.ToString ("F4")
        //    + " velocity " + bs.velocity.ToString("F4") + " spin " + spin.ToString("F4"));
        Vector3 v = -bs.velocity;
        // Debug.Log ("Return time " + bs.time + " velocity " + v.ToString ("F4"));
        // TODO: Compute top spin and angle paddle to compensate.
        float side_spin = spin.y;
        float top_spin = Vector3.Dot(spin, Vector3.Cross (v, Vector3.up).normalized);
        Vector3 vside = side_spin * Vector3.Cross (v, Vector3.up).normalized;
        Vector3 vtop = top_spin * Vector3.up;
        Vector3 v0 = v + 0.5f * vside;
        Vector3 vp = v0 + 0.5f * vtop;
        vp = Mathf.Max(1.4f - 0.2f*vp.magnitude, 0f) * vp;
        Vector3 np = v0 - 0.3f * top_spin * Vector3.up;
        np = np.normalized;
        bool backhand = (bs.position.x > .38); // Cover 3/4 of table with forehand
        Stroke s = new Stroke (bs.time, bs.position, np, vp, 0.5f*swing_duration, 0.5f*swing_duration, backhand);
        return s;
    }
    
    // Update paddle position to match hand controller position.
    public void move_paddle(float delta_t) {
        // Check if serve setup has completed and time to toss ball.
        // This has to be done before paddle and ball motion so that
        // paddle and ball motion do not get off by one time step.
        if (wait_to_serve > 0f)
        {
            wait_to_serve -= delta_t;
            if (wait_to_serve <= 0f)
                serve_toss();
            else
            {
                // Robot ball motion while in hand.
                Serve s = current_serve();
                Ball ball = play.ball_in_play;
                s.toss.throw_motion(ball, -wait_to_serve);
            }
        }

        time_now += delta_t;

        if (motions.Count == 0)
            return;

        PaddleMotion m = motions [0];
        bool done = m.move (time_now, paddle);
        if (done)
            motions.Remove (m);
    }

    public void set_stroke(Stroke s, float time_now) {
        this.time_now = time_now;

        motions.Clear();

        // Backswing
        Transform t = paddle.transform;
        PaddleMotion backswing = backswing_motion(time_now, t.position, t.rotation,
            s.start_time, s.start_position, s.start_rotation);
        limit_backswing_acceleration (backswing, s);
        motions.Add (backswing);

        // Stroke
        motions.Add (s);

        // Recovery
        PaddleMotion recovery = recovery_motion(s.end_time, s.end_position, s.end_rotation);
        motions.Add (recovery);

        /*
        Debug.Log ("Robot acceleration,"
                    + " backswing " + backswing.acceleration ().ToString ("F1")
                    + " stroke " + s.acceleration ().ToString ("F1")
                    + " recovery " + recovery.acceleration ().ToString ("F1"));
        */
    }

    public void set_motion(PaddleMotion motion, float time_now) {
        this.time_now = time_now;
        motions.Clear();
        motions.Add(motion);
    }

    PaddleMotion backswing_motion(float start_time, Vector3 start_position, Quaternion start_rotation,
        float end_time, Vector3 end_position, Quaternion end_rotation)
    {
        // Create backswing motion.
        float bs_end = Mathf.Max (end_time - backswing_pause_time, start_time);
        PaddleMotion backswing = new PaddleMotion();
        backswing.set_motion(start_time, start_position, start_rotation,
                            bs_end, end_position, end_rotation);
        return backswing;
    }

    bool limit_backswing_acceleration(PaddleMotion backswing, Stroke s) {
        
        if (!limit_acceleration)
            return false;
        
        float ba = backswing.acceleration ();
        // Debug.Log("Backswing accel " + backswing.acceleration() + " max " + max_backswing_acceleration + " duration " + (backswing.end_time - backswing.start_time));
        if (ba <= max_backswing_acceleration)
            return false;
        
        // Lengthen backswing time to stay within maximum acceleration.
        float f = Mathf.Sqrt (ba / max_backswing_acceleration);
        float bs_start = backswing.start_time;
        float bs_end = bs_start + (backswing.end_time - bs_start) * f;
        backswing.set_motion(bs_start, backswing.start_position, backswing.start_rotation,
                             bs_end, s.start_position, s.start_rotation);
        // Debug.Log("Reduced backswing accel " + backswing.acceleration() + " max " + max_backswing_acceleration);

        // If stroke begins before backswing ends then shorten stroke.
        if (bs_end > s.start_time) {
            float fs = Mathf.Sqrt (s.pre_swing_acceleration () / max_stroke_acceleration);
            float min_dur = fs * s.pre_swing_duration;
            float new_dur = s.pre_swing_duration - (bs_end - s.start_time);
            new_dur = Mathf.Max (new_dur, min_dur);
            s.change_pre_swing_duration (new_dur);
            // Fix backswing end position for shorter stroke.
            backswing.set_motion(bs_start, backswing.start_position, backswing.start_rotation,
                                bs_end, s.start_position, s.start_rotation);
            // Debug.Log ("Shortened stroke duration to " + tmin.ToString ("F2"));
            // Debug.Log("new backswing accel " + backswing.acceleration() + " new stroke accel " + s.pre_swing_acceleration());
            // If there still not time for shorter maximum acceleration stroke
            // then delay the stroke. This will often cause a ball miss.
            if (bs_end > s.start_time) {
                float dt = bs_end - s.start_time;
                s.start_time += dt;
                s.end_time += dt;
                // Debug.Log ("Delayed stroke " + dt.ToString ("F2"));
            }
        }
        return true;
    }

    PaddleMotion recovery_motion(float time, Vector3 position, Quaternion rotation)
    {
        // Create paddle motion from specified position to center of table.
        float r_end = time + recovery_time;
        Quaternion r_rot = (Quaternion.AngleAxis(-30, new Vector3(1f,0f,0f))        // tilt tip down
            * Quaternion.AngleAxis(20, new Vector3(0f,1f,0f))      // tilt tip right, favoring forehand
            * Quaternion.AngleAxis(90, new Vector3(0f,0f,1f))      // point blade normal to horizontal
            * Quaternion.AngleAxis (-90, new Vector3(1f,0f,0f)));  // point tip forward.
        PaddleMotion recovery = new PaddleMotion ();
        recovery.set_motion(time, position, rotation, r_end, recovery_position, r_rot);
        return recovery;
    }

    public void set_movement_speed() {
        string s = movement_speeds[movement_speed];
        if (s == "Slow") {
            limit_acceleration = true;
            max_backswing_acceleration = 20f;
            max_stroke_acceleration = 50f;
        } else if (s == "Medium") {    
            limit_acceleration = true;
            max_backswing_acceleration = 40f;
            max_stroke_acceleration = 100f;
        } else if (s == "Fast") {
            limit_acceleration = true;
            max_backswing_acceleration = 60f;
            max_stroke_acceleration = 150f;
        } else if (s == "Unlimited") {
            limit_acceleration = false;
        }
    }

    public void serve() {
        new_rally();
        Serve s;
	if (repeated_serve >= serve_repeat_count || cur_serve == null)
	    s = new_serve ();
	else
	{
	    s = cur_serve;
	    repeated_serve += 1;
	}
        Ball ball = play.start_robot_serve();
        s.toss.throw_motion(ball, s.toss.throw_start_time());
        ball.freeze = true;
        PaddleMotion motion = s.setup_to_serve(paddle.transform);
        set_motion(motion, motion.start_time);
        wait_to_serve = 0.5f;
//        Debug.Log("Serve hit height " + s.toss.hit_height + " ball vel " + s.ball_velocity + " ball ang vel " + s.ball_angular_velocity);
    }

    void serve_toss() {
            Ball ball = play.ball_in_play;
        ball.freeze = false;
        Serve s = current_serve ();
        Stroke stroke = s.serve_ball (ball, play.ball_tracking, paddle);
        set_stroke(stroke, ball.motion.time);
    }

    public void use_these_serves(List<Serve> serves)
    {
        this.active_serves = serves.ToArray();
    }

    public void set_serve_repeat(int repeat)
    {
	this.serve_repeat_count = repeat;
    }
    
    public Serve new_serve()
    {
        if (active_serves == null)
           use_these_serves(serves.serves);
        int i = UnityEngine.Random.Range (0, active_serves.Length);
	this.cur_serve = active_serves[i];
	this.repeated_serve = 1;
	return cur_serve;
    }

    public Serve current_serve()
    {
        return cur_serve;
    }

}
