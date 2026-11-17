using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

// Checks the experiment configuration, schedule and CSV log without
// playing the game.  Run from the command line with
//   Unity -batchmode -projectPath . -executeMethod ExperimentTest.run
// Exit code 0 means all checks passed.  Uses Temp/ so real files are
// not touched.
public static class ExperimentTest {

    static List<string> failures = new List<string>();

    [MenuItem("GhostCoach/Run experiment and logging test")]
    public static void run() {
        failures.Clear();
        string root = Path.GetFullPath("Temp/GhostCoachExperimentTest");
        if (Directory.Exists(root))
            Directory.Delete(root, true);
        GhostCoachFiles.root_override = root;
        try {
            check_config();
            check_schedule();
            check_state();
            check_clips();
            check_csv();
        } finally {
            GhostCoachFiles.root_override = null;
        }
        bool ok = (failures.Count == 0);
        Debug.Log("ExperimentTest " + (ok ? "PASSED" : "FAILED") + ", " + failures.Count + " failed checks");
        if (Application.isBatchMode)
            EditorApplication.Exit(ok ? 0 : 1);
    }

    static void check(bool ok, string what) {
        Debug.Log("ExperimentTest " + (ok ? "PASS " : "FAIL ") + what);
        if (!ok)
            failures.Add(what);
    }

    static void check_config() {
        ExperimentConfig c = ExperimentConfig.load_or_create();
        string path = GhostCoachFiles.path(ExperimentConfig.file_name);
        check(File.Exists(path), "default config written to " + ExperimentConfig.file_name);
        check(c.variants.Count == 2 && c.variant("A").feedback_text && !c.variant("B").feedback_text
              && c.variant("B").feedback_haptic, "default variants: A text and vibration, B vibration only");
        check(c.strokes_per_block == 20 && string.Join(" ", c.schedule) == "A B B A",
              "default schedule A B B A with 20 strokes per block");

        // An edited file is read back.
        c.strokes_per_block = 5;
        c.variant("B").mixed_reality = false;
        File.WriteAllText(path, JsonUtility.ToJson(c, true));
        ExperimentConfig edited = ExperimentConfig.load_or_create();
        check(edited.strokes_per_block == 5 && !edited.variant("B").mixed_reality, "edited config is read back");

        // A broken file falls back to the defaults instead of failing.
        File.WriteAllText(path, "{ this is not json");
        ExperimentConfig broken = ExperimentConfig.load_or_create();
        check(broken.variants.Count == 2 && broken.strokes_per_block == 20, "broken config falls back to defaults");
        check(c.variant("X").name == "A", "unknown variant name falls back to the first variant");
    }

    static void check_schedule() {
        ExperimentConfig c = ExperimentConfig.defaults();
        check(string.Join(" ", c.schedule_for(1)) == "A B B A", "participant 1 gets A B B A");
        check(string.Join(" ", c.schedule_for(2)) == "B A A B", "participant 2 gets B A A B");
        check(string.Join(" ", c.schedule_for(3)) == "A B B A", "participant 3 gets A B B A");
        c.counterbalance = false;
        check(string.Join(" ", c.schedule_for(2)) == "A B B A", "no counterbalancing keeps the schedule");
        c.counterbalance = true;
        c.schedule = new List<string> { "A", "B", "C" };
        check(string.Join(" ", c.schedule_for(2)) == "C B A", "three variants are swapped first with last");
    }

    // Accidental recordings must not become the coach.
    static void check_clips() {
        check(!ExperimentConfig.defaults().allow_recording, "recording a coach is off by default");
        check(!ExperimentConfig.defaults().show_stand_marker && ExperimentConfig.defaults().round_racket,
              "floor marker off and round racket on by default");
        string dir = MotionClip.clips_directory();
        Directory.CreateDirectory(dir);
        MotionClip good = clip(6f, true);
        good.name = "good";
        MotionClip no_strokes = clip(6f, false);
        MotionClip too_short = clip(1f, true);
        check(good.usable_as_coach && !no_strokes.usable_as_coach && !too_short.usable_as_coach,
              "a clip needs 3 s and a stroke to be usable as a coach");
        File.WriteAllText(Path.Combine(dir, "a_good.json"), JsonUtility.ToJson(good));
        File.SetLastWriteTime(Path.Combine(dir, "a_good.json"), System.DateTime.Now.AddMinutes(-10));
        File.WriteAllText(Path.Combine(dir, "b_short.json"), JsonUtility.ToJson(too_short));
        File.WriteAllText(Path.Combine(dir, "c_nostroke.json"), JsonUtility.ToJson(no_strokes));
        MotionClip loaded = MotionClip.load_latest();
        check(loaded != null && loaded.name == "good", "newer accidental clips are skipped, loaded " + (loaded == null ? "none" : loaded.name));
        File.Delete(Path.Combine(dir, "a_good.json"));
        check(MotionClip.load_latest() == null, "only accidental clips means no recorded coach");
    }

    static MotionClip clip(float seconds, bool with_stroke) {
        MotionClip c = new MotionClip();
        for (int i = 0 ; i <= (int)(seconds * 10) ; ++i) {
            MotionFrame f = new MotionFrame();
            f.t = 0.1f * i;
            c.frames.Add(f);
        }
        if (with_stroke) {
            MotionEvent e = new MotionEvent();
            e.t = 1f;
            e.type = "contact";
            c.events.Add(e);
        }
        return c;
    }

    static void check_state() {
        ExperimentState s = ExperimentState.load();
        check(s.next_participant == 1, "participant counter starts at 1");
        s.next_participant = 7;
        s.save();
        check(ExperimentState.load().next_participant == 7, "participant counter is remembered");
        s.hide_hand_visuals = true;
        s.save();
        ExperimentState again = ExperimentState.load();
        check(again.hide_hand_visuals && again.next_participant == 7, "hide racket choice is remembered next to the counter");
    }

    static void check_csv() {
        check(SessionLog.cell("Stäng racketen mer") == "Stäng racketen mer", "plain text cell unchanged");
        check(SessionLog.cell("a, b") == "\"a, b\"", "cell with comma is quoted");
        check(SessionLog.cell("say \"hi\"") == "\"say \"\"hi\"\"\"", "quotes are doubled");
        CultureInfo old = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("sv-SE");
        string half = SessionLog.cell(0.5f);
        CultureInfo.CurrentCulture = old;
        check(half == "0.5", "decimal point even with Swedish number format, got " + half);
        check(SessionLog.cell(true) == "1" && SessionLog.cell(null) == "", "bools as 1/0, null empty");

        string path = Path.Combine(GhostCoachFiles.directory("logs"), "test.csv");
        SessionLog log = new SessionLog(path);
        log.write("t", 1.5f, "P01", "s", "stroke", "x, y");
        log.close();
        string[] lines = File.ReadAllLines(path);
        check(lines.Length == 2 && lines[0].StartsWith("time,session_seconds,participant"),
              "log has a header and one row");
        check(lines.Length == 2 && lines[1].Split(',').Length == SessionLog.columns.Length + 1,
              "row has every column (the quoted comma adds one split)");
    }
}
