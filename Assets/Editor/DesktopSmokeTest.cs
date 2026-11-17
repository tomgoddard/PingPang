using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Plays the scene in the editor without a headset and checks that the
// desktop mouse and keyboard rig works.  Run from the command line with
//   Unity -batchmode -projectPath . -executeMethod DesktopSmokeTest.run
// Exit code 0 means all checks passed.
public static class DesktopSmokeTest {

    static int frame, phase;
    static float phase_start;
    static List<string> failures = new List<string>();
    static List<string> errors = new List<string>();
    static Vector3 paddle_start, ball_start;
    static Ball tossed_ball;
    static float toss_max_height;
    static int haptic_count_before;
    static Vector3 ghost_paddle_start;
    static float ghost_time_start;
    static int free_haptic_before;
    static ControllerMapperTest.Fake fake_controllers;
    static GhostCoach.Phase phase_before_button;
    static int participants_before_hold;
    static bool hands_hidden_before_button;
    static List<bool> paddle_states = new List<bool>();
    static Ball fed_ball_1;
    static Vector3 fed_spawn_1, fed_vel_1, fed_near_bounce_1;
    static bool fed_far_bounced, fed_near_bounced;
    static bool saved_options_enabled;
    static EnterPlayModeOptions saved_options;

    [MenuItem("GhostCoach/Run desktop smoke test")]
    public static void run() {
        EditorSceneManager.OpenScene("Assets/scenes.unity");
        frame = 0;
        phase = 0;
        phase_start = 0f;
        failures.Clear();
        errors.Clear();
        // Use a scratch folder for clips, logs and the participant counter,
        // with 2 strokes per block so the test reaches the second block.
        string root = System.IO.Path.GetFullPath("Temp/GhostCoachSmokeTest");
        if (System.IO.Directory.Exists(root))
            System.IO.Directory.Delete(root, true);
        GhostCoachFiles.root_override = root;
        ButtonDedup.window = 0f;        // The test presses the same button twice in a row.
        ExperimentConfig config = ExperimentConfig.defaults();
        config.strokes_per_block = 2;
        config.allow_recording = true;
        System.IO.File.WriteAllText(GhostCoachFiles.path(ExperimentConfig.file_name), JsonUtility.ToJson(config, true));
        // Keep static state across entering play mode.
        saved_options_enabled = EditorSettings.enterPlayModeOptionsEnabled;
        saved_options = EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        Application.logMessageReceived += record_error;
        EditorApplication.update += step;
        EditorApplication.EnterPlaymode();
    }

    static void record_error(string message, string stack, LogType type) {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            errors.Add(type + ": " + message + "\n" + stack);
    }

    static void check(bool ok, string what) {
        Debug.Log("DesktopSmokeTest " + (ok ? "PASS " : "FAIL ") + what);
        if (!ok)
            failures.Add(what);
    }

