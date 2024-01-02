using System;  // use IntPtr
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;  // Use Marshal
using UnityEngine;
using UnityEngine.XR;  // Use InputDevice

public class Wand : MonoBehaviour {

//    public SteamVR_Behaviour_Pose controller;
    public bool right, left;
    public Transform tracking_space;  // Coordinate system for local hand controller positions.
    public float latency = 0f;  // Time in seconds that controller lags frame redraw.
    // Display warning message if controller maximum acceleration exceeded.
    public Warning warning;      // For displaying warning messages.
    float last_hs = 0f;  // Last hand speed, for debugging
    float last_ac = 0f;   // Last accel magnitude, for debugging
//    uint last_frame_index = 0;
//    double last_frame_time = 0;
//    ulong imu_ring_buffer = 0;  // For getting IMU acceleration values
//    uint imu_struct_size = 0;
    
//    TrackedDevicePose_t[] render_poses = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
//    TrackedDevicePose_t[] game_poses = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
//    Compositor_FrameTiming timing = new Compositor_FrameTiming();
    Vector3 prev_hv;
    Vector3 prev_pos = new Vector3();
    Quaternion prev_rot = new Quaternion();

    InputDevice device;
    
    public int device_index() {
	// Index into pose array
//	return controller.GetDeviceIndex();
	return -1;
    }

    bool have_device() {
	var devices = new List<InputDevice>();
	XRNode hand = (left? XRNode.LeftHand : XRNode.RightHand);
	InputDevices.GetDevicesAtXRNode(hand, devices);

	if(devices.Count == 1)
	{
	    // Debug.Log(string.Format("Device name '{0}' with role '{1}'", device.name, device.role.ToString()));
	    device = devices[0];
	    return true;
	}
	else if (devices.Count > 1)
	{
	    Debug.Log("Found more than one XR input device!");
	}
	return false;
    }
    
    // Lighthouse device serial number, e.g. "LHR-5D7C5B12"
    /*
    string serial_number() {
	uint id = (uint) device_index();
	var error = ETrackedPropertyError.TrackedProp_Success;
	var prop = ETrackedDeviceProperty.Prop_SerialNumber_String;
	var system = OpenVR.System;
	uint capacity = 128;
	var result = new System.Text.StringBuilder((int)capacity);
	system.GetStringTrackedDeviceProperty(id, prop, result, capacity, ref error);
	if (error == ETrackedPropertyError.TrackedProp_Success)
	    return result.ToString();
	return "";
    }
    */
    
