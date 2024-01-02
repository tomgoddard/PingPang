using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BallState {
        public Ball ball;
	public float time;
	public Vector3 position, velocity, angular_velocity;

	public BallState(Ball ball, float time, Vector3 position, Vector3 velocity, Vector3 angular_velocity) {
	        this.ball = ball;
		this.time = time;
		this.position = position;
		this.velocity = velocity;
		this.angular_velocity = angular_velocity;
	}

	public BallState interpolate(BallState s, float f) {
		float t = (1 - f) * time + f * s.time;
		Vector3 p = Vector3.LerpUnclamped (position, s.position, f);
		Vector3 v = Vector3.LerpUnclamped (velocity, s.velocity, f);
		Vector3 av = Vector3.LerpUnclamped (angular_velocity, s.angular_velocity, f);
		BallState ib = new BallState (ball, t, p, v, av);
		return ib;
	}
}

public class BallTrack {
	public BallState initial;
	public bool in_play;
	public bool hit_net;
	public bool double_bounce;
	public List<BallState> serve, over_net, final;

	public void initialize(BallState bs) {
		initial = bs;
		in_play = false;
		hit_net = false;
		double_bounce = false;
		serve = new List<BallState> ();
		over_net = new List<BallState> ();
		final = new List<BallState> ();
	}

	public BallState highest_over_net() {
		float ymax = -1;
		BallState top = null;
		foreach (BallState bs in over_net) {
			if (bs.position.y > ymax) {
				ymax = bs.position.y;
				top = bs;
			}
		}
		return top;
	}

	public BallState top_of_bounce() {
		float yprev = -1;
		BallState top = null;
		foreach (BallState bs in final) {
			if (bs.position.y > yprev)
				yprev = bs.position.y;
			else
				break;
			top = bs;
		}
		return top;
	}

        public BallState crossing_point(float p, Vector3 axis) {
	    float p1 = p;
	    foreach (BallState bs in final) {
		float p2 = Vector3.Dot(bs.position, axis);
		if ((p1 < p && p2 >= p) || (p1 > p && p2 <= p))
		    return bs;
		p1 = p2;
	    }
	    return null;
	}

	public BallState table_crossing() {
	       // Find place where ball descends below table height.
	        if (double_bounce)
	       	   return final[final.Count - 1];
		   
		BallState top = final[0];
		float ystart = top.position.y;
		foreach (BallState bs in final) {
			if (bs.position.y < ystart) {
			   	top = bs;
				break;
			}
		}
		return top;
	}

	public BallState drop_position(float height) {
		BallState drop = null;
		BallState last = null;
		foreach (BallState bs in serve) {
			float h = bs.position.y;
			if (bs.velocity.y <= 0 && h < height) {
				float f = (height - h) / (last.position.y - h);
				drop = last.interpolate (bs, f);
				break;
			}
			last = bs;
		}
		return drop;
	}
}

public class BallTracking : MonoBehaviour {
	public List<Bouncer> bouncers;
	float time_step = 1.0f/90;

	public BallTrack predict_ball_flight(Ball b, bool serve) {
		BallTrack bt = new BallTrack ();
		compute_ball_flight (b, serve, ref bt);
		return bt;
	}

	bool compute_ball_flight(Ball b, bool is_serve, ref BallTrack track) {
		BallState bs = b.motion;
		track.initialize (bs);

		string hit;
		if (is_serve) {
			hit = flight_to_bounce (b, ref bs, ref track.serve);
			if (hit != "table_near")
				return false;
		}

		hit = flight_to_bounce (b, ref bs, ref track.over_net);
		while (hit == "net") {
			track.hit_net = true;
			hit = flight_to_bounce (b, ref bs, ref track.over_net);
		}
		
		if (hit != "table_far")
			return false;

		hit = flight_to_bounce (b, ref bs, ref track.final);
		if (hit == "table_far")
			track.double_bounce = true;

		if (is_serve && track.hit_net)
			track.in_play = false;
		else
			track.in_play = true;
			
		return true;
	}

	string flight_to_bounce (Ball b, ref BallState s, ref List<BallState> path) {
		int max_time_steps = 1000;
		for (int i = 0; i < max_time_steps; ++i) {			
			path.Add (s);
			BallState s2 = b.ball_time_step (s, time_step);
			foreach (Bouncer bn in bouncers) {
				Rebound rb = bn.check_for_bounce (s, s2, b.radius, b.inertia_coef);
				if (rb != null) {
					s = rb.final;
					return bn.tag;
				}
			}
			s = s2;
		}
		return "too many time steps";
	}
}