    static void step() {
        if (!EditorApplication.isPlaying)
            return;
        frame += 1;
        DesktopRig rig = DesktopRig.active;
        Play play = Object.FindFirstObjectByType<Play>();

        // Advance through phases by game time, since batch mode does not
        // cap the frame rate and frames can be very short.
        float t = Time.time - phase_start;
        if (frame > 100000) {
            check(false, "game time advanced, only " + Time.time.ToString("F2") + " s after " + frame + " frames");
            finish();
            return;
        }
        if (phase == 0 && t > 0.5f) {
            check(rig != null, "desktop rig created when no headset");
            int loose = 0;
            foreach (Ball b in Object.FindObjectsByType<Ball>(FindObjectsSortMode.None))
                if (b.gameObject.activeInHierarchy && !play.ball_held(b))
                    loose += 1;
            check(loose == 0, "no ball lying around at the start, loose balls " + loose);

            // The racket in the hand is drawn round, with the hit area unchanged.
            Paddle own = play.paddle_hand.held_paddle;
            bool round_rubber = false, box_showing = false;
            foreach (MeshFilter mf in own.GetComponentsInChildren<MeshFilter>(true)) {
                if (mf.name == "round rubber red" && mf.sharedMesh.name == "Cylinder" && mf.GetComponent<MeshRenderer>().enabled)
                    round_rubber = true;
                if (mf.sharedMesh != null && mf.sharedMesh.name == "Cube" && mf.GetComponent<MeshRenderer>().enabled)
                    box_showing = true;
            }
            check(round_rubber && !box_showing, "the racket in the hand is round, no box showing");
            Vector3 rubber = own.forehand_rubber().transform.localScale;
            check(Mathf.Approximately(rubber.x, 0.15f) && Mathf.Approximately(rubber.y, 0.16f)
                  && Mathf.Approximately(rubber.z, 0.002f),
                  "hit area is unchanged, rubber scale " + rubber.ToString("F3"));

            // Hide the racket and the ball in the hand.
            GhostCoach hide_gc = Object.FindFirstObjectByType<GhostCoach>();
            check(!hide_gc.hand_visuals.hidden, "racket and ball in the hand are shown by default");
            paddle_states.Clear();
            foreach (MeshRenderer r in own.GetComponentsInChildren<MeshRenderer>(true))
                paddle_states.Add(r.enabled);
            hide_gc.OnGhostToggleHands();
            bool all_off = true;
            foreach (MeshRenderer r in own.GetComponentsInChildren<MeshRenderer>(true))
                if (r.enabled)
                    all_off = false;
            check(hide_gc.hand_visuals.hidden && all_off, "toggle hides the racket in the hand");
            if (rig == null) { finish(); return; }
            save_camera_view(rig.head_camera, "Logs/smoke_player_view.png");
            GhostCoach gc = Object.FindFirstObjectByType<GhostCoach>();
            if (gc != null && gc.coach_ghost.showing) {
                // The moment the coach hits the ball, seen from the stand marker.
                List<float> contacts = gc.coach_clip.contact_times();
                gc.coach_ghost.seek(contacts.Count > 0 ? contacts[0] : 1f);
                GameObject cp = GameObject.Find("ghost paddle");
                if (cp != null)
                    save_view(gc.coach_ghost.player_spot + 1.6f * Vector3.up, cp.transform.position, 60f,
                              "Logs/smoke_coach_contact.png");
                // A mocap coach is capsules between the recorded joints, drawn
                // beside the player and a little larger than life.
                float t_contact = (contacts.Count > 0 ? contacts[0] : 1f);
                List<string> names = gc.coach_clip.joint_names;
                if (names.Count > 0) {
                    MotionFrame mf = gc.coach_clip.sample(t_contact);
                    GameObject torso = GameObject.Find("ghost Hips Neck");
                    Vector3 hips = gc.coach_ghost.drawn(mf.joints[names.IndexOf("Hips")]);
                    Vector3 neck = gc.coach_ghost.drawn(mf.joints[names.IndexOf("Neck")]);
                    check(torso != null && (torso.transform.position - 0.5f * (hips + neck)).magnitude < 0.03f,
                          "the torso is between the hips and neck joints");
                    float head_top = gc.coach_ghost.drawn(mf.joints[names.IndexOf("HeadTop_End")]).y;
                    float real_top = mf.joints[names.IndexOf("HeadTop_End")].y;
                    check(head_top > real_top * 1.05f && head_top < real_top * 1.15f,
                          "the coach is about 10 % larger than the recorded person (" + real_top.ToString("F2")
                          + " m becomes " + head_top.ToString("F2") + " m)");
                    // The whole figure from the front-left, further away.
                    Vector3 side = gc.coach_ghost.player_spot - hips;
                    side.y = 0f;
                    save_view(hips + 2.6f * side.normalized + 0.5f * Vector3.up, hips + 0.3f * Vector3.up, 45f,
                              "Logs/smoke_coach_body.png");
                    save_view(hips + 2.4f * Vector3.forward + 0.8f * Vector3.up + 0.6f * Vector3.right, hips + 0.3f * Vector3.up, 45f,
                              "Logs/smoke_coach_body2.png");
                }
            }
            check(gc != null && gc.coach_clip != null && gc.coach_clip.has_ball, "coach clip has a ball, recorded or made up");
            if (gc != null && gc.coach_clip != null && gc.coach_clip.source == "mock") {
                check(gc.coach_ghost.showing, "mock coach shown when nothing is recorded");
                GameObject mp = GameObject.Find("ghost paddle");
                if (mp != null)
                    save_screenshot(mp.transform.position, "Logs/smoke_mock_coach.png");
            }
            rig.set_pointer(new Vector2(0.5f, 0.5f));
            next_phase();
        }
        else if (phase == 1 && t > 0.2f) {
            Ball in_hand = play.free_hand.held_ball;
            check(in_hand != null && !in_hand.GetComponent<MeshRenderer>().enabled, "toggle hides the ball in the hand");
            {
                GhostCoach g1 = Object.FindFirstObjectByType<GhostCoach>();
                Paddle own1 = play.paddle_hand.held_paddle;
                g1.OnGhostToggleHands();
                MeshRenderer[] now = own1.GetComponentsInChildren<MeshRenderer>(true);
                bool same = (now.Length == paddle_states.Count);
                bool box_showing = false;
                for (int k = 0 ; same && k < now.Length ; ++k) {
                    if (now[k].enabled != paddle_states[k])
                        same = false;
                    MeshFilter mfk = now[k].GetComponent<MeshFilter>();
                    if (now[k].enabled && mfk != null && mfk.sharedMesh != null && mfk.sharedMesh.name == "Cube")
                        box_showing = true;
                }
                check(!g1.hand_visuals.hidden && same && !box_showing,
                      "toggle again restores the round racket as it was, " + now.Length + " parts");
                check(!ExperimentState.load().hide_hand_visuals, "the choice is remembered");
                // The racket in the hand, forehand side, from close by.
                Vector3 normal = own1.forehand_normal();
                save_view(own1.position + 0.35f * normal + 0.08f * Vector3.up, own1.position, 40f,
                          "Logs/smoke_own_racket.png");
            }
            paddle_start = play.paddle_hand.held_paddle.transform.position;
            rig.set_pointer(new Vector2(0.8f, 0.3f));
            check(play.free_hand.holding_ball(), "ball held in free hand at start");
            tossed_ball = play.free_hand.held_ball;
            if (tossed_ball != null)
                ball_start = tossed_ball.transform.position;
            toss_max_height = 0f;
            rig.toss_ball();
            next_phase();
        }
        else if (phase == 2 && t <= 0.5f && tossed_ball != null) {
            toss_max_height = Mathf.Max(toss_max_height, tossed_ball.transform.position.y - ball_start.y);
        }
        else if (phase == 2) {
            Vector3 p = play.paddle_hand.held_paddle.transform.position;
            check((p - paddle_start).magnitude > 0.05f,
                  "paddle follows pointer, moved " + (p - paddle_start).magnitude.ToString("F2") + " m");
            check(!play.free_hand.holding_ball(), "ball released by toss");
            check(tossed_ball == null || tossed_ball.GetComponent<MeshRenderer>().enabled, "the ball is visible again");
            if (tossed_ball != null)
                check(toss_max_height > 0.15f,
                      "tossed ball rose " + toss_max_height.ToString("F2") + " m");
            play.robot_active = true;      // The robot rests in the watch phase.
            rig.robot_serve();
            next_phase();
        }
        else if (phase == 3 && t > 0.1f) {
            ball_start = play.ball_in_play.transform.position;
            next_phase();
        }
        else if (phase == 4 && t > 0.5f) {
            float d = (play.ball_in_play.transform.position - ball_start).magnitude;
            check(d > 0.3f, "robot served ball moved " + d.ToString("F2") + " m");

            GhostCoach coach = Object.FindFirstObjectByType<GhostCoach>();
            check(coach != null, "GhostCoach created");
            if (coach == null) { finish(); return; }
            GhostCoach.Phase before = coach.phase;
            haptic_count_before = rig.paddle_haptic_count;
            coach.OnGhostTogglePhase();
            check(coach.phase != before, "phase toggled to " + coach.phase);
            // A quick start and stop, as from random button presses, must not
            // be saved or replace the coach.
            string coach_before = coach.coach_clip.name;
            coach.OnGhostRecord();
            coach.OnGhostRecord();
            check(!coach.recorder.recording && coach.coach_clip.name == coach_before
                  && !string.IsNullOrEmpty(coach.recorder.discarded),
                  "a quick record and stop is discarded, \"" + coach.recorder.discarded + "\"");
            check(System.IO.Directory.Exists(MotionClip.clips_directory()) == false
                  || System.IO.Directory.GetFiles(MotionClip.clips_directory(), "*.json").Length == 0,
                  "the discarded recording was not saved");
            coach.OnGhostRecord();
            check(coach.recorder.recording, "recording started");
            next_phase();
        }
        else if (phase == 5 && t > 0.5f) {
            // Move the paddle during the recording so the replay moves, and
            // mark a ball contact so the clip has a stroke to compare with.
            rig.set_pointer(new Vector2(0.3f, 0.6f));
            play.player_paddle_hit(play.ball_in_play);
            next_phase();
        }
        else if (phase == 6 && t > MotionClip.min_coach_seconds + 0.3f) {
            GhostCoach coach = Object.FindFirstObjectByType<GhostCoach>();
            check(rig.paddle_haptic_count > haptic_count_before,
                  "haptic pulses shown in desktop mode (" + (rig.paddle_haptic_count - haptic_count_before) + ")");
            coach.OnGhostRecord();
            MotionClip c = coach.coach_clip;
            check(!coach.recorder.recording && c != null && c.frames.Count > 10,
                  "recording saved " + (c == null ? 0 : c.frames.Count) + " frames");
            if (c == null) { finish(); return; }
            MotionFrame m = c.sample(0.5f * c.duration);
            check(m.head_position.y > 0.5f,
                  "clip head height in table space " + m.head_position.y.ToString("F2") + " m");
            // Remove the test clip so it is not loaded as the coach later.
            System.IO.File.Delete(System.IO.Path.Combine(MotionClip.clips_directory(), c.name + ".json"));

            check(coach.coach_ghost.showing && !coach.coach_ghost.show_ball && play.robot_active && coach.path_guide.showing,
                  "play phase: coach stays beside, no coach ball, robot plays, path shown");
            coach.OnGhostTogglePhase();
            check(coach.phase == GhostCoach.Phase.Coach && coach.coach_ghost.showing,
                  "coach ghost shown in coach phase");
            check(!play.robot_active && coach.coach_ghost.show_ball && !coach.path_guide.showing,
                  "watch phase: robot rests, coach hits its ball, no path");
            int stroke_count = coach.coach_clip.strokes().Count;
            if (stroke_count >= 2) {
                int stroke_before = coach.stroke;
                coach.next_stroke();
                check(coach.stroke == (stroke_before + 1) % stroke_count && coach.coach_ghost.stroke == coach.stroke
                      && coach.path_guide.stroke == coach.stroke, "next stroke changes the repeated stroke to " + coach.stroke);
                check(coach.coach_ghost.time >= 0f && coach.coach_ghost.showing, "coach still shown after changing stroke");
            }
            coach.coach_ghost.speed = 1f;
            GameObject gp = GameObject.Find("ghost paddle");
            check(gp != null, "ghost paddle created");
            MeshFilter blade = (gp != null ? gp.GetComponentInChildren<MeshFilter>() : null);
            bool round = false;
            if (gp != null)
                foreach (MeshFilter mf in gp.GetComponentsInChildren<MeshFilter>())
                    if (mf.name.Contains("blade") && mf.sharedMesh.name == "Cylinder")
                        round = true;
            check(round, "ghost racket blade is round");
            if (gp != null)
                ghost_paddle_start = gp.transform.position;
            ghost_time_start = coach.coach_ghost.time;
            {
                GameObject marker = GameObject.Find("stand here marker");
                check(marker != null && !marker.GetComponent<MeshRenderer>().enabled,
                      "the floor marker exists but is hidden by default");
            }
            next_phase();
        }
        else if (phase == 7 && t > 0.8f) {
            GhostCoach coach = Object.FindFirstObjectByType<GhostCoach>();
            check(coach.coach_ghost.time != ghost_time_start, "coach replay time advanced");
            GameObject gp = GameObject.Find("ghost paddle");
            if (gp != null) {
                float d = (gp.transform.position - ghost_paddle_start).magnitude;
                check(d > 0.05f, "ghost paddle replays recorded motion, moved " + d.ToString("F2") + " m");
                save_screenshot(gp.transform.position, "Logs/smoke_coach_phase.png");
            }
            int contacts = coach.coach_clip.contact_times().Count;
            check(contacts >= 1, "recorded clip has ball contacts (" + contacts + ")");

            // Take the live balls out of play so only the simulated hits below
            // count as strokes.
            foreach (Ball b in Object.FindObjectsByType<Ball>(FindObjectsSortMode.None))
                b.freeze = true;
            coach.OnGhostTogglePhase();
            check(coach.phase == GhostCoach.Phase.Path && coach.coach_ghost.showing
                  && coach.path_guide.showing, "play phase keeps the coach and shows the path");
            check(coach.path_guide.coach_path_points > 10,
                  "coach paddle path drawn with " + coach.path_guide.coach_path_points + " points");
            free_haptic_before = rig.free_haptic_count;
            // Simulate the player hitting the ball.
            play.player_paddle_hit(play.ball_in_play);
            next_phase();
        }
        else if (phase == 8 && t > 0.6f) {
            GhostCoach coach = Object.FindFirstObjectByType<GhostCoach>();
            StrokeScore sc = coach.path_guide.last_score;
            check(sc != null && sc.score >= 0 && sc.score <= 100 && sc.tip.Length > 0,
                  "stroke scored " + (sc == null ? "none" : sc.score + " \"" + sc.tip + "\""));
            check(play.billboard.text.StartsWith("Score"), "score shown on billboard");
            check(rig.free_haptic_count > free_haptic_before, "score vibration in free hand");
            GameObject line = GameObject.Find("coach paddle path");
            if (line != null)
                save_screenshot(line.GetComponent<LineRenderer>().GetPosition(0), "Logs/smoke_path_phase.png");
            // Switch to left handed in the settings menu.
            DesktopRig.set_left_handed(play.settings, true);
            next_phase();
        }
        else if (phase == 9 && t > 0.2f) {
            GhostCoach coach = Object.FindFirstObjectByType<GhostCoach>();
            check(play.paddle_hand.wand.left, "left handed chosen in settings");
            check(coach.coach_clip.left_handed && !coach.recorded_clip.left_handed,
                  "right handed coach mirrored for a left handed player");
            check(coach.path_guide.showing, "path still shown after switching hand");
            check(binding(coach, "HoldBall").Contains("{RightHand}") && binding(coach, "RobotServe").Contains("{RightHand}"),
                  "left handed: ball and serve buttons on the right (free) hand, " + binding(coach, "HoldBall"));
            check(binding(coach, "GhostTogglePhase").Contains("{LeftHand}"),
                  "left handed: phase button mirrored to the left hand");
            check(binding(coach, "ShowSettings").Contains("{LeftHand}"), "menu button stays on the left controller");
            DesktopRig.set_left_handed(play.settings, false);
            next_phase();
        }
        else if (phase == 10 && t > 0.2f) {
            GhostCoach coach = Object.FindFirstObjectByType<GhostCoach>();
            check(!play.paddle_hand.wand.left && coach.coach_clip == coach.recorded_clip,
                  "back to right handed, coach not mirrored");
            check(binding(coach, "HoldBall").Contains("{LeftHand}") && binding(coach, "GhostTogglePhase").Contains("{RightHand}"),
                  "right handed: buttons back to normal");
            Experiment e = coach.experiment;
            check(e.participant == "P01" && e.block == 0 && e.variant.name == "A" && e.stroke_in_block == 1,
                  "session P01 in block 1 (variant A) after one stroke, " + e.participant + " block " + e.block_label);
            // Second stroke ends block 1.
            play.player_paddle_hit(play.ball_in_play);
            next_phase();
        }
        else if (phase == 11 && t > 0.6f) {
            GhostCoach coach = Object.FindFirstObjectByType<GhostCoach>();
            Experiment e = coach.experiment;
            check(e.block == 1 && e.variant.name == "B", "after 2 strokes block 2 uses variant B");
            check(play.billboard.text.StartsWith("Block 2 av 4"), "block change shown: " + play.billboard.text.Replace("\n", " / "));
            free_haptic_before = rig.free_haptic_count;
            play.player_paddle_hit(play.ball_in_play);
            next_phase();
        }
        else if (phase == 12 && t > 0.6f) {
            GhostCoach coach = Object.FindFirstObjectByType<GhostCoach>();
            check(play.billboard.text == "", "variant B shows no score text, billboard \"" + play.billboard.text.Replace("\n", " / ") + "\"");
            check(rig.free_haptic_count > free_haptic_before, "variant B still vibrates");
            string log_path = coach.experiment.log.path;
            coach.OnGhostNewSession();
            check_log(log_path);
            Experiment e = coach.experiment;
            check(e.participant == "P02" && string.Join(" ", e.schedule) == "B A A B" && e.variant.name == "B",
                  "next participant P02 gets B A A B, " + e.participant + " " + string.Join(" ", e.schedule));
            check(e.log.path != log_path && System.IO.File.Exists(e.log.path), "new log file for P02");
            // Reset after random button presses.
            coach.coach_ghost.speed = 1f;
            if (coach.phase == GhostCoach.Phase.Coach)
                coach.OnGhostTogglePhase();
            coach.OnGhostReset();
            check(coach.phase == GhostCoach.Phase.Coach && coach.coach_ghost.showing
                  && Mathf.Approximately(coach.coach_ghost.speed, 0.5f),
                  "reset goes back to phase 1 at half speed");

            // Press buttons on a fake pair of controllers: the whole route from
            // the controllers to the game, without the Input System.
            ControllerInput controllers = coach.GetComponent<ControllerInput>();
            check(controllers != null, "controller input is set up");
            fake_controllers = new ControllerMapperTest.Fake();
            controllers.source = fake_controllers;
            ButtonDedup.log.Clear();
            phase_before_button = coach.phase;
            fake_controllers.set(false, Btn.Primary, true);          // Right A.
            next_phase();
        }
        else if (phase == 13 && t > 0.3f) {
            GhostCoach coach = Object.FindFirstObjectByType<GhostCoach>();
            fake_controllers.set(false, Btn.Primary, false);
            check(ButtonDedup.log.Contains("GhostTogglePhase") && coach.phase != phase_before_button,
                  "right A on the controller switches phase, now " + coach.phase);
            check(ButtonDedup.log.FindAll(a => a == "GhostTogglePhase").Count == 1, "holding it for a while switches only once");
            bool hidden_before = coach.hand_visuals.hidden;
            fake_controllers.set(true, Btn.Primary, true);           // Left X.
            hands_hidden_before_button = hidden_before;
            next_phase();
        }
        else if (phase == 14 && t > 0.3f) {
            GhostCoach coach = Object.FindFirstObjectByType<GhostCoach>();
            fake_controllers.set(true, Btn.Primary, false);
            check(coach.hand_visuals.hidden != hands_hidden_before_button, "left X on the controller hides or shows the racket and ball");
            coach.hand_visuals.apply_hidden(false);                   // Leave it as it was.
            fake_controllers.set(true, Btn.Trigger, true);           // Left trigger: robot serve.
            next_phase();
        }
        else if (phase == 15 && t > 0.3f) {
            fake_controllers.set(true, Btn.Trigger, false);
            check(ButtonDedup.log.Contains("RobotServe"), "left trigger on the controller serves");
            GhostCoach coach = Object.FindFirstObjectByType<GhostCoach>();
            participants_before_hold = int.Parse(coach.experiment.participant.Substring(1));
            fake_controllers.set(true, Btn.Thumbstick, true);        // Hold the left thumbstick.
            next_phase();
        }
        else if (phase == 16 && t > 1.8f) {
            fake_controllers.set(true, Btn.Thumbstick, false);
            GhostCoach coach = Object.FindFirstObjectByType<GhostCoach>();
            int now = int.Parse(coach.experiment.participant.Substring(1));
            check(ButtonDedup.log.Contains("GhostNewSession") && now == participants_before_hold + 1,
                  "holding the left thumbstick starts the next participant, " + coach.experiment.participant);

            // Toggle BallFeeder on via paddle hand trigger (right trigger).
            check(coach.ball_feeder != null, "BallFeeder component created");
            ButtonDedup.log.Clear();
            fake_controllers.set(false, Btn.Trigger, true);
            fed_far_bounced = false;
            fed_near_bounced = false;
            next_phase();
        }
        else if (phase == 17) {
            GhostCoach coach = Object.FindFirstObjectByType<GhostCoach>();
            BallFeeder feeder = coach.ball_feeder;
            if (t > 0.1f)
                fake_controllers.set(false, Btn.Trigger, false);
            if (fed_ball_1 == null && feeder.fed_count >= 1) {
                fed_ball_1 = play.ball_in_play;
                fed_spawn_1 = feeder.last_spawn_position;
                fed_vel_1 = feeder.last_launch_velocity;
                check(ButtonDedup.log.Contains("ToggleBallFeed") && feeder.feeding,
                      "paddle hand trigger starts standalone ball feed");
                check(!play.robot_active && !coach.coach_ghost.showing && !coach.path_guide.showing,
                      "ball feed runs standalone with robot and ghost hidden");
                check(fed_spawn_1.z > 0f && fed_vel_1.z < 0f,
                      "fed ball spawns on far side (" + fed_spawn_1.ToString("F2") + ") moving toward player");
            }
            if (fed_ball_1 != null) {
                Vector3 bp = fed_ball_1.transform.position;
                Vector3 bv = fed_ball_1.motion.velocity;
                if (!fed_far_bounced && bp.z > 0f && bv.y > 0f && bp.y <= coach.table.height + 0.10f)
                    fed_far_bounced = true;
                if (fed_far_bounced && !fed_near_bounced && bp.z < 0f && bv.y > 0f && bp.y <= coach.table.height + 0.10f) {
                    fed_near_bounced = true;
                    fed_near_bounce_1 = bp;
                }
            }
            if (t > 1.5f) {
                check(fed_far_bounced && fed_near_bounced,
                      "fed ball bounces on far side then near side (" + fed_near_bounce_1.ToString("F2") + ")");
                next_phase();
            }
        }
        else if (phase == 18 && t > 1.7f) {
            GhostCoach coach = Object.FindFirstObjectByType<GhostCoach>();
            BallFeeder feeder = coach.ball_feeder;
            check(feeder.fed_count >= 2,
                  "second ball fed after ~3 seconds regardless of player return (count " + feeder.fed_count + ")");
            check((feeder.last_spawn_position - fed_spawn_1).magnitude < 0.001f
                  && (feeder.last_launch_velocity - fed_vel_1).magnitude < 0.001f,
                  "second feed has identical spawn and launch velocity");
            fake_controllers.set(false, Btn.Trigger, true);
            next_phase();
        }
        else if (phase == 19 && t > 0.2f) {
            fake_controllers.set(false, Btn.Trigger, false);
            GhostCoach coach = Object.FindFirstObjectByType<GhostCoach>();
            check(!coach.ball_feeder.feeding, "paddle hand trigger toggles ball feed off");
            finish();
        }
    }