    public void wand_motion(out Vector3 position, out Quaternion rotation,
        out Vector3 velocity, out Vector3 angular_velocity,
        out Vector3 acceleration) {

	position = velocity = angular_velocity = acceleration = Vector3.zero;
	rotation = Quaternion.identity;

	if (!have_device())
	    return;

	Vector3 p, v, a, av;
	Quaternion r;
	if (!device.TryGetFeatureValue(CommonUsages.devicePosition, out p))
	    return;
	if (!device.TryGetFeatureValue(CommonUsages.deviceRotation, out r))
	    return;
	if (!device.TryGetFeatureValue(CommonUsages.deviceVelocity, out v))
	    return;
	if (!device.TryGetFeatureValue(CommonUsages.deviceAngularVelocity, out av))
	    return;
	if (!device.TryGetFeatureValue(CommonUsages.deviceAcceleration, out a))
	    return;
	
#if old_steamvr
        int i = device_index();
        var compositor = OpenVR.Compositor;
        if (i == -1 || compositor == null) {
            position = velocity = angular_velocity = acceleration = Vector3.zero;
            rotation = Quaternion.identity;
	    // Debug.Log("No wand index " + i + " valid " + controller.isValid + " active " + controller.isActive);
            return;
        }
	
	//
        // GetLastPoses() returns the poses produced by WaitGetPoses().
	// These Unity SteamVR plugin calls are not the same as the
	// Valve SteamVR library calls of the same name, since the Unity
	// versions return two arrays of poses while the Valve calls return
	// only one array.  I couldn't find any documentation explaining
	// the two arrays.  But here is what I think is going on.  A comment
	// in the Unity SteamVR code says WaitGetPoses() is in the Unity
	// render thread.  I guess it gets called before rendering but
	// after all the Unity component Update() methods are called.
	// This would give it the most accurate poses for rendering.
	// The next call to Update will then want the predicted poses for
	// the following render frame, and I guess that game_poses are
	// those predicted poses for the following frame.
	//
	// This post
	// https://steamcommunity.com/app/358720/discussions/0/351659808493203424/?l=german
	// says render poses are predicted for the next rendered frame
	// which I believe already completed its Update() call.
	// Game poses is one frame beyond that, ie the predicted poses
	// for the render after the current Update() call completes.
	// The predictions include a full frame delay after vsync for
	// the photons to appear on the display.
	//
        compositor.GetLastPoses(render_poses, game_poses);

	/*
	timing.m_nSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(Compositor_FrameTiming));
	if (compositor.GetFrameTiming(ref timing, 0))
	{
	    uint fi = timing.m_nFrameIndex;
	    if (fi > last_frame_index+1)
		Debug.Log("Dropped a frame " + (fi - last_frame_index));
	    last_frame_index = fi;
	    double ft = timing.m_flSystemTimeInSeconds;
//	    if (ft - last_frame_time > 1.2/90)
//		Debug.Log("Long time step " + 1000*(ft - last_frame_time) + " msec");
	    last_frame_time = ft;
//	    if (fi % 300 == 0)
//		Debug.Log("Got frame " + fi + " time " + timing.m_flSystemTimeInSeconds);
	}
	*/

        var pose = game_poses [i];
        // TODO: Check if poses are valid using pose.bPoseIsValid
        var t = new SteamVR_Utils.RigidTransform(pose.mDeviceToAbsoluteTracking);
        Vector3 hp = t.pos;
        Quaternion hr = t.rot;

        // Sign changes for velocity are copied form SteamVR_Controller.Device class.
        Vector3 hv = new Vector3(pose.vVelocity.v0, pose.vVelocity.v1, -pose.vVelocity.v2);
#endif
	
        Vector3 hp = p;
        Quaternion hr = r;
        // Sign changes for velocity are copied form SteamVR_Controller.Device class.
//        Vector3 hv = new Vector3(v.x, v.y, -v.z);
	Vector3 hv = v;

	/*
	  TODO: Need to handle skipped frames where time step will be
	  twice this amount.  And this wand motion routine should return
	  the time step since the last wand motion so that the ball can
	  be advanced by the right amount.  Advancing ball by only half
	  the amount can cause double strikes.
	 */
        float time_step = 1.0f / 90.0f;
	if (hv.magnitude > 2f)
	    //hv = 2*(hp - prev_pos) / time_step - prev_hv; // Unstable
	    hv = (hp - prev_pos) / time_step;
//        Vector3 hav = new Vector3 (-pose.vAngularVelocity.v0, -pose.vAngularVelocity.v1, pose.vAngularVelocity.v2);
	// Vector3 hav = new Vector3 (-av.x, -av.y, av.z);

	// Empirical tests with Valve Index controllers show
	// reported angular acceleration is wrong by about a 135 degree
	// rotation about y-axis.  Oct 2, 2022.
	Vector3 hav = Quaternion.AngleAxis(135.0f, new Vector3(0,1,0)) * av;
	
	if (av.magnitude > 12f && false)
	{
	    Quaternion rdelta = r * Quaternion.Inverse(prev_rot);
	    float angle = 0.0f;
	    Vector3 axis = Vector3.zero;
	    rdelta.ToAngleAxis(out angle, out axis);
	    float fps = 90.0f;
	    float s = fps * angle * Mathf.PI / 180.0f;
	    Vector3 nav = Quaternion.AngleAxis(135.0f, new Vector3(0,1,0)) * av;
	    Debug.Log("Angular vel " + nav.x + ", " + nav.y + ", " + nav.z +
		      " last rot " + s*axis.x + ", " + s*axis.y + ", " + s*axis.z);
	    /* Angular velocity with Valve Index seems right for rotations around y axis (vertical), but wrong around x,z and the wrong x,z depends on paddle orientation. Ugh. */
	}
	prev_rot = r;

        // Approximate acceleration from two velocity measurements.
        // With Samsung Odyssey Windows Mixed Reality headset and SteamVR
        // the previous velocity and next velocity are always identical (Dec 31, 2018).
        //var prev_pose = poses [i];
        //Vector3 prev_hv = new Vector3(prev_pose.vVelocity.v0, prev_pose.vVelocity.v1, -prev_pose.vVelocity.v2);
        // TODO: Don't hard code time step.
        Vector3 ha = (hv - prev_hv) / time_step;
        prev_hv = hv;

	Vector3 vpred = (hp - prev_pos)/time_step;
	prev_pos = hp;
	/*
	if (hv.magnitude > 2 && vpred.magnitude > 1.5*hv.magnitude)
	    Debug.Log("Wand motion is larger than wand velocity for one time step " + (vpred.magnitude / hv.magnitude)
		      + " vmove " + vpred + " vrep " + hv
		      + " cos angle " + Vector3.Dot(vpred.normalized, hv.normalized));
	*/
	/*
	Vector3 hvave = 0.5f * (hv + prev_hv);
	if ((hvave - vpred).magnitude > 1.0f)
	    Debug.Log("Wand motion differs from wand velocity prediction " + (vpred.magnitude / hv.magnitude)
		      + " vmove " + vpred + " vtrack " + hvave
		      + " cos angle " + Vector3.Dot(vpred.normalized, hvave.normalized));
	*/
	
        // Transform from tracking_space coordinates (camera) to world coordinates.
        position = tracking_space.TransformPoint (hp);
        rotation = tracking_space.rotation * hr;
        velocity = tracking_space.TransformVector (hv);
        angular_velocity = tracking_space.TransformVector (hav);
        acceleration = tracking_space.TransformVector (ha);
    }

