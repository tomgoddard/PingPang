using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR;

// Mouse and keyboard stand-in for the headset and hand controllers so the
// game can be run in the Unity editor without a Quest.  Created
// automatically after the scene loads when no XR device is active.  Never
// runs on the headset.
//
// Wand.wand_motion() asks hand_motion() for the hand pose when it finds no
// XR controller.  Poses are in tracking space coordinates like real
// controller poses.  The paddle follows the mouse pointer so fast mouse
// motions swing the paddle and can hit the ball.
[DefaultExecutionOrder(-50)]  // Update hands before Play.Update() uses them.
public class DesktopRig : MonoBehaviour
{

    public static DesktopRig active;

    public Play play;
    public Buttons buttons;
    public Camera head_camera;
    public Transform tracking_space;

    public Vector3 head_position = new Vector3(0f, 1.6f, 0f);  // Tracking space, meters.
    public float yaw = 0f, pitch = 20f;          // Degrees, pitch positive looks down.
    public float reach = 0.5f;                   // Paddle distance from head, meters.
    public float face_angle = 0f;                // Degrees, positive closes the paddle face.
    public bool backhand = false;
    public bool show_help = true;

    // Pointer in viewport coordinates (0-1).  Follows the mouse unless
    // set_pointer() is used, e.g. by automated tests.
    public Vector2 pointer = new Vector2(0.6f, 0.4f);
    bool pointer_fixed = false;
    bool room_hidden = false;

    float look_speed = 0.15f;         // Degrees per pixel of mouse motion.
    float move_speed = 1.5f;          // Meters per second.
    float face_turn_speed = 60f;      // Degrees per second.
    float toss_height = 0.4f;         // Meters.
    float toss_duration = 0.25f;      // Seconds from release speed to top of toss.
    float toss_time = -1f;            // Time since toss started, negative if not tossing.

    // Hand poses computed once per frame, in tracking space.
    Vector3 paddle_hand_position, free_hand_position;
    Quaternion paddle_hand_rotation, free_hand_rotation;
    Vector3 paddle_hand_velocity, free_hand_velocity;
    bool have_poses = false;

    // Haptic pulses are shown on screen since there is no controller to vibrate.
    float paddle_haptic_until = -1f, free_haptic_until = -1f;
    public int paddle_haptic_count = 0, free_haptic_count = 0;
    float haptic_min_show = 0.15f;    // Seconds, so short pulses are visible.

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void create_if_no_headset()
    {
        if (Application.platform == RuntimePlatform.Android || XRSettings.isDeviceActive)
            return;
        Play play = FindFirstObjectByType<Play>();
        if (play == null)
            return;

        DesktopRig rig = new GameObject("DesktopRig").AddComponent<DesktopRig>();
        rig.play = play;
        rig.buttons = FindFirstObjectByType<Buttons>();
        rig.head_camera = (play.passthrough_camera != null ? play.passthrough_camera : Camera.main);
        rig.tracking_space = play.paddle_hand.wand.tracking_space;
        Debug.Log("No headset found, using mouse and keyboard (DesktopRig).  Press H for help.");
    }

    void OnEnable()
    {
        active = this;
    }

    void OnDisable()
    {
        if (active == this)
            active = null;
    }

    // Called by Wand.wand_motion() when there is no XR controller.
    public bool hand_motion(bool left, out Vector3 position, out Quaternion rotation,
                            out Vector3 velocity)
    {
        bool paddle = (left == play.paddle_hand.wand.left);
        position = (paddle ? paddle_hand_position : free_hand_position);
        rotation = (paddle ? paddle_hand_rotation : free_hand_rotation);
        velocity = (paddle ? paddle_hand_velocity : free_hand_velocity);
        return have_poses;
    }

    // Called by Wand.haptic_pulse() when there is no XR controller.
    public void show_haptic(bool left, float duration, float strength)
    {
        float until = Time.time + Mathf.Max(duration, haptic_min_show);
        if (left == play.paddle_hand.wand.left)
        {
            paddle_haptic_until = until;
            paddle_haptic_count += 1;
        }
        else
        {
            free_haptic_until = until;
            free_haptic_count += 1;
        }
    }

    public void set_pointer(Vector2 viewport_position)
    {
        pointer = viewport_position;
        pointer_fixed = true;
    }

    public void hold_ball()
    {
        if (buttons != null)
            buttons.OnHoldBall();
    }

    public void toss_ball()
    {
        toss_time = 0f;
    }

    public void robot_serve()
    {
        if (buttons != null)
            buttons.OnRobotServe();
    }

