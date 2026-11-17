using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// A recorded motion, e.g. a coach playing a few strokes, used both for the
// coach ghost (phase 1) and the paddle path the player follows (phase 2).
//
// All poses are in table coordinates (see TableSpace) so a clip recorded
// at one real table plays back at the right place at another table after
// the virtual table has been aligned with "move table".  Clips come from
// the Quest (MotionRecorder) or later from Qualisys exports.
[Serializable]
public class MotionClip {
    public string name;
    public string source;            // "quest" or "qualisys"
    public bool left_handed;
    public string recorded;          // Date and time, ISO 8601.
    public bool has_ball = true;     // Mocap clips have no ball and no opponent.
    public bool has_opponent = true;
    // Body joints in each frame's joints array, e.g. "RightForeArm" for the
    // right elbow (humanoid joint names).  Empty for Quest recordings, which
    // only have the head and the paddle.
    public List<string> joint_names = new List<string>();


    public List<MotionFrame> frames = new List<MotionFrame>();
    public List<MotionEvent> events = new List<MotionEvent>();

    public float duration {
        get { return (frames.Count > 0 ? frames[frames.Count-1].t : 0f); }
    }

    // A recording needs some time and at least one stroke to be worth using
    // as a coach; a few accidental button presses give neither.
    public const float min_coach_seconds = 3f;

    public bool usable_as_coach {
        get { return duration >= min_coach_seconds && events.Count > 0; }
    }

    // Times of ball contacts with the paddle, seconds from clip start.
    public List<float> contact_times() {
        List<float> times = new List<float>();
        foreach (MotionEvent e in events)
            if (e.type == "contact")
                times.Add(e.t);
        return times;
    }

    // All strokes in time order: forehand contacts and the strokes marked
    // "backhand" (ambiguous or backhand).  The player picks one to repeat.
    public List<MotionEvent> strokes() {
        List<MotionEvent> list = new List<MotionEvent>();
        foreach (MotionEvent e in events)
            if (e.type == "contact" || e.type == "backhand")
                list.Add(e);
        list.Sort((a, b) => a.t.CompareTo(b.t));
        return list;
    }

    // Interpolated frame at time t, clamped to the clip.
    public MotionFrame sample(float t) {
        int n = frames.Count;
        if (n == 0)
            return null;
        if (t <= frames[0].t)
            return frames[0];
        if (t >= frames[n-1].t)
            return frames[n-1];

        // Binary search for the frames around t.
        int lo = 0, hi = n - 1;
        while (hi - lo > 1) {
            int mid = (lo + hi) / 2;
            if (frames[mid].t <= t)
                lo = mid;
            else
                hi = mid;
        }
        MotionFrame a = frames[lo], b = frames[hi];
        float f = (b.t > a.t ? (t - a.t) / (b.t - a.t) : 0f);
        return MotionFrame.lerp(a, b, f);
    }

    // Copy mirrored left to right about the plane x = center_x in table
    // coordinates, so a right handed coach becomes a left handed one.
    public MotionClip mirrored(float center_x) {
        MotionClip m = new MotionClip();
        m.name = name + "_mirrored";
        m.source = source;
        m.left_handed = !left_handed;
        m.recorded = recorded;
        m.has_ball = has_ball;
        m.has_opponent = has_opponent;
        m.joint_names = joint_names;
        foreach (MotionFrame f in frames)
            m.frames.Add(f.mirrored(center_x));
        m.events.AddRange(events);
        return m;
    }

    public static string clips_directory() {
        return Path.Combine(GhostCoachFiles.root, "clips");
    }

    public string save() {
        string dir = clips_directory();
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, name + ".json");
        File.WriteAllText(path, JsonUtility.ToJson(this));
        Debug.Log("Saved motion clip " + name + " (" + frames.Count + " frames, "
                  + duration.ToString("F1") + " s) to " + path);
        return path;
    }

    public static MotionClip load(string path) {
        if (!File.Exists(path))
            return null;
        return JsonUtility.FromJson<MotionClip>(File.ReadAllText(path));
    }

    // Clips shipped with the app in Assets/Resources/GhostCoach/clips, e.g.
    // converted from Qualisys with Tools/blender/export_coach_clip.py.
    public static MotionClip load_bundled() {
        foreach (TextAsset a in Resources.LoadAll<TextAsset>("GhostCoach/clips"))
            if (a.text.TrimStart().StartsWith("{"))
                return JsonUtility.FromJson<MotionClip>(a.text);
        return null;
    }

    // Most recently written clip that is usable as a coach (see
    // usable_as_coach), or null.  Accidental recordings are skipped.
    public static MotionClip load_latest() {
        string dir = clips_directory();
        if (!Directory.Exists(dir))
            return null;
        string[] paths = Directory.GetFiles(dir, "*.json");
        Array.Sort(paths, (a, b) => File.GetLastWriteTime(b).CompareTo(File.GetLastWriteTime(a)));
        foreach (string path in paths) {
            MotionClip c = load(path);
            if (c != null && c.usable_as_coach)
                return c;
        }
        return null;
    }
}

