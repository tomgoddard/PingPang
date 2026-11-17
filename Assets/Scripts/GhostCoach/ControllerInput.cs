using UnityEngine;
using UnityEngine.XR;

// Reads the Quest controller buttons straight from the XR devices, the
// same way Wand.cs reads their poses, and runs the game actions through
// ControllerMapper.  This works whether or not the Input System connects the
// controllers to PlayControls.inputactions; if it does, ButtonDedup makes
// sure a press counts once.  The device log lines show in
// "adb logcat -s Unity", so it is easy to see whether the controllers are
// found and whether presses arrive.
public class ControllerInput : MonoBehaviour {

    public Play play;
    public Buttons buttons;
    public GhostCoach coach;

    public IButtonSource source = new XrButtonSource();
    ControllerMapper mapper = new ControllerMapper();
    bool[] seen = new bool[2];

    void Awake() {
        mapper.fire = dispatch;
    }

    void Update() {
        if (play == null || buttons == null || play.paddle_hand == null || play.paddle_hand.wand == null)
            return;
        XrButtonSource xr = source as XrButtonSource;
        if (xr != null)
            for (int h = 0 ; h < 2 ; ++h) {
                bool here = xr.present(h == 0);
                if (here != seen[h]) {
                    seen[h] = here;
                    Debug.Log("ControllerInput: " + (h == 0 ? "left" : "right") + " controller "
                              + (here ? "found" : "lost"));
                }
            }
        mapper.move_table_mode = buttons.move_table_mode;
        mapper.adjust_grip_mode = buttons.adjust_grip_mode;
        mapper.update(source, play.paddle_hand.wand.left, Time.unscaledTime);
    }

    void dispatch(string action) {
        Debug.Log("ControllerInput: " + action);
        switch (action) {
        case "RobotServe": buttons.OnRobotServe(); break;
        case "HoldBall": buttons.OnHoldBall(); break;
        case "ShowSettings": buttons.OnShowSettings(); break;
        case "MoveTableStart": buttons.OnMoveTableStart(); break;
        case "MoveTableEnd": buttons.OnMoveTableEnd(); break;
        case "AdjustGripStart": buttons.OnAdjustGripStart(); break;
        case "AdjustGripEnd": buttons.OnAdjustGripEnd(); break;
        case "GhostTogglePhase": if (coach != null) coach.OnGhostTogglePhase(); break;
        case "GhostRecord": if (coach != null) coach.OnGhostRecord(); break;
        case "GhostSpeed": if (coach != null) coach.OnGhostSpeed(); break;
        case "GhostToggleHands": if (coach != null) coach.OnGhostToggleHands(); break;
        case "GhostNewSession": if (coach != null) coach.OnGhostNewSession(); break;
        case "ToggleBallFeed": if (coach != null && coach.ball_feeder != null) coach.ball_feeder.OnToggleBallFeed(); break;
        }
    }
}

// Button states from the XR controllers (Quest Touch).
public class XrButtonSource : IButtonSource {

    InputDevice device(bool left_hand) {
        return InputDevices.GetDeviceAtXRNode(left_hand ? XRNode.LeftHand : XRNode.RightHand);
    }

    public bool present(bool left_hand) {
        return device(left_hand).isValid;
    }

    public bool down(bool left_hand, Btn button) {
        InputDevice d = device(left_hand);
        if (!d.isValid)
            return false;
        InputFeatureUsage<bool> usage;
        switch (button) {
        case Btn.Primary: usage = CommonUsages.primaryButton; break;
        case Btn.Secondary: usage = CommonUsages.secondaryButton; break;
        case Btn.Grip: usage = CommonUsages.gripButton; break;
        case Btn.Trigger: usage = CommonUsages.triggerButton; break;
        case Btn.Thumbstick: usage = CommonUsages.primary2DAxisClick; break;
        default: usage = CommonUsages.menuButton; break;
        }
        bool value;
        return d.TryGetFeatureValue(usage, out value) && value;
    }
}
