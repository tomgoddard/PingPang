using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Replay : MonoBehaviour {

	public Ball ball;
	public Hand paddle_hand;
	bool freeze = false;
	public Vector3 ball_position = Vector3.zero;
	public Vector3 paddle_position = Vector3.zero;
	public Quaternion paddle_rotation;

	public void replay() {
		freeze = !freeze;
		paddle_hand.freeze_paddle = freeze;
		ball.freeze = freeze;
		if (freeze) {
			ball.transform.position = ball_position;
			Paddle paddle = paddle_hand.held_paddle;
			paddle.transform.position = paddle_position;
			paddle.transform.rotation = paddle_rotation;
		}
	}

	public void record_positions() {
		ball_position = ball.transform.position;
		Paddle paddle = paddle_hand.held_paddle;
		paddle_position = paddle.transform.position;
		paddle_rotation = paddle.transform.rotation;
	}
}