    // Move the head to stand at a floor position facing a direction, e.g.
    // on the phase's stand marker.  In VR the player walks there.
    public void stand_at(Vector3 world_position, Vector3 world_forward)
    {
        Vector3 p = tracking_space.InverseTransformPoint(world_position);
        head_position = new Vector3(p.x, 1.6f, p.z);
        Vector3 f = tracking_space.InverseTransformDirection(world_forward);
        yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
        pitch = 20f;
    }

    void Update()
    {
        float delta_t = Time.deltaTime;
        if (!room_hidden)
        {
            // Passthrough shows nothing in the editor, only a black
            // background, so show the virtual room instead.
            room_hidden = true;
            play.enable_show_room(false);
        }
        handle_input(delta_t);
        if (toss_time >= 0f)
        {
            toss_time += delta_t;
            if (toss_time > 3f * toss_duration)
                toss_time = -1f;   // Hand back down.
        }
        update_hands(delta_t);
    }

    void LateUpdate()
    {
        // Set camera after the camera rig has updated so ours wins.
        if (head_camera != null && tracking_space != null)
            head_camera.transform.SetPositionAndRotation(tracking_space.TransformPoint(head_position),
                                                         tracking_space.rotation * head_rotation());
    }

    Quaternion head_rotation()
    {
        return Quaternion.Euler(pitch, yaw, 0f);
    }

    Quaternion body_rotation()
    {
        return Quaternion.Euler(0f, yaw, 0f);
    }

    void handle_input(float delta_t)
    {
        Mouse mouse = Mouse.current;
        Keyboard keys = Keyboard.current;

        if (mouse != null)
        {
            if (!pointer_fixed && Screen.width > 0 && Screen.height > 0)
            {
                Vector2 m = mouse.position.ReadValue();
                pointer = new Vector2(m.x / Screen.width, m.y / Screen.height);
            }
            if (mouse.rightButton.isPressed)
            {
                Vector2 d = mouse.delta.ReadValue();
                yaw += look_speed * d.x;
                pitch = Mathf.Clamp(pitch - look_speed * d.y, -80f, 80f);
            }
            float scroll = mouse.scroll.ReadValue().y;
            if (scroll != 0f)
                reach = Mathf.Clamp(reach + 0.0005f * scroll, 0.25f, 1.0f);
            if (mouse.leftButton.wasPressedThisFrame)
                click_ui();
        }

        if (keys == null)
            return;

        // Arrow keys walk.  Not WASD since S is bound to robot serve.
        Vector3 move = Vector3.zero;
        if (keys.upArrowKey.isPressed) move += Vector3.forward;
        if (keys.downArrowKey.isPressed) move += Vector3.back;
        if (keys.leftArrowKey.isPressed) move += Vector3.left;
        if (keys.rightArrowKey.isPressed) move += Vector3.right;
        if (keys.eKey.isPressed) move += Vector3.up;
        if (keys.qKey.isPressed) move += Vector3.down;
        float speed = move_speed * (keys.shiftKey.isPressed ? 3f : 1f);
        head_position += body_rotation() * (speed * delta_t * move);

        if (keys.zKey.isPressed) face_angle -= face_turn_speed * delta_t;
        if (keys.xKey.isPressed) face_angle += face_turn_speed * delta_t;
        face_angle = Mathf.Clamp(face_angle, -80f, 80f);
        if (keys.cKey.wasPressedThisFrame) backhand = !backhand;

        if (keys.bKey.wasPressedThisFrame) hold_ball();
        if (keys.spaceKey.wasPressedThisFrame) toss_ball();
        if (keys.rKey.wasPressedThisFrame) robot_serve();
        if (keys.tabKey.wasPressedThisFrame && buttons != null) buttons.OnShowSettings();
        if (keys.hKey.wasPressedThisFrame) show_help = !show_help;

        // Same as right controller A and B buttons.
        GhostCoach coach = FindFirstObjectByType<GhostCoach>();
        if (coach != null)
        {
            if (keys.pKey.wasPressedThisFrame) coach.OnGhostTogglePhase();
            if (keys.kKey.wasPressedThisFrame) coach.OnGhostRecord();
            if (keys.lKey.wasPressedThisFrame) coach.OnGhostSpeed();
            if (keys.nKey.wasPressedThisFrame) coach.OnGhostNewSession();
            if (keys.backspaceKey.wasPressedThisFrame) coach.OnGhostReset();
            if (keys.gKey.wasPressedThisFrame) coach.OnGhostToggleHands();
            if (keys.fKey.wasPressedThisFrame && coach.ball_feeder != null) coach.ball_feeder.OnToggleBallFeed();
        }
        // Same as choosing Right or Left in the settings menu.
        if (keys.vKey.wasPressedThisFrame && play.settings != null)
            set_left_handed(play.settings, !play.paddle_hand.wand.left);
    }

