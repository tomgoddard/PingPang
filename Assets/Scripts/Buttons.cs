using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Buttons : MonoBehaviour {

    /*
    public SteamVR_Action_Boolean show_tracks_action;
    public SteamVR_Action_Boolean show_previous_track_action;
    public SteamVR_Action_Boolean show_next_track_action;
    public SteamVR_Action_Boolean show_settings_action;
    */
    
    public Play play;
    public SettingsUI settings_ui;

    bool adjusting_grip;
    bool moving_table;
    Vector3 last_table_move_position;
    Quaternion last_table_move_rotation;

    void Start() {
	/*
	show_settings_action.AddOnStateDownListener(show_settings,
						    SteamVR_Input_Sources.Any);
	show_tracks_action.AddOnStateDownListener(show_tracks,
						  SteamVR_Input_Sources.Any);
	show_next_track_action.AddOnStateDownListener(show_next_track,
						      SteamVR_Input_Sources.Any);
	show_previous_track_action.AddOnStateDownListener(show_previous_track,
							  SteamVR_Input_Sources.Any);
	*/
    }

    // Handle hand controller button presses.
    void Update () {
        Hand free_hand = play.free_hand;
        Hand paddle_hand = play.paddle_hand;

	// TODO: Add trackpad dpad buttons for replay
        // Show last hit paddle position if button 2 pressed.
        // if (paddle_hand.wand.button2_pressed())
        //    replay.replay ();

	// TODO: Add GUI setting to play one handed so new ball appears in paddle hand.
        // Button 1 holds ball in paddle hand.
        // if (paddle_hand.wand.button1_pressed () && !paddle_hand.holding_ball ())
        //    play.hold_ball (paddle_hand);

	// TODO: Add start game button to GUI
        // Start game if button 1 pressed in free hand.
        // if (free_hand.wand.button1_pressed ())
        //    play.start_game ();

        // Adjust paddle grip
	if (adjusting_grip) {
	    paddle_hand.freeze_paddle = true;
	    paddle_hand.adjust_paddle_grip();
	    return;
	} else if (paddle_hand.freeze_paddle) {
	    paddle_hand.freeze_paddle = false;
	}

        // Move table
	if (moving_table)
	    move_table();
    }

    public void OnRobotServe() {
	if (play.settings.auto_serve.isOn)
	{
	    // Start or pause auto serve.
	    bool auto_serve = !play.robot.auto_serve;
	    play.robot.auto_serve = auto_serve;
	    if (auto_serve)
		play.robot.serve ();
	}
	else
	    play.robot.serve ();
    }

    public void OnHoldBall() {
	if (!play.free_hand.holding_ball ())
            if (play.playing_game && !play.player_serves ())
                play.robot.serve ();
	    else
		play.hold_ball(play.free_hand);
    }

    public void OnShowSettings() {
        settings_ui.show_ui(! settings_ui.shown());
    }

    /*
    void show_settings(SteamVR_Action_Boolean unused,
		       SteamVR_Input_Sources hand_type) {
        settings_ui.show_ui(! settings_ui.shown());
    }

    void show_tracks(SteamVR_Action_Boolean unused,
		     SteamVR_Input_Sources hand_type) {
	PathTracer tracks = play.path_tracer;
	tracks.end_tracking();
	bool shown = tracks.shown();
	bool show_ball = !shown, show_paddle = !shown;
	tracks.show_tracks(show_ball, show_paddle);
    }

    void show_next_track(SteamVR_Action_Boolean unused,
			 SteamVR_Input_Sources hand_type) {
	play.path_tracer.show_next_track();
    }

    void show_previous_track(SteamVR_Action_Boolean unused,
			     SteamVR_Input_Sources hand_type) {
	play.path_tracer.show_previous_track();
    }
    */
    
    public void enable_move_table(bool enable)
    {
	// Activate action map (ie controller button bindings) for adjusting grip.
	string action_map = (enable ? "MoveTableActions" : "PlayActions");
	PlayerInput player_input = GetComponent<PlayerInput>();
	player_input.SwitchCurrentActionMap(action_map);
	if (!enable)
	  moving_table = false;
    }
    public void OnMoveTableStart() {
	moving_table = true;
	last_table_move_position = play.paddle_hand.wand.position();
	last_table_move_rotation = play.paddle_hand.wand.rotation();
    }
    public void OnMoveTableEnd() {
	moving_table = false;
    }
    void move_table() {
        // Allow only rotation about vertical.
	Wand paddle_wand = play.paddle_hand.wand;
	Quaternion paddle_rotation = paddle_wand.rotation();
	Quaternion rotation = paddle_rotation * Quaternion.Inverse(last_table_move_rotation);
	float angle;
	Vector3 axis;
	rotation.ToAngleAxis(out angle, out axis);
	Quaternion y_rotation = Quaternion.AngleAxis(axis.y * angle, Vector3.up);

	// Don't allow changing vertical position of table.
	Vector3 paddle_position = paddle_wand.position();
	Vector3 offset = paddle_position - y_rotation*last_table_move_position;
	offset.y = 0f;

	// To move table y_rotation and offset, move camera by inverse.
	Quaternion c_rotation = Quaternion.Inverse(y_rotation);
	Vector3 c_offset = -(c_rotation * offset);

	// Apply camera motion on left.
	Transform player_transform = play.vr_camera.transform;
	player_transform.rotation = c_rotation * player_transform.rotation;
	player_transform.position = c_offset + c_rotation * player_transform.position;

	// Update hand position for new camera position.
	last_table_move_position = paddle_wand.position();
	last_table_move_rotation = paddle_wand.rotation();
    }
    
    public void enable_adjust_grip(bool enable)
    {
	// Activate action map (ie controller button bindings) for adjusting grip.
	string action_map = (enable ? "AdjustGripActions" : "PlayActions");
	PlayerInput player_input = GetComponent<PlayerInput>();
	player_input.SwitchCurrentActionMap(action_map);
    }
    public void OnAdjustGripStart() {
	adjusting_grip = true;
    }
    public void OnAdjustGripEnd() {
	adjusting_grip = false;
    }

    public void OnReportMotion() {
	Vector3 hp, hv, hav, ha;
        Quaternion hr;
        play.paddle_hand.wand.wand_motion (out hp, out hr, out hv, out hav, out ha);
	string msg = ("p " + hp.x + " " + hp.y + " " + hp.z + " " +
		      "v " + hv.x + " " + hv.y + " " + hv.z + " " +
		      "a " + ha.x + " " + ha.y + " " + ha.z + "\n" +
		      "r " + hr.w + " " + hr.x + " " + hr.y + " " + hr.z + " " +
		      "av " + hav.x + " " + hav.y + " " + hav.z + "\n");
	Debug.Log(msg);
    }
}