    // The session log of P01 should have the session, both blocks and the
    // three strokes with their scores and the feedback given.
    static void check_log(string path) {
        string[] lines = System.IO.File.ReadAllLines(path);
        Debug.Log("DesktopSmokeTest session log " + path + ":\n" + string.Join("\n", lines));
        int strokes = 0, blocks = 0, phases = 0;
        bool feedback_ok = true, columns_ok = true;
        int event_col = System.Array.IndexOf(SessionLog.columns, "event");
        int variant_col = System.Array.IndexOf(SessionLog.columns, "variant");
        int text_col = System.Array.IndexOf(SessionLog.columns, "feedback_text");
        int score_col = System.Array.IndexOf(SessionLog.columns, "score");
        foreach (string line in lines) {
            string[] c = line.Split(',');
            if (c.Length < SessionLog.columns.Length) { columns_ok = false; continue; }
            string ev = c[event_col];
            if (ev == "block_start") blocks += 1;
            if (ev == "phase") phases += 1;
            if (ev == "stroke") {
                strokes += 1;
                int score;
                bool text_expected = (c[variant_col] == "A");
                if (!int.TryParse(c[score_col], out score) || c[text_col] != (text_expected ? "1" : "0"))
                    feedback_ok = false;
            }
        }
        check(lines.Length > 0 && lines[0].StartsWith("time,") && lines[1].Contains(",session_start,"),
              "log starts with header and session_start");
        check(strokes == 3 && blocks == 2, "log has 3 strokes and 2 block starts (" + strokes + ", " + blocks + ")");
        check(phases >= 2, "log has the phase changes (" + phases + ")");
        check(feedback_ok, "each stroke row has a score and the feedback of its variant");
        check(columns_ok, "every row has all " + SessionLog.columns.Length + " columns");
        check(lines[lines.Length-1].Contains(",session_end,"), "log ends with session_end");
    }

