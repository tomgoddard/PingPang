using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PaddleMotion {
	public float start_time, end_time;
	public Vector3 start_position, end_position;
	public Quaternion start_rotation, end_rotation;
	Vector3 ave_velocity, ave_angular_velocity;

	public PaddleMotion() {}

	public void set_motion(float start_time, Vector3 start_position, Quaternion start_rotation,
			       float end_time, Vector3 end_position, Quaternion end_rotation) {
		this.start_time = start_time;
		this.start_position = start_position;
		this.start_rotation = start_rotation;
		this.end_time = end_time;
		this.end_position = end_position;
		this.end_rotation = end_rotation;

		float duration = end_time - start_time;
		if (duration > 0) {
			ave_velocity = (end_position - start_position) / duration;
			Quaternion rot = end_rotation * Quaternion.Inverse (start_rotation);
			float ra;
			Vector3 raxis;
			rot.ToAngleAxis (out ra, out raxis);
			ave_angular_velocity = raxis * (Mathf.Deg2Rad * ra / duration);
		} else {
			ave_velocity = Vector3.zero;
			ave_angular_velocity = Vector3.zero;
		}
	}

	public virtual bool move(float time, Paddle paddle) {
		if (time >= start_time && time <= end_time) {	
			float f = (time - start_time) / (end_time - start_time);
			f = Mathf.Min(f, 1);
			// TODO: Interpolate elbow position and rotation about elbow.
			float pi2 = 2*Mathf.PI;
			float mf = f - Mathf.Sin(f*pi2) / pi2;  // angle factor
			Vector3 p = Vector3.Lerp (start_position, end_position, mf);
			Quaternion rp = Quaternion.Slerp (start_rotation, end_rotation, mf);
			float vf = 1.0f - Mathf.Cos(f*pi2);
			paddle.move(p, rp, vf*ave_velocity, vf*ave_angular_velocity);
		}
		bool done = (time >= end_time);
		return done;
	}

	public float acceleration() {
		// Peak acceleration for linear distance and time interval
		// with sine acceleration and intial and final speed equal to zero.
		float d = (end_position - start_position).magnitude;
		float t = end_time - start_time;
		float a = (t > 0 ? 2 * Mathf.PI * d / (t * t) : 0);
		return a;
	}
}