using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Stroke : PaddleMotion {
	public float contact_time;  // Time at ball contact.
	public float pre_swing_duration, post_swing_duration;  // Duration of swing before contact and after.
	public Vector3 position, normal, velocity;  // State of paddle at ball contact.
	float elbow_distance = 0.45f;  // meters from paddle center to elbow for paddle swing.
	Vector3 elbow_position;  // Located elbow distance along handle direction from paddle center.
	Vector3 elbow_rotation_axis;  // Axis of swing.
	float pre_rotation_angle, post_rotation_angle;       // Swing rotation. Radians.
	Quaternion paddle_rotation;   // Orientation at contact.
	bool backhand;

	public Stroke(float time, Vector3 position,
					Vector3 normal, Vector3 velocity,
					float pre_swing_duration, float post_swing_duration,
					bool backhand = false) {
		this.contact_time = time;
		this.position = position;
		this.normal = normal;
		this.velocity = velocity;
		this.pre_swing_duration = pre_swing_duration;
		this.post_swing_duration = post_swing_duration;
		this.backhand = backhand;

		compute_elbow ();
		compute_swing ();
	}

	void compute_swing() {
		float stime = contact_time - pre_swing_duration;
		float etime = contact_time + post_swing_duration;
		Quaternion sr = Quaternion.AngleAxis (-pre_rotation_angle * Mathf.Rad2Deg, elbow_rotation_axis);
		Quaternion er = Quaternion.AngleAxis (post_rotation_angle * Mathf.Rad2Deg, elbow_rotation_axis);
		Vector3 spos = elbow_position + sr * (position - elbow_position);
		Vector3 epos = elbow_position + er * (position - elbow_position);
		Quaternion srot = sr * paddle_rotation;
		Quaternion erot = er * paddle_rotation;
		set_motion(stime, spos, srot, etime, epos, erot);
	}

	void compute_elbow() {
		// Compute elbow position.
		// Pivot paddle around elbow.
		Vector3 elbow_direction = Vector3.Cross (velocity, normal).normalized;
		if (elbow_direction.magnitude == 0)
			elbow_direction = new Vector3(1,0,0); // parallel back of table.
		if ((backhand && elbow_direction.x > 0) ||
			(!backhand && elbow_direction.x < 0))
			elbow_direction = -elbow_direction;
		//Vector3 ed = Vector3.Cross(normal, v);
		//if (ed.magnitude == 0)
		//	ed = Vector3.Cross (v, Vector3.up);
		elbow_position = position + elbow_distance * elbow_direction;
		elbow_rotation_axis = Vector3.Cross(velocity, elbow_direction).normalized;
		float ave_angle_vel = 0.5f * velocity.magnitude / elbow_distance;
		pre_rotation_angle =  pre_swing_duration * ave_angle_vel;
		post_rotation_angle = post_swing_duration * ave_angle_vel;
		paddle_rotation = (backhand ?
			Quaternion.LookRotation (normal, -elbow_direction) :
			Quaternion.LookRotation (-normal, -elbow_direction));
		/*
		paddle_rotation = (Quaternion.LookRotation(normal, elbow_direction)
							* (backhand ?
								Quaternion.LookRotation(-Vector3.right, -Vector3.forward) :
								Quaternion.LookRotation(Vector3.right, Vector3.forward)));
								*/
	}

	public float pre_swing_acceleration() {
		return (Mathf.PI / 2f) * velocity.magnitude / pre_swing_duration;
	}

	public void change_pre_swing_duration(float pre_swing_duration) {
		this.pre_swing_duration = pre_swing_duration;
		compute_swing ();
	}

	public void jump_to_start(Paddle p) {
		// Jump to start of swing.
		move (0f, p);
		// Avoid collisions on jump to backswing position.
		foreach (Bouncer bc in p.gameObject.GetComponentsInChildren<Bouncer> ())
			bc.jump_wall ();
	}

	public override bool move(float time, Paddle paddle) {
		float t = time;
		float f;
		if (t <= start_time)
			f = 0;
		else if (t >= end_time)
			f = 1;
		else if (t <= contact_time)
			f = 0.5f * (t - start_time) / pre_swing_duration;
		else
			f = 0.5f + 0.5f * (t - contact_time) / post_swing_duration;

		// Use acceleration sin(2pi*t/T).
		float pi2 = 2*Mathf.PI;
		float af = 2f * ((f-0.5f) - Mathf.Sin(f*pi2) / pi2);  // angle factor
		float vf = 0.5f*(1f - Mathf.Cos(f*pi2));   // velocity factor

		float a = af * (f <= 0.5 ? pre_rotation_angle : post_rotation_angle);
		Quaternion r = Quaternion.AngleAxis (a * Mathf.Rad2Deg, elbow_rotation_axis);
		Vector3 p = elbow_position + r * (position - elbow_position);
		Quaternion rp = r * paddle_rotation;
		Vector3 vp = ((f > 0 && f < 1) ? vf * (r * velocity) : Vector3.zero);
		Vector3 vap = ((f > 0 && f < 1) ? elbow_rotation_axis * (vf / elbow_distance) : Vector3.zero);

		// No elbow rotation, uniform velocity.
		// Vector3 p = position + (f-0.5f) * swing_duration * velocity;
		// Quaternion rp = paddle_rotation;
		// Vector3 vp = velocity;

		paddle.move(p, rp, vp, vap);

		bool done = (f >= 1);
		return done;
	}
}