[Serializable]
public class MotionFrame {
    public float t;                        // Seconds from clip start.
    public Vector3 head_position;          // Table coordinates, meters.
    public Quaternion head_rotation;
    public Vector3 paddle_position;        // Paddle blade center.
    public Quaternion paddle_rotation;
    public bool ball_in_play;
    public Vector3 ball_position;
    public Vector3 robot_paddle_position;  // Opponent, so a rally can be replayed.
    public Quaternion robot_paddle_rotation;
    public Vector3[] joints;               // Body joints, see MotionClip.joint_names.

    public MotionFrame mirrored(float center_x) {
        MotionFrame m = new MotionFrame();
        m.t = t;
        m.head_position = mirror(head_position, center_x);
        m.head_rotation = mirror(head_rotation);
        m.paddle_position = mirror(paddle_position, center_x);
        m.paddle_rotation = mirror(paddle_rotation);
        m.ball_in_play = ball_in_play;
        m.ball_position = mirror(ball_position, center_x);
        m.robot_paddle_position = mirror(robot_paddle_position, center_x);
        m.robot_paddle_rotation = mirror(robot_paddle_rotation);
        if (joints != null) {
            m.joints = new Vector3[joints.Length];
            for (int i = 0 ; i < joints.Length ; ++i)
                m.joints[i] = mirror(joints[i], center_x);
        }
        return m;
    }

    static Vector3 mirror(Vector3 p, float center_x) {
        return new Vector3(2f * center_x - p.x, p.y, p.z);
    }

    // Rotation seen in a mirror across the x = 0 plane (same trick as the
    // left handed grip in Grip.hand_to_paddle_motion).
    static Quaternion mirror(Quaternion q) {
        return new Quaternion(q.x, -q.y, -q.z, q.w);
    }

    public static MotionFrame lerp(MotionFrame a, MotionFrame b, float f) {
        MotionFrame m = new MotionFrame();
        m.t = Mathf.Lerp(a.t, b.t, f);
        m.head_position = Vector3.Lerp(a.head_position, b.head_position, f);
        m.head_rotation = Quaternion.Slerp(a.head_rotation, b.head_rotation, f);
        m.paddle_position = Vector3.Lerp(a.paddle_position, b.paddle_position, f);
        m.paddle_rotation = Quaternion.Slerp(a.paddle_rotation, b.paddle_rotation, f);
        m.ball_in_play = (f < 0.5f ? a.ball_in_play : b.ball_in_play);
        m.ball_position = Vector3.Lerp(a.ball_position, b.ball_position, f);
        m.robot_paddle_position = Vector3.Lerp(a.robot_paddle_position, b.robot_paddle_position, f);
        m.robot_paddle_rotation = Quaternion.Slerp(a.robot_paddle_rotation, b.robot_paddle_rotation, f);
        if (a.joints != null && b.joints != null && a.joints.Length == b.joints.Length) {
            m.joints = new Vector3[a.joints.Length];
            for (int i = 0 ; i < m.joints.Length ; ++i)
                m.joints[i] = Vector3.Lerp(a.joints[i], b.joints[i], f);
        }
        return m;
    }
}

[Serializable]
public class MotionEvent {
    public float t;
    public string type;        // "contact" when the ball hits the paddle.
}

// Converts between world and table coordinates.  Ignores the table
// transform's scale so distances stay in meters.
public static class TableSpace {
    public static Vector3 to_table(Transform table, Vector3 world_position) {
        return Quaternion.Inverse(table.rotation) * (world_position - table.position);
    }

    public static Quaternion to_table(Transform table, Quaternion world_rotation) {
        return Quaternion.Inverse(table.rotation) * world_rotation;
    }

    public static Vector3 to_world(Transform table, Vector3 table_position) {
        return table.position + table.rotation * table_position;
    }

    public static Quaternion to_world(Transform table, Quaternion table_rotation) {
        return table.rotation * table_rotation;
    }

    // Middle of the table surface projected to the floor, and the surface
    // height, in table coordinates.
    public static Vector3 center(Table table, out float top_y) {
        Transform top = table.table_top.transform;
        Vector3 c = to_table(table.transform, top.position);
        top_y = c.y + 0.5f * top.lossyScale.y;
        c.y = 0f;
        return c;
    }
}
