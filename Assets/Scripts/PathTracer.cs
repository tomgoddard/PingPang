using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PathTracer : MonoBehaviour {

    List<TracePoint> player_paddle_track = new List<TracePoint>();
    List<TracePoint> ball_track = new List<TracePoint>();

    // Track is divided into segments.
    // Segment dividers given by indices into track lists.
    List<int> segments = new List<int>();
    int segment = -1;  // Current segment shown
    
    public GameObject paddle_template;
    public GameObject ball_template;

    bool tracking = false;

    float spin_bands_per_turn = 20;
    float spin_band_width = 0.01f;	// meters
    float spin_band_radius = 0.02f;	// meters
    float full_turn_spin = 5f;		// meters/second
    float spiral_out_per_turn = 0.4f;	// fraction of initial radius

    /*
    float spin_band_width = 0.1f;	// meters
    float spin_band_radius = 0.2f;	// meters
    */
    
    /*
    float start_track_speed = 2.0f;
    float end_track_speed = 1.0f;
    float start_angular_speed = 100.0f;
    float end_angular_speed = 50.0f;
    */

    public void clear_tracking() {
	player_paddle_track.Clear();
	ball_track.Clear();
	segments.Clear();
	segment = -1;
	tracking = false;
	set_mesh(false, false);
    }
    
    public void start_tracking() {
	player_paddle_track.Clear();
	ball_track.Clear();
	segments.Clear();
	segment = -1;
	show_tracks(false, false);
	tracking = true;
    }

    public bool is_tracking() {
	return tracking;
    }

    public void end_tracking() {
	if (!tracking)
	    return;
	tracking = false;
    }

    public void show_tracks(bool show_ball_tracks, bool show_stroke_tracks) {
	set_mesh(show_ball_tracks, show_stroke_tracks);
    }

    public void show_next_track() {
	if (segments.Count == 0)
	    segment = -1;
	else if (segment == -1)
	    segment = 0;
	else if (segment+1 <= segments.Count)
	    segment += 1;
	else {
	    segment = -1;
	    set_mesh(false, false);
	    return;
	}
	set_mesh(true, true);
    }

    public void show_previous_track() {
	if (segments.Count == 0)
	    segment = -1;
	else if (segment == -1)
	    segment = segments.Count;
	else if (segment > 0)
	    segment -= 1;
	else {
	    segment = -1;
	    set_mesh(false, false);
	    return;
	}
	set_mesh(true, true);
    }

    public bool shown() {
	Mesh mesh = GetComponent<MeshFilter>().mesh;
	return mesh.triangles.Length > 0;
    }
    
    public void new_position(Paddle player_paddle, Ball ball) {

	if (!tracking || ball == null)
	    return;

	player_paddle_track.Add(new TracePoint(player_paddle));
	ball_track.Add(new TracePoint(ball));
    }

    public void add_segment() {
	segments.Add(ball_track.Count);
    }
    
    void set_mesh(bool show_ball_tracks, bool show_stroke_tracks) {

	List<CombineInstance> instances = new List<CombineInstance>();

	List<TracePoint> btrack, ptrack;
	if (segment == -1) {
	    btrack = ball_track;
	    ptrack = player_paddle_track;
	} else {
	    int start = (segment == 0 ? 0 : segments[segment-1]);
	    int end = (segment < segments.Count ?
		       segments[segment] : ball_track.Count);
	    int count = end-start;
	    btrack = ball_track.GetRange(start, count);
	    ptrack = player_paddle_track.GetRange(start, count);
	}
	
	Component [] ptemplates = paddle_template.GetComponentsInChildren<MeshFilter>();
	if (show_stroke_tracks) {
	    List<TracePoint> pctrack = paddle_close_to_ball(ptrack, btrack);
	    add_mesh_instances(ptemplates, pctrack, instances);
	}

	Component [] btemplates = ball_template.GetComponentsInChildren<MeshFilter>();
	if (show_ball_tracks) {
	    add_mesh_instances(btemplates, btrack, instances);
	    add_spin_indicators(spin_points(btrack), instances);
	}
	
	CombineInstance [] iarray = instances.ToArray();
	Mesh mesh = GetComponent<MeshFilter>().mesh;
	mesh.CombineMeshes(iarray);
	//
	// Caution.  If combined mesh is more than 64K triangles then the rendering
	// is incorrect joining the wrong vertices.
	//

	/*
	for (int p = 0 ; p < positions.Count ; ++p)
	    Debug.Log("Accel " + p + " " + accel[p] + " mag " + accel[p].magnitude);
	*/
    }

    List<TracePoint> paddle_close_to_ball(List<TracePoint> ptrack,
					  List<TracePoint> btrack) {
	List<TracePoint> track = new List<TracePoint>();
	int ntimes = ptrack.Count;
	float dmin = 10f;
	int tmin = 0;
	for (int t = 0 ; t < ntimes ; ++t)
	{
	    float d = Vector3.Distance(ptrack[t].position,
				       btrack[t].position);
	    if (d < dmin)
	    {
		dmin = d;
		tmin = t;
	    }
	    if (d < 0.20 ||
		(t > 0 && (ptrack[t].position - ptrack[t-1].position).magnitude > 0.04))
		track.Add(ptrack[t]);
		
	}
//	track.Add(ptrack[tmin]);
	return track;
    }

    void add_mesh_instances(Component [] templates,
			    List<TracePoint> points,
			    List<CombineInstance> instances)
    {
	Vector3 no_scale = new Vector3(1,1,1);
	foreach (MeshFilter mf in templates)
	{
	    Transform mt = mf.gameObject.transform;
	    Matrix4x4 tm = Matrix4x4.TRS(mt.localPosition, mt.localRotation, mt.localScale);
	    foreach (TracePoint p in points)
	    {
		Matrix4x4 m = Matrix4x4.TRS(p.position, p.rotation, no_scale) * tm;
		CombineInstance instance = new CombineInstance();
		instance.mesh = mf.mesh;
		instance.transform = m;
		instances.Add(instance);
	    }
	}
    }

    List<TracePoint> spin_points(List<TracePoint> btrack) {
	// Choose ball points half way between points where
	// spin changes.  Using ball points right after spin
	// changes puts spin indicator right next to bounce
	// surface which is hard to see.
	List<TracePoint> track = new List<TracePoint>();
	Vector3 prev_angular_velocity = new Vector3(0,0,0);
	int prev_p = 0;
	for (int p = 0 ; p < btrack.Count ; ++p)
	{
	    Vector3 w = btrack[p].angular_velocity;
	    if (Vector3.Distance(w, prev_angular_velocity) > 0.1f)
	    {
		if (prev_p > 0)
		    track.Add(btrack[prev_p + (p-prev_p)/2]);
		prev_p = p;
	    }
	    prev_angular_velocity = w;
	}
	if (prev_p > 0)
	    track.Add(btrack[prev_p + (btrack.Count-prev_p)/2]);
	return track;
    }

    void add_spin_indicators(List<TracePoint> points, List<CombineInstance> instances)
    {
	foreach (TracePoint p in points)
	{
	    CombineInstance instance = new CombineInstance();
	    instance.mesh = spin_indicator(p);
	    instance.transform = Matrix4x4.identity;
	    instances.Add(instance);
	}
    }

    Mesh spin_indicator(TracePoint point) {

	float ball_radius = 0.02f;
	float spin = point.angular_velocity.magnitude * ball_radius;
	float angle = 2 * Mathf.PI * spin / full_turn_spin;
	int div = (int) (spin_bands_per_turn * angle / (2*Mathf.PI));
	if (div == 0)
	    div = 1;

	// Need front and backfacinging triangles since Unity culls back facing.
	int nv = 4 * (div + 1);
	int bv = nv/2;
	int nt = 4 * div;
	Vector3 [] vertices = new Vector3[nv];
	int [] triangles = new int[3*nt];
	float w2 = -0.5f*spin_band_width;
	float r0 = spin_band_radius;
	float rf = spiral_out_per_turn * spin_band_radius / (2*Mathf.PI);
	
	for (int v = 0 ; v <= div ; ++v)
	{
	    float a = angle * (v == div-1 && v >= 1 ? v-1 : v) /div;
	    ref Vector3 v0 = ref vertices[v], v1 = ref vertices[v+div+1];
	    float r = r0 + rf*a;
	    v0.x = v1.x = r*Mathf.Cos(a);
	    v0.y = v1.y = r*Mathf.Sin(a);
	    float w = (v < div-1 ? w2 : (v == div ? 0 : 2*w2));
	    v0.z = -w;
	    v1.z = w;
	    
	    // Backfacing.
	    vertices[v + bv] = v0;
	    vertices[v+div+1 + bv] = v1;
	}

	for (int t = 0 ; t < div ; ++t)
	{
	    // 4 triangles per band.
	    int b = 12*t;
	    int v0 = t, v1 = t+1, v2 = t+div+1, v3 = t+1+div+1;
	    triangles[b] = v0; triangles[b+1] = v1; triangles[b+2] = v3;
	    triangles[b+3] = v0; triangles[b+4] = v3; triangles[b+5] = v2;
	    // Backfacing.
	    triangles[b+6] = bv+v0; triangles[b+7] = bv+v3; triangles[b+8] = bv+v1;
	    triangles[b+9] = bv+v0; triangles[b+10] = bv+v2; triangles[b+11] = bv+v3;
	}

	Vector3 offset = point.position;
	Vector3 axis = point.angular_velocity.normalized;
	Quaternion rot = Quaternion.FromToRotation(new Vector3(0,0,1), axis);
	for (int v = 0 ; v < nv ; ++v)
	    vertices[v] = offset + rot * vertices[v];
	
	Mesh m = new Mesh();
	m.vertices = vertices;
	m.triangles = triangles;
	m.RecalculateNormals();

	return m;
    }
    
}

class TracePoint
{
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 angular_velocity;
    
    public TracePoint(Paddle p)
    {
	position = p.transform.position;
	rotation = p.transform.rotation;
    }

    public TracePoint(Ball b)
    {
	position = b.transform.position;
	rotation = b.transform.rotation;
	angular_velocity = b.motion.angular_velocity;
    }
}