    // Render a view from beside and behind a point, for checking visuals
    // by eye.  Logs/ is not in git.
    static void save_screenshot(Vector3 target, string path) {
        Camera cam = new GameObject("smoke test camera").AddComponent<Camera>();
        Vector3 table_center = Object.FindFirstObjectByType<Table>().transform.position;
        Vector3 away = target - table_center;
        away.y = 0f;
        cam.transform.position = target + 1.8f * away.normalized + new Vector3(1.2f, 0.6f, 0f);
        cam.transform.LookAt(target - 0.3f * Vector3.up);
        cam.fieldOfView = 70f;
        RenderTexture rt = new RenderTexture(1280, 720, 24);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        Texture2D image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        image.Apply();
        RenderTexture.active = null;
        System.IO.File.WriteAllBytes(path, image.EncodeToPNG());
        Object.DestroyImmediate(cam.gameObject);
        Debug.Log("DesktopSmokeTest saved screenshot " + path);
    }

    // Render from a point toward another point, for checking visuals by eye.
    static void save_view(Vector3 from, Vector3 look_at, float fov, string path) {
        Camera cam = new GameObject("smoke test view").AddComponent<Camera>();
        cam.transform.position = from;
        cam.transform.LookAt(look_at);
        cam.fieldOfView = fov;
        RenderTexture rt = new RenderTexture(1280, 720, 24);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        Texture2D image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        image.Apply();
        RenderTexture.active = null;
        System.IO.File.WriteAllBytes(path, image.EncodeToPNG());
        Object.DestroyImmediate(cam.gameObject);
        Debug.Log("DesktopSmokeTest saved screenshot " + path);
    }

