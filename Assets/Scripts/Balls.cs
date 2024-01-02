using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Balls : MonoBehaviour {

	public Play play; 
	public Ball ball;   // Original ball for duplicating
	public Material white_ball, orange_striped_ball;
	List<Ball> balls = new List<Ball>();
	int cur_ball;
	public int ball_count = 3;

	public List<Bouncer> bouncers;

	// Use this for initialization
	void Start () {
		balls.Add (ball);
		cur_ball = 0;
	}

	public Ball new_ball () {
		if (balls.Count < ball_count) {
			GameObject bo = Object.Instantiate (ball.gameObject, transform);
			Ball bc = bo.GetComponent<Ball> ();
			balls.Add (bc);
			cur_ball = balls.Count - 1;
		} else {
			cur_ball = (cur_ball + 1) % balls.Count;
		}
		Ball b = balls [cur_ball];
		if (play.ball_held (b))
			return new_ball ();
		b.gameObject.SetActive (true);
		b.freeze = false;
		return b;
	}

	public void move_balls(float delta_t) {
		foreach (Ball b in balls) {
			BallState bs1 = b.motion;
			BallState bs2 = b.move_ball (delta_t);
			if (bs2 != null) {
				BallState bsf = compute_rebounds (bs1, bs2, b);
				b.set_motion (bsf);
			}
		}
	}

	BallState compute_rebounds(BallState bs1, BallState bs2, Ball b) {
		// Adjust ball position and velocity for bouncing off paddle, table, floor, net.
		int max_rebounds = 2;
		for (int r = 0; r < max_rebounds; ++r) {
			Rebound first_rb = null;
			foreach (Bouncer bn in bouncers) {
				Rebound rb = bn.check_for_bounce (bs1, bs2, b.radius, b.inertia_coef);
				if (rb != null && first_rb != null) {
					Debug.Log ("Hit " + rb.bouncer.gameObject.name + " time " + rb.contact.time.ToString("F7") +
						" and " + first_rb.bouncer.gameObject.name + " time " + first_rb.contact.time.ToString("F7"));
				}

				if (rb != null && (first_rb == null || rb.contact.time < first_rb.contact.time))
					first_rb = rb;
			}
			if (first_rb == null)
				break;
			first_rb.set_ball (b);
			play.ball_bounced (b, first_rb.bouncer);
			bs1 = first_rb.contact;
			bs2 = first_rb.final;
		}
		return bs2;
	}

	public void set_ball_coloring(string name)
	{
		Material m = white_ball;
		if (name == "orange striped")
			m = orange_striped_ball;
		foreach (Ball b in balls) {
			MeshRenderer r = b.GetComponent<MeshRenderer>();
			r.material = m;
		}
	}
}