    // The Right and Left toggles share a toggle group, but the group only
    // switches the other one off while the menu is shown, so do it here.
    public static void set_left_handed(SettingsUI settings, bool left)
    {
        (left ? settings.righthanded : settings.lefthanded).isOn = false;
        (left ? settings.lefthanded : settings.righthanded).isOn = true;
    }

    void update_hands(float delta_t)
    {
        Quaternion head = head_rotation();
        Quaternion body = body_rotation();

        // Paddle center along the pointer ray from the head.
        float tan_y = Mathf.Tan(0.5f * Mathf.Deg2Rad * (head_camera != null ? head_camera.fieldOfView : 60f));
        float aspect = (Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f);
        Vector3 ray = new Vector3((2f * pointer.x - 1f) * tan_y * aspect,
                                  (2f * pointer.y - 1f) * tan_y, 1f).normalized;
        Vector3 paddle_position = head_position + head * (reach * ray);

        // Paddle blade upright facing forward, forehand or backhand rubber
        // toward the opponent, face tilted by face_angle about the body right axis.
        Vector3 forward = body * Vector3.forward;
        Quaternion upright = Quaternion.LookRotation(backhand ? forward : -forward, Vector3.up);
        Quaternion paddle_rotation = Quaternion.AngleAxis(face_angle, body * Vector3.right) * upright;

        // Invert the grip transform used by Grip.hand_to_paddle_motion().
        Grip grip = play.paddle_hand.grip;
        if (grip == null)
            return;
        Vector3 gp = grip.paddle_grip_position;
        Quaternion gr = grip.paddle_grip_rotation;
        if (play.paddle_hand.wand.left)
        {
            gp = new Vector3(-gp.x, gp.y, gp.z);
            gr = new Quaternion(gr.x, -gr.y, -gr.z, gr.w);
        }
        Quaternion hand_rotation = paddle_rotation * Quaternion.Inverse(gr);
        Vector3 hand_position = paddle_position - hand_rotation * gp;

        // Free hand low in front of the body on the side away from the paddle.
        float side = (play.paddle_hand.wand.left ? 1f : -1f);
        Vector3 free_position = head_position + body * new Vector3(0.25f * side, -0.45f, 0.35f);
        if (toss_time >= 0f)
        {
            float f = Mathf.Min(toss_time / toss_duration, 1f);
            free_position.y += toss_height * Mathf.Sin(0.5f * Mathf.PI * f);
        }

        if (have_poses && delta_t > 0f)
        {
            paddle_hand_velocity = (hand_position - paddle_hand_position) / delta_t;
            free_hand_velocity = (free_position - free_hand_position) / delta_t;
        }
        paddle_hand_position = hand_position;
        paddle_hand_rotation = hand_rotation;
        free_hand_position = free_position;
        free_hand_rotation = body;
        have_poses = true;
    }

    // Click menu toggles and buttons with the mouse.  In VR they are
    // pressed by touching them with the paddle (TouchUI).
    void click_ui()
    {
        if (head_camera == null)
            return;
        Ray r = head_camera.ScreenPointToRay(Mouse.current.position.ReadValue());
        RaycastHit[] hits = Physics.RaycastAll(r, 20f, ~0, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit h in hits)
        {
            GameObject g = h.collider.gameObject;
            if (g.GetComponent<Toggle>() != null || g.GetComponent<Button>() != null)
            {
                ExecuteEvents.Execute(g, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
                return;
            }
        }
    }

    void OnGUI()
    {
        float y = Screen.height - 40;
        if (Time.time < paddle_haptic_until)
            GUI.Box(new Rect(10, y, 260, 28), "Haptic: paddle hand (" + paddle_haptic_count + ")");
        if (Time.time < free_haptic_until)
            GUI.Box(new Rect(280, y, 260, 28), "Haptic: free hand (" + free_haptic_count + ")");
        if (!show_help)
        {
            GUI.Label(new Rect(10, 10, 300, 25), "H: help");
            return;
        }
        string help =
            "Desktop mode (no headset)\n" +
            "Mouse: move paddle     Scroll: paddle distance\n" +
            "Right drag: look       Arrows, Q/E: walk, down/up (Shift fast)\n" +
            "Z/X: open/close face   C: forehand/backhand (" + (backhand ? "backhand" : "forehand") + ")\n" +
            "B: ball in hand        Space: toss    R or S: robot serve\n" +
            "Tab: settings menu     Left click: press menu buttons\n" +
            "P: coach/path phase   K: next stroke\n" +
            "L: coach speed 100/50/25%   V: right/left handed\n" +
            "N: next test participant   Backspace: reset\n" +
            "G: hide/show racket and ball in hand   F: ball feed\n" +
            "H: hide help";
        GUI.Box(new Rect(10, 10, 420, 200), "");
        GUI.Label(new Rect(18, 14, 410, 185), help);
    }
}