    // What the player sees: render the head camera itself.
    static void save_camera_view(Camera cam, string path) {
        RenderTexture rt = new RenderTexture(1280, 720, 24);
        RenderTexture old = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render();
        cam.targetTexture = old;
        RenderTexture.active = rt;
        Texture2D image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        image.Apply();
        RenderTexture.active = null;
        System.IO.File.WriteAllBytes(path, image.EncodeToPNG());
        Debug.Log("DesktopSmokeTest saved screenshot " + path + " from " + cam.name
                  + " clear " + cam.clearFlags + " background " + cam.backgroundColor);
    }

    // Effective path of an action's first binding, including overrides.
    static string binding(GhostCoach coach, string action) {
        var input = coach.GetComponent<UnityEngine.InputSystem.PlayerInput>();
        return input.actions.FindAction(action).bindings[0].effectivePath;
    }

    static void next_phase() {
        phase += 1;
        phase_start = Time.time;
    }

    static void finish() {
        GhostCoachFiles.root_override = null;
        ButtonDedup.window = 0.15f;
        EditorApplication.update -= step;
        Application.logMessageReceived -= record_error;
        EditorApplication.ExitPlaymode();
        EditorSettings.enterPlayModeOptionsEnabled = saved_options_enabled;
        EditorSettings.enterPlayModeOptions = saved_options;

        foreach (string e in errors)
            Debug.Log("DesktopSmokeTest logged error during play: " + e);
        bool ok = (failures.Count == 0);
        Debug.Log("DesktopSmokeTest " + (ok ? "PASSED" : "FAILED") + ", "
                  + failures.Count + " failed checks, " + errors.Count + " errors logged, "
                  + frame + " frames, " + Time.time.ToString("F1") + " s game time");
        if (Application.isBatchMode)
            EditorApplication.Exit(ok ? 0 : 1);
    }
}
