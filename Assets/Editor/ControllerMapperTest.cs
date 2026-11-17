using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Checks how controller buttons become game actions, with a fake controller
// instead of a headset.  Run from the command line with
//   Unity -batchmode -nographics -projectPath . -executeMethod ControllerMapperTest.run
// Exit code 0 means all checks passed.
public static class ControllerMapperTest
{

    // Also used by DesktopSmokeTest to press buttons in the real scene.
    public class Fake : IButtonSource
    {
        public bool[,] state = new bool[2, 6];
        public bool down(bool left_hand, Btn b) { return state[left_hand ? 0 : 1, (int)b]; }
        public void set(bool left_hand, Btn b, bool value) { state[left_hand ? 0 : 1, (int)b] = value; }
    }

    static List<string> failures = new List<string>();
    static List<string> fired = new List<string>();

    [MenuItem("GhostCoach/Run controller button test")]
    public static void run()
    {
        failures.Clear();
        float old_window = ButtonDedup.window;
        try
        {
            right_handed();
            left_handed();
            button_modes();
            hold_for_new_participant();
            holding_does_not_repeat();
            dedup();
        }
        finally
        {
            ButtonDedup.window = old_window;
        }
        bool ok = (failures.Count == 0);
        Debug.Log("ControllerMapperTest " + (ok ? "PASSED" : "FAILED") + ", " + failures.Count + " failed checks");
        if (Application.isBatchMode)
            EditorApplication.Exit(ok ? 0 : 1);
    }

    static void check(bool ok, string what)
    {
        Debug.Log("ControllerMapperTest " + (ok ? "PASS " : "FAIL ") + what);
        if (!ok)
            failures.Add(what);
    }

    static ControllerMapper mapper()
    {
        ControllerMapper m = new ControllerMapper();
        m.fire = a => fired.Add(a);
        return m;
    }

    // Press one button for a frame and return what it fired.
    static string press(ControllerMapper m, Fake f, bool left_handed, bool left_hand, Btn b, float t)
    {
        fired.Clear();
        f.set(left_hand, b, true);
        m.update(f, left_handed, t);
        f.set(left_hand, b, false);
        m.update(f, left_handed, t + 0.02f);
        return string.Join(",", fired);
    }

    static void right_handed()
    {
        ControllerMapper m = mapper();
        Fake f = new Fake();
        check(press(m, f, false, true, Btn.Trigger, 0f) == "RobotServe", "right handed: left trigger serves");
        check(press(m, f, false, true, Btn.Grip, 1f) == "HoldBall", "right handed: left grip takes a ball");
        check(press(m, f, false, true, Btn.Secondary, 2f) == "HoldBall", "right handed: left Y takes a ball");
        check(press(m, f, false, true, Btn.Primary, 3f) == "GhostToggleHands", "right handed: left X hides or shows racket and ball");
        check(press(m, f, false, true, Btn.Menu, 4f) == "ShowSettings", "right handed: left menu button opens the settings");
        check(press(m, f, false, false, Btn.Primary, 5f) == "GhostTogglePhase", "right handed: right A switches phase");
        check(press(m, f, false, false, Btn.Secondary, 6f) == "GhostRecord", "right handed: right B records");
        check(press(m, f, false, false, Btn.Thumbstick, 7f) == "GhostSpeed", "right handed: right thumbstick click changes speed");
        check(press(m, f, false, false, Btn.Trigger, 8f) == "ToggleBallFeed", "right handed: the paddle hand's trigger toggles ball feed");
        check(press(m, f, false, false, Btn.Menu, 9f) == "", "right handed: the right controller has no menu button");
    }

    static void left_handed()
    {
        ControllerMapper m = mapper();
        Fake f = new Fake();
        check(press(m, f, true, false, Btn.Trigger, 0f) == "RobotServe", "left handed: right trigger serves");
        check(press(m, f, true, false, Btn.Grip, 1f) == "HoldBall", "left handed: right grip takes a ball");
        check(press(m, f, true, false, Btn.Primary, 2f) == "GhostToggleHands", "left handed: right A hides or shows racket and ball");
        check(press(m, f, true, true, Btn.Primary, 3f) == "GhostTogglePhase", "left handed: left X switches phase");
        check(press(m, f, true, true, Btn.Secondary, 4f) == "GhostRecord", "left handed: left Y records");
        check(press(m, f, true, true, Btn.Thumbstick, 5f) == "GhostSpeed", "left handed: left thumbstick click changes speed");
        check(press(m, f, true, true, Btn.Menu, 6f) == "ShowSettings", "left handed: the menu button is still the left one");
        check(press(m, f, true, true, Btn.Trigger, 7f) == "ToggleBallFeed", "left handed: the paddle hand's trigger toggles ball feed");
    }