    public Vector3 position() {
        Vector3 p, v, av, a;
        Quaternion r;
        wand_motion(out p, out r, out v, out av, out a);
	return p;
    }

    /*
    public void wand_motion1(out Vector3 position, out Quaternion rotation,
                            out Vector3 velocity, out Vector3 angular_velocity,
                            out Vector3 acceleration) {
        // Object position is in hand coordinates.
        // Outputs are in world coordinates.
        var dev = device();
        if (dev == null) {
            position = velocity = angular_velocity = acceleration = Vector3.zero;
            rotation = Quaternion.identity;
            return;
        }
        dev.Update();
        //Vector3 hp = dev.transform.pos;
        //Quaternion hr = dev.transform.rot;
        Vector3 hp = hand.transform.localPosition;
        Quaternion hr = hand.transform.localRotation;
        Vector3 hv = dev.velocity;
        //Vector3 ha = device.acceleration;
        Vector3 ha = Vector3.zero; // TODO: SteamVR does not have acceleration.
        Vector3 hav = dev.angularVelocity;
        // TODO: Report bug in OVRInput angular velocity is not in tracking frame.
        //Vector3 hav = hr * hav1;

        // Issue warning if hand velocity and accel zero which happens
        // when 16g accleration exceeded with oculus touch.
        //check_imu_out_of_range (hv, ha);

        // Transform from tracking_space coordinates to world coordinates.
        position = tracking_space.TransformPoint (hp);
        rotation = tracking_space.rotation * hr;
        velocity = tracking_space.TransformVector (hv);
        angular_velocity = tracking_space.TransformVector (hav);
        acceleration = tracking_space.TransformVector (ha);
    }
    */
    
    public void object_motion(Vector3 object_position, out Vector3 position, out Quaternion rotation,
                                out Vector3 velocity, out Vector3 acceleration) {
        // Object position is in hand coordinates.
        // Outputs are in world coordinates.
        Vector3 p, va;
        Quaternion r;
        wand_motion(out p, out r, out velocity, out va, out acceleration);
        position = p + r * object_position;
        rotation = r;
    }

