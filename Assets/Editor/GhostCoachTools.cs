using System.IO;
using UnityEditor;
using UnityEngine;

// Menu items for the files GhostCoach saves on this computer while playing
// in the editor: coach clips, session logs, the experiment configuration
// and the participant counter.  On the headset they are in
// /sdcard/Android/data/se.lth.ghostcoach/files/GhostCoach/.
public static class GhostCoachTools {

    [MenuItem("GhostCoach/Open saved data folder")]
    public static void open_folder() {
        Directory.CreateDirectory(GhostCoachFiles.root);
        EditorUtility.RevealInFinder(GhostCoachFiles.root);
    }

    [MenuItem("GhostCoach/Delete recorded coach clips...")]
    public static void delete_clips() {
        string dir = MotionClip.clips_directory();
        string[] files = (Directory.Exists(dir) ? Directory.GetFiles(dir, "*.json") : new string[0]);
        if (files.Length == 0) {
            EditorUtility.DisplayDialog("GhostCoach", "There are no recorded coach clips.", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("Delete recorded coach clips",
                "Delete " + files.Length + " coach clips recorded in the editor?\n\n" + dir
                + "\n\nThe Qualisys coach shipped with the project is not touched.",
                "Delete", "Cancel"))
            return;
        foreach (string f in files)
            File.Delete(f);
        Debug.Log("GhostCoach: deleted " + files.Length + " recorded coach clips");
    }

    [MenuItem("GhostCoach/Reset participant counter...")]
    public static void reset_participants() {
        if (!EditorUtility.DisplayDialog("Reset participant counter",
                "The next participant will be P01 again.  Existing logs are kept, but a new "
                + "P01 log has a different date so it does not overwrite the old one.",
                "Reset", "Cancel"))
            return;
        ExperimentState state = new ExperimentState();
        state.save();
        Debug.Log("GhostCoach: participant counter reset");
    }
}