    static void button_modes()
    {
        ControllerMapper m = mapper();
        Fake f = new Fake();
        m.move_table_mode = true;
        fired.Clear();
        f.set(true, Btn.Primary, true);
        m.update(f, false, 0f);
        f.set(true, Btn.Primary, false);
        m.update(f, false, 0.5f);
        check(string.Join(",", fired) == "MoveTableStart,MoveTableEnd", "move table: X starts when pressed and ends when released: " + string.Join(",", fired));
        check(press(m, f, false, true, Btn.Trigger, 1f) == "", "move table: other buttons do nothing");
        check(press(m, f, false, false, Btn.Primary, 2f) == "", "move table: A does not switch phase");
        check(press(m, f, false, true, Btn.Menu, 3f) == "ShowSettings", "move table: the menu button still works");

        m.move_table_mode = false;
        m.adjust_grip_mode = true;
        fired.Clear();
        f.set(true, Btn.Primary, true);
        m.update(f, false, 4f);
        f.set(true, Btn.Primary, false);
        m.update(f, false, 4.5f);
        check(string.Join(",", fired) == "AdjustGripStart,AdjustGripEnd", "adjust grip: X starts and ends: " + string.Join(",", fired));
        m.adjust_grip_mode = false;
        check(press(m, f, false, true, Btn.Trigger, 5f) == "RobotServe", "back to normal after the modes");
    }

    static void hold_for_new_participant()
    {
        ControllerMapper m = mapper();
        Fake f = new Fake();
        fired.Clear();
        f.set(true, Btn.Thumbstick, true);
        for (float t = 0f; t <= 1.4f; t += 0.1f)
            m.update(f, false, t);
        check(fired.Count == 0, "a short hold of the thumbstick does nothing");
        for (float t = 1.5f; t <= 3f; t += 0.1f)
            m.update(f, false, t);
        check(string.Join(",", fired) == "GhostNewSession", "holding 1.5 s starts a new participant, once: " + string.Join(",", fired));
        f.set(true, Btn.Thumbstick, false);
        m.update(f, false, 3.2f);
        fired.Clear();
        f.set(true, Btn.Thumbstick, true);
        for (float t = 4f; t <= 6f; t += 0.1f)
            m.update(f, false, t);
        check(string.Join(",", fired) == "GhostNewSession", "a new hold works again after letting go");
    }

    static void holding_does_not_repeat()
    {
        ControllerMapper m = mapper();
        Fake f = new Fake();
        fired.Clear();
        f.set(true, Btn.Trigger, true);
        for (int i = 0; i < 30; ++i)
            m.update(f, false, 0.02f * i);
        check(fired.Count == 1, "holding a button fires once, not every frame (" + fired.Count + ")");
        f.set(true, Btn.Trigger, false);
        m.update(f, false, 1f);
        f.set(true, Btn.Trigger, true);
        m.update(f, false, 1.1f);
        check(fired.Count == 2, "pressing again fires again");
    }

    static void dedup()
    {
        ButtonDedup.window = 0.15f;
        ButtonDedup.log.Clear();
        // Time.unscaledTime does not move inside this call, so a second press
        // within the window is the same press arriving by the other route.
        bool first = ButtonDedup.accept("ControllerMapperTestAction");
        bool second = ButtonDedup.accept("ControllerMapperTestAction");
        bool other = ButtonDedup.accept("ControllerMapperTestOther");
        check(first && !second && other, "a press that arrives by both routes counts once");
        ButtonDedup.window = 0f;
        check(ButtonDedup.accept("ControllerMapperTestAction") && ButtonDedup.accept("ControllerMapperTestAction"),
              "with the window at 0 every press counts (used by the tests)");
    }
}
