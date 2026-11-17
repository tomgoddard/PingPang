using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// One version of the interaction design being compared (parallel
// design).  The comparative test switches between variants in blocks.
[Serializable]
public class Variant {
    public string name;
    public string description;
    public bool feedback_text = true;          // Score and tip on the billboard.
    public bool feedback_haptic = true;        // Score vibration in the free hand.
    public bool coach_on_paddle_side = false;  // Where the player watches the coach from.
    public float coach_behind = 0f;            // Meters behind the coach.
    public bool mixed_reality = true;          // Passthrough on, or virtual room (VR).
}

// Settings for the programmatically controlled comparative test, read from
// GhostCoach/experiment.json on the device.  If the file is missing the
// defaults below are written there, so the team can adb pull it, edit it
// and adb push it back.
[Serializable]
public class ExperimentConfig {
    public bool run_schedule = true;
    // Recording a coach with the headset (right B) is off by default: the
    // coach comes from a Qualisys take, and accidental presses would
    // otherwise replace it.  Turn on to make a clip without mocap.
    public bool allow_recording = false;
    // Look of the scene.  The floor marker only helps when telling a new
    // tester where to stand; the racket is drawn round like a real blade.
    public bool show_stand_marker = false;
    public bool round_racket = true;
    public int strokes_per_block = 20;
    public List<string> schedule = new List<string> { "A", "B", "B", "A" };
    // Every second participant gets the variants swapped (A B B A becomes
    // B A A B), so learning over the session does not favor one variant.
    public bool counterbalance = true;
    public List<Variant> variants = new List<Variant>();

    public const string file_name = "experiment.json";

    // Default comparison: feedback channel.  Text and vibration against
    // vibration only.
    public static ExperimentConfig defaults() {
        ExperimentConfig c = new ExperimentConfig();
        Variant a = new Variant();
        a.name = "A";
        a.description = "Feedback med text och vibration";
        Variant b = new Variant();
        b.name = "B";
        b.description = "Feedback med bara vibration";
        b.feedback_text = false;
        c.variants.Add(a);
        c.variants.Add(b);
        return c;
    }

    public Variant variant(string name) {
        foreach (Variant v in variants)
            if (v.name == name)
                return v;
        return (variants.Count > 0 ? variants[0] : new Variant());
    }

    public List<string> schedule_for(int participant) {
        List<string> s = new List<string>(schedule);
        if (!counterbalance || participant % 2 != 0)
            return s;
        // Swap the variants in order of first use: first with last and so on.
        List<string> names = new List<string>();
        foreach (string n in s)
            if (!names.Contains(n))
                names.Add(n);
        for (int i = 0 ; i < s.Count ; ++i)
            s[i] = names[names.Count - 1 - names.IndexOf(s[i])];
        return s;
    }

    public static ExperimentConfig load_or_create() {
        string path = GhostCoachFiles.path(file_name);
        if (File.Exists(path)) {
            try {
                ExperimentConfig c = JsonUtility.FromJson<ExperimentConfig>(File.ReadAllText(path));
                if (c != null && c.variants.Count > 0)
                    return c;
                Debug.LogWarning("GhostCoach: " + path + " has no variants, using defaults");
            } catch (Exception e) {
                Debug.LogWarning("GhostCoach: could not read " + path + ": " + e.Message);
            }
            return defaults();
        }
        ExperimentConfig d = defaults();
        File.WriteAllText(path, JsonUtility.ToJson(d, true));
        Debug.Log("GhostCoach: wrote default experiment config to " + path);
        return d;
    }
}

// Remembers the next participant number between app launches, in
// GhostCoach/state.json.
[Serializable]
public class ExperimentState {
    public int next_participant = 1;
    // The player's choice to hide the racket and ball in the hand, kept
    // between launches.
    public bool hide_hand_visuals = false;

    const string file_name = "state.json";

    public static ExperimentState load() {
        string path = GhostCoachFiles.path(file_name);
        if (File.Exists(path)) {
            try {
                ExperimentState s = JsonUtility.FromJson<ExperimentState>(File.ReadAllText(path));
                if (s != null)
                    return s;
            } catch (Exception) {
            }
        }
        return new ExperimentState();
    }

    public void save() {
        File.WriteAllText(GhostCoachFiles.path(file_name), JsonUtility.ToJson(this, true));
    }
}
