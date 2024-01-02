using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Ball : MonoBehaviour {

	public BallState motion;
	public Vector3 gravity = new Vector3(0f, -9.8f, 0f);
	public float radius = 0.02f;
	public float mass = 0.0027f;  // 2.7 grams
	public float inertia_coef = 2.0f/3;  // Spherical shell inertial 2/3 m*r*r.
	public float air_density = 1.27f; // kg/m**3
	public float drag_coefficient = 0.5f;
	public float lift_coefficient = 0.28f;  // Magnus effect
	float cdrag, clift;
	public bool freeze = false; // Used for instant replay.

	// Use this for initialization
	void Awake () {
		motion = new BallState (this, 0f, transform.position, Vector3.zero, Vector3.zero);
		float area = Mathf.PI * radius * radius;
		cdrag = 0.5f * drag_coefficient * air_density * area / mass;
		clift = 0.5f * lift_coefficient * air_density * area * radius / mass;
	}
		
	public void set_motion(BallState bs) {
		float time_step = bs.time - motion.time;
	
		transform.position = bs.position;
		motion = bs;

		// Update orientation of ball so markings on ball rotate.
		float a = Mathf.Rad2Deg * time_step * bs.angular_velocity.magnitude;  // Rotation in degrees.
		Vector3 axis = bs.angular_velocity.normalized;
		transform.rotation = Quaternion.AngleAxis (a, axis) * transform.rotation;
	}

	public BallState move_ball(float delta_t) {

		if (freeze)
			return null;

		if (transform.position.y < -1) {
			// Deactivate balls after they go below the floor.
			gameObject.SetActive (false);
			freeze = true;
			return null;
		}
		BallState bs = ball_time_step (motion, delta_t);
		return bs;
	}

	public BallState ball_time_step(BallState bs, float t) {
		Vector3 v = bs.velocity;
		Vector3 drag = -cdrag * v.magnitude * v;
		Vector3 lift = clift * Vector3.Cross (bs.angular_velocity, v);
		Vector3 accel = gravity + drag + lift;
		Vector3 p = bs.position + v * t + accel * (0.5f*t*t);
		v += t * accel;
		BallState bs2 = new BallState (this, bs.time + t, p, v, bs.angular_velocity);
		return bs2;
	}

	public float vertical_speed_for_range(float vhorz, float spin, float height, float range) {
	       //
	       // Assume 1D motion with drag to compute x postion versus time.
	       //    dv/dt = -k * v*v
	       //    v = v0 / (1 + k*v0*t)
	       //    x = (1/k) * log(1 + k*v0*t)
	       //    exp(kx) = 1 + k*v0*t
	       //    v = v0 * exp(-kx)
	       //	       
	       // Use 1D motion to get time to reach x position.  Then compute
	       // vertical speed needed so ball drops to table in that time under
	       // gravity and magnus force.
	       //   h'' = -g - C*w*v
	       //   h' = -gt - (C*w/k)*log(1+k*v0*t) + vy
	       //   h = h0 + vy*t -(1/2)*g*t*t - (C*w/(k*k*v0))*(exp(kx)*(kx-1) + 1)
	       //
	       float k = cdrag, C = clift, g = -gravity.y;
	       float v0 = vhorz, x = range, h0 = height, w = spin / radius;
	       float t = (Mathf.Exp(k*x) - 1) / (k * v0);
	       float hmagnus = (C*w/(k*k*v0))*(Mathf.Exp(k*x)*(k*x-1) + 1);
	       float hgravity = 0.5f*g*t*t;
	       float vy = (-h0 + hgravity + hmagnus) / t;
	       return vy;
	}
}
