using System;

public enum Btn { Primary = 0, Secondary, Grip, Trigger, Thumbstick, Menu }

// Where button states come from: the XR controllers in the game, a fake in tests.
public interface IButtonSource {
    bool down(bool left_hand, Btn button);
}

// Turns raw button states into game actions.  Pure logic, so it can be
// tested without a scene or a headset.  The mapping is the one in
// PlayControls.inputactions: for a right handed player the free hand is the
// left one (trigger serves, grip or Y takes a ball, X shows or hides the
// racket and ball, menu button opens the settings, holding the thumbstick
// in starts a new participant) and the paddle hand is the right one (A
// switches phase, B records, thumbstick click changes the coach's speed).
// For a left handed player the hands swap, except the menu button, which
// only the left controller has.  While the settings' "move table" or "adjust
// grip" mode is on, X starts and ends it, and the other buttons do nothing,
// like the Input System action maps.
public class ControllerMapper {

    public Action<string> fire;
    public bool move_table_mode, adjust_grip_mode;
    public float hold_seconds = 1.5f;

    const int hands = 2, buttons = 6;       // Hand index 0 is left, 1 is right.
    bool[,] was = new bool[hands, buttons];
    float hold_start = -1f;
    bool hold_fired = false;

    public void update(IButtonSource source, bool left_handed, float now) {
        int[,] edge = new int[hands, buttons];       // 1 pressed, -1 released.
        for (int h = 0 ; h < hands ; ++h)
            for (int b = 0 ; b < buttons ; ++b) {
                bool d = source.down(h == 0, (Btn)b);
                edge[h, b] = (d && !was[h, b] ? 1 : (!d && was[h, b] ? -1 : 0));
                was[h, b] = d;
            }

        int free_hand = (left_handed ? 1 : 0), paddle_hand = 1 - free_hand;
        bool modes = move_table_mode || adjust_grip_mode;

        // Menu button: left controller only.
        if (edge[0, (int)Btn.Menu] == 1)
            fire("ShowSettings");

        int primary = edge[free_hand, (int)Btn.Primary];
        if (move_table_mode) {
            if (primary == 1) fire("MoveTableStart");
            if (primary == -1) fire("MoveTableEnd");
        } else if (adjust_grip_mode) {
            if (primary == 1) fire("AdjustGripStart");
            if (primary == -1) fire("AdjustGripEnd");
        }
        if (modes)
            return;

        if (edge[free_hand, (int)Btn.Trigger] == 1) fire("RobotServe");
        if (edge[free_hand, (int)Btn.Grip] == 1) fire("HoldBall");
        if (edge[free_hand, (int)Btn.Secondary] == 1) fire("HoldBall");
        if (primary == 1) fire("GhostToggleHands");

        if (edge[paddle_hand, (int)Btn.Primary] == 1) fire("GhostTogglePhase");
        if (edge[paddle_hand, (int)Btn.Secondary] == 1) fire("GhostRecord");
        if (edge[paddle_hand, (int)Btn.Thumbstick] == 1) fire("GhostSpeed");
        if (edge[paddle_hand, (int)Btn.Trigger] == 1) fire("ToggleBallFeed");

        // Holding the free hand's thumbstick in starts the next participant.
        if (was[free_hand, (int)Btn.Thumbstick]) {
            if (hold_start < 0f) {
                hold_start = now;
                hold_fired = false;
            } else if (!hold_fired && now - hold_start >= hold_seconds) {
                hold_fired = true;
                fire("GhostNewSession");
            }
        } else
            hold_start = -1f;
    }
}
