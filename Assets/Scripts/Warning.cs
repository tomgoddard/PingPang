using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Warning : MonoBehaviour {
	public TextMesh poster;
	float clear_time = 0f;
	float clear_delay = 5f;  // Seconds before warning message cleared.

	// Update is called once per frame
	void Update () {
		if (clear_time > 0 && Time.time >= clear_time) {
			clear_time = 0;
			poster.text = "";
		}
	}

	public void warn(string message) {
		poster.text = message;
		clear_time = Time.time + clear_delay;
		Debug.Log (message);
	}
}
