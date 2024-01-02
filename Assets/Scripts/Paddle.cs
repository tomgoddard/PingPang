using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Paddle : MonoBehaviour {

    public GameObject blade;

    public Bouncer forehand_rubber() {
	foreach (Bouncer bn in GetComponentsInChildren<Bouncer> ())
	    if (bn.z_bottom)
		return bn;
	return null;
    }

    public Vector3 position
    {
	get { return transform.position; }
    }

    public Vector3 forehand_normal() {
	return transform.TransformVector (new Vector3 (0, 0, -1)).normalized;
    }

    public void move(Vector3 position, Quaternion rotation,
		     Vector3 velocity, Vector3 angular_velocity) {
	transform.position = position;
	transform.rotation = rotation;

	// Set paddle surface velocities used in bounce calculations done in Balls.LateUpdate().
	foreach (Bouncer bc in gameObject.GetComponentsInChildren<Bouncer> ())
	    bc.move_wall (velocity, angular_velocity);
    }

    public float width
    {
	get { return blade.transform.localScale.x; }
    }

    // Center of paddle to surface of forehand rubber times 2.
    public float thickness
    {
	get
	{
	    Transform frt = forehand_rubber().transform;
	    float h = Mathf.Abs(frt.localPosition.z) + 0.5f * frt.localScale.z;
	    return 2f*h;
	}
    }
}
