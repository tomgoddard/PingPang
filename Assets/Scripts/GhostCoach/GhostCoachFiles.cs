using System.IO;
using UnityEngine;

// Where GhostCoach keeps its files: coach clips, the experiment
// configuration and session logs.  On the Quest this is
//   /sdcard/Android/data/se.lth.ghostcoach/files/GhostCoach/
// which can be copied with adb pull and adb push.
public static class GhostCoachFiles {

    // Tests set this so they do not touch real clips, logs or the
    // participant counter.
    public static string root_override;

    public static string root {
        get {
            return (root_override != null ? root_override
                    : Path.Combine(Application.persistentDataPath, "GhostCoach"));
        }
    }

    public static string path(string name) {
        Directory.CreateDirectory(root);
        return Path.Combine(root, name);
    }

    public static string directory(string name) {
        string dir = Path.Combine(root, name);
        Directory.CreateDirectory(dir);
        return dir;
    }
}
