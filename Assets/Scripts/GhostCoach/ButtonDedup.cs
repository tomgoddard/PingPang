using System.Collections.Generic;
using UnityEngine;

// A button press can reach the game by two routes: the Input System
// (PlayControls.inputactions) and ControllerInput, which reads the XR
// controllers directly.  Each handler asks accept() first, so a press that
// arrives by both routes counts once, whichever comes first.
public static class ButtonDedup {

    // Seconds during which the same button action is ignored after it fired.
    // Tests set it to 0 to see every press.
    public static float window = 0.15f;

    static Dictionary<string, float> last = new Dictionary<string, float>();

    // The most recent accepted actions, oldest first, for tests and debugging.
    public static List<string> log = new List<string>();

    public static bool accept(string action) {
        float now = Time.unscaledTime;
        float t;
        if (window > 0f && last.TryGetValue(action, out t) && now - t < window)
            return false;
        last[action] = now;
        log.Add(action);
        if (log.Count > 100)
            log.RemoveAt(0);
        return true;
    }
}