    // Returned hand position, rotation, velocity, angular velocity and
    // acceleration are in tracking_space coordinates.
    public void predict_hand_motion(out Vector3 hp, out Quaternion hr,
                                    out Vector3 hv, out Vector3 hav, out Vector3 ha) {
        wand_motion (out hp, out hr, out hv, out hav, out ha);

        // Predict controller position at the next frame draw time.
        // Unfortunately the Unity Oculus 1.18.1 API does not provide a way to predict
        // the pose at the next redraw, so I have to compute it myself.
        if (latency != 0) {
            // Extrapolate hand position.
            float t = latency;
            hp += t * hv + (0.5f * t * t) * ha;
            float ad = Mathf.Rad2Deg * t * hav.magnitude;
            Vector3 axis = hav.normalized;
            hr = Quaternion.AngleAxis (ad, axis) * hr;
            // TODO: Predict angular velocity using angular acceleration.
        }
    }

    // Look for hand velocity that happens when Oculus Touch controller inertial measurement
    // unit hardware exceeds 16g acceleration limit.
    void check_imu_out_of_range(Vector3 hv, Vector3 ha) {
        if (hv.magnitude == 0 && last_hs >= 5) {
            warning.warn ("Acceleration out of range, last hand speed " + last_hs + " and accel " + last_ac);
        }
        last_hs = hv.magnitude;
        last_ac = ha.magnitude;
    }

    // This imu reading code is derived from
    //
    //  https://github.com/ValveSoftware/openvr/wiki/IVRIOBuffer
    //
    // Device serial numbers are shown in the SteamVR log:
    //
    //    /cygdrive/C/Program Files (x86)/Steam/logs/vrserver.txt
    //
    // Tracker config including firmware parameters
    //
    //     /cygdrive/c/Program Files (x86)/Steam/config/lighthouse/lhr-5d7c5b12/config.json
    /*
    public bool imu_state(ref double time, ref Vector3 accel, ref Vector3 rot,
			  ref int out_of_range)
    {
	if (imu_ring_buffer == 0)
	{
	    // initialization
	    CVRIOBuffer b = OpenVR.IOBuffer;
	    ImuSample_t sample = new ImuSample_t();
	    imu_struct_size = (uint) Marshal.SizeOf(sample);
	    string serial = serial_number();
	    if (serial == "")
		return false;
	    string device = "/devices/lighthouse/" + serial + "/imu";
	    EIOBufferError e = b.Open( device,
				       EIOBufferMode.Read,
				       imu_struct_size, 10,
				       ref imu_ring_buffer );
	    if (e != EIOBufferError.IOBuffer_Success) {
		Debug.Log("Error opening IMU device: " + e);
		return false;
	    }
	}
		    
        IntPtr buf = Marshal.AllocHGlobal((int) imu_struct_size);
        uint buf_bytes = imu_struct_size;
	uint read = 0;

	double [] db = new double[7];
	int count = 0;
	int offscale = 0;
	while (OpenVR.IOBuffer.Read(imu_ring_buffer, buf, buf_bytes, ref read)
               == EIOBufferError.IOBuffer_Success && read == buf_bytes)
	{
	    count += 1;
	    offscale = Marshal.ReadInt32(buf, 56);
	    Marshal.Copy(buf, db, 0, 7);
	}
        Marshal.FreeHGlobal(buf);
	// b.Close( ulIMUStream );
	
	if (count == 0)
	    return false;

	time = db[0];
	accel.Set((float)db[1], (float)db[2], (float)db[3]);
	rot.Set((float)db[4], (float)db[5], (float)db[6]);
	out_of_range = offscale;

	return true;
    }
    */
    
    public void haptic_pulse(float duration, float strength) {
    }
    /*
        var dev = device ();
        if (dev == null)
            return;
        StartCoroutine (haptic_pulses (dev, duration, strength));
        //dev.TriggerHapticPulse (durationMicroSeconds);
    }
    IEnumerator haptic_pulses(SteamVR_Controller.Device device, float duration, float strength) {
        ushort pulse_microsec = (ushort) (3999 * strength); // Max pulse duration is 4 milliseconds.
        for (float t = 0; t < duration; t += Time.deltaTime) {
            device.TriggerHapticPulse (pulse_microsec);
            yield return new WaitForSeconds(0.005f);  // Can only trigger pulse every 5 msec according to SteamVR docs.
        }
    }
    */
}
