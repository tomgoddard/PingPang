using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PaddleTrack : MonoBehaviour {

    List<Vector3> positions = new List<Vector3>();
    List<Quaternion> rotations = new List<Quaternion>();
    List<Vector3> accel = new List<Vector3>();
    float start_track_speed = 2.0f;
    float end_track_speed = 1.0f;
    float start_angular_speed = 100.0f;
    float end_angular_speed = 50.0f;

    public void new_paddle_position(Vector3 position, Quaternion rotation,
				    Vector3 velocity, Vector3 angular_velocity,
				    Vector3 hand_accel) {

	if (!gameObject.activeSelf)
	    return;
	    
	bool started = (positions.Count > 0);
	if ((!started && (velocity.magnitude >= start_track_speed
			  || angular_velocity.magnitude >= start_angular_speed))
	    || (started && (velocity.magnitude >= end_track_speed
			    || angular_velocity.magnitude >= end_angular_speed))
	    )
	{
	    positions.Add(position);
	    rotations.Add(rotation);
	    accel.Add(hand_accel);
	}
	else if (positions.Count > 0)
	{
	    set_mesh();
	    positions.Clear();
	    rotations.Clear();
	    accel.Clear();
	}
    }

    void set_mesh() {
	Mesh mesh = GetComponent<MeshFilter>().mesh;
	Component [] templates = GetComponentsInChildren<MeshFilter>();

	// For some reason templates contains the mesh filter of the
	// paddle tracker game object in addition to its children.
	int n = positions.Count * (templates.Length - 1);
	CombineInstance [] instances = new CombineInstance [n];
	int i = 0;
	Vector3 no_scale = new Vector3(1,1,1);
	foreach (MeshFilter mf in templates)
	{
	    if (mf.mesh == mesh)
		continue;
	    Transform t = mf.gameObject.transform;
	    Matrix4x4 tm = Matrix4x4.TRS(t.localPosition, t.localRotation, t.localScale);
	    for (int p = 0 ; p < positions.Count ; ++p)
	    {
		instances[i].mesh = mf.mesh;
		Matrix4x4 m = Matrix4x4.TRS(positions[p], rotations[p], no_scale) * tm;
		instances[i].transform = m;
		i += 1;
	    }
	}

	mesh.CombineMeshes(instances);

	/*
	for (int p = 0 ; p < positions.Count ; ++p)
	    Debug.Log("Accel " + p + " " + accel[p] + " mag " + accel[p].magnitude);
	*/
    }
    
}
