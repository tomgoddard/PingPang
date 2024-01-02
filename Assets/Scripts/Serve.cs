using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Serve {
	public string name;
	public Toss toss;
	public Vector3 ball_velocity;  // After leaving paddle
	public Vector3 ball_angular_velocity;  // After leaving paddle

	public Vector3 top, bounce, fall;   // Positions over net, on far table, and crossing table height
	public bool double_bounce;


	float swing_duration = 0.4f;    // seconds
	float serve_pause_time = 0.5f;  // seconds, delay after paddle behind ball before tossing
	float serve_set_time = 0.5f;    // seconds, time to move paddle behind ball for serving.
	float max_grazing_slope = 3f;

	public Serve() {
	       // Restoring serves with JsonFrom gives these
	       // values 0 if only the above initializers are used.
	       swing_duration = 0.4f;
	       serve_pause_time = 0.5f;
	       serve_set_time = 0.5f;
	       max_grazing_slope = 3f;
       }

	public Serve(string name, Toss toss, Vector3 vb, float avb) {
		this.name = name;
		this.toss = toss;
		this.ball_velocity = vb;
		this.ball_angular_velocity = avb * Vector3.Cross (vb, Vector3.up).normalized;
	}

	public Serve(Toss toss, BallTrack bt) {
		this.name = "unnamed";
		BallState bs = bt.initial;
		Toss rtoss = new Toss(flip_side(toss.position),
				      flip_side(toss.velocity),
				      bs.position.y);
		this.toss = rtoss;
		this.ball_velocity = flip_side(bs.velocity);
		this.ball_angular_velocity = flip_side(bs.angular_velocity);
		this.top = flip_side(bt.highest_over_net().position);
		this.bounce = flip_side(bt.final[0].position);
		this.fall = flip_side(bt.table_crossing().position);
		this.double_bounce = bt.double_bounce;
	}

	Vector3 flip_side(Vector3 v)
	{
		return new Vector3(-v.x, v.y, -v.z);
	}

	public PaddleMotion setup_to_serve(Transform paddle_position) {

		// Before starting serve place paddle behind ball.
		PaddleMotion set_to_serve = new PaddleMotion();
		float t_toss = 0f;
		float t_set = t_toss - serve_pause_time;
		float t_start = t_set - serve_set_time;
		Vector3 set_position = toss.position + new Vector3(0f,0f,0.2f);  // Behind ball
		Quaternion set_rotation = Quaternion.AngleAxis(90f, new Vector3(0f,0f,1f));
		Transform t = paddle_position;
		set_to_serve.set_motion(t_start, t.position, t.rotation,
			t_set, set_position, set_rotation);

		return set_to_serve;
	}

	public Stroke serve_ball(Ball ball, BallTracking ball_tracking, Paddle paddle) {

		// Determine stroke to produce out-going velocity and spin
		BallState hit_point = toss.toss_ball (ball, ball_tracking);
		float hit_time = hit_point.time;
		Vector3 wdrop = Vector3.zero;
		Bouncer bn = paddle.forehand_rubber ();
		Vector3 pn, pv;
		bn.wall_motion (hit_point.velocity, ball_velocity, wdrop,
			        ball_angular_velocity,
				ball.radius, ball.inertia_coef, max_grazing_slope,
				out pv, out pn);
		Vector3 php = hit_point.position - ball.radius * pn;
		Stroke s = new Stroke (hit_time, php, pn, pv, 0.5f*swing_duration, 0.5f*swing_duration);
		// Debug.Log("Planned hit time " + hit_time);
		return s;
	}
}

[System.Serializable]
public class Toss {
	public Vector3 position, velocity;
	public float hit_height;
	const float throw_acceleration = 9.8f;

	public Toss(Vector3 position, Vector3 velocity, float hit_height) {
		// In room coordinates.
		this.position = position;
		this.velocity = velocity;
		this.hit_height = hit_height;
	}

	public BallState toss_ball(Ball ball, BallTracking track) {
	        position_ball_for_toss(ball);
		bool serve = true;
		BallTrack bt = track.predict_ball_flight (ball, serve);
		BallState bs = bt.drop_position (hit_height);
		return bs;
	}

	public void position_ball_for_toss(Ball ball) {
		Transform room = ball.transform.parent;
		Vector3 rp = room.TransformPoint (position);
		Vector3 rv = room.TransformVector(velocity);
		ball.set_motion(new BallState(ball, 0f, rp, rv, Vector3.zero));
	}

	public float throw_start_time() {
	        return -velocity.magnitude / throw_acceleration;
	}

	public void throw_motion(Ball ball, float t) {
	        float tstart = throw_start_time();
	        if (t >= tstart)
		{
			Transform room = ball.transform.parent;
			Vector3 rp = room.TransformPoint (position);
			Vector3 rv = room.TransformVector(velocity);
			Vector3 vdir = rv.normalized;
			float tm = t - tstart;
			rp += 0.5f*throw_acceleration*(tm*tm - tstart*tstart)*vdir;
			rv *= tm / tstart;
			BallState bs = new BallState(ball, t, rp, rv, Vector3.zero);
			ball.set_motion(bs);
		}
	}
}
