
using System.Collections;
using System.Collections.Generic;
using System.IO;     // Use File
using System;        // Use Serializable
using UnityEngine;
using UnityEngine.InputSystem;		// Use PlayerInput
using UnityEngine.UI;

public class SettingsUI : MonoBehaviour
{
    public Buttons buttons;

    // Setting panels
    public GameObject ui_panels;
    public GameObject general_settings, serve_settings, rally_settings, cues_settings;

    public Button show_general_settings;
    public Button show_serve_settings;
    public Button show_rally_settings;
    public Button show_cues_settings;
    public Button show_settings_button, hide_settings_button;
    public Button save_settings_button;

    public Play play;

    // General settings:
    public Toggle show_room;
    
    public Toggle righthanded, lefthanded;
    public Toggle shakehands, penholder, customgrip;
    public Toggle custom_paddle;
    public Toggle adjust_grip;
    public Button save_grips;

    public Toggle white_ball, orange_striped_ball;

    public Toggle move_table, hide_table;
    public Table table;

    // Serve settings
    public Toggle auto_serve, repeat_serve;
    public Toggle serve_short, serve_medium, serve_long, serve_longest;
    public Toggle serve_topspin, serve_backspin, serve_nospin;
    public Toggle serve_to_forehand, serve_to_backhand, serve_to_middle;
    public Toggle serve_low, serve_medium_high, serve_high;
    public Button remove_serve;
    public Text serves_matching;
    
    // Rally settings
    public Toggle spin_top, spin_back, spin_none, spin_back1top, spin_varied, spin_varied_back, spin_topback;
    public Toggle place_forehand, place_backhand, place_random_forehand, place_random, place_diagonal, place_down_line, place_elbow, place_no_return;
    public Toggle loop, smash, drop, chop, block;
    public Toggle move_slow, move_medium, move_fast, move_unlimited;
    public Toggle rally_slow, rally_medium, rally_fast;

    // Visual Cues settings
    public Toggle hit_arrows;
    public GameObject arrows;
    public Toggle show_paddle_marker, show_net_marker, show_table_marker;
    public Toggle show_paddle_tracks, show_ball_tracks;
    public GameObject paddle_marker, robot_paddle_marker, net_marker, table_marker;

    void Start()
    {
        // Show table top control panels when buttons on edge
        // of table pressed.
        bind_show_hide_buttons();
	bind_save_button();

        // Buttons change settings as soon as they are clicked.
        bind_grip_buttons();
	bind_show_room_button();
	bind_move_table_button();
	bind_hide_table_button();
        bind_ball_color_buttons();
        bind_arrow_buttons();
        bind_marker_buttons();
	bind_track_buttons();
        bind_move_speed_buttons();        
        bind_rally_speed_buttons();
        bind_rally_spin_buttons();
        bind_rally_pattern_buttons();
        bind_serve_buttons();
        bind_shot_type_buttons();

        load_settings();
        filter_serves();
    }
    
    void bind_show_hide_buttons()
    {
        add_panel(general_settings, show_general_settings);
        add_panel(serve_settings, show_serve_settings);
        add_panel(rally_settings, show_rally_settings);
	add_panel(cues_settings, show_cues_settings);

        show_settings_button.onClick.AddListener(delegate { show_ui( !shown() ); } );
        hide_settings_button.onClick.AddListener(delegate { show_ui(false); } );
    }
    
    void bind_save_button()
    {
        save_settings_button.onClick.AddListener(delegate { save_settings(); } );
    }

    void add_panel(GameObject panel, Button show)
    {
        show.onClick.AddListener(delegate { show_settings(panel); } );
    }

    public void show_ui(bool show)
    {
	ui_panels.SetActive( show );
	if (! show)
	    move_table.isOn = false;
    }

    public bool shown()
    {
        return ui_panels.activeSelf;
    }

    void show_settings(GameObject panel)
    {
        panel.SetActive(! panel.activeSelf);
        GameObject [] panels = {general_settings, serve_settings, rally_settings, cues_settings};
        foreach (GameObject p in panels)
            if (p != panel)
                p.SetActive(false);
    }

    void set_action_map(string action_map)
    {
	PlayerInput player_input = GetComponent<PlayerInput>();
	player_input.SwitchCurrentActionMap(action_map);
    }

    void bind_grip_buttons()
    {
        add_grip(shakehands, "shake hands");
        add_grip(penholder, "pen hold");
        add_grip(customgrip, "custom");

	bind_adjust_grip_button();
	
        save_grips.onClick.AddListener(delegate { play.paddle_grips.save_grips(); });

        add_hand(righthanded, true);
        add_hand(lefthanded, false);
	custom_paddle.onValueChanged.AddListener(delegate { set_paddle_hand(righthanded.isOn); });
    }

    void add_grip(Toggle t, string name)
    {
        t.onValueChanged.AddListener(delegate {if (t.isOn) set_paddle_grip(name);});
    }

    void add_hand(Toggle t, bool right)
    {
        t.onValueChanged.AddListener(delegate {if (t.isOn) set_paddle_hand(right);});
    }
    
    void bind_ball_color_buttons()
    {
        add_ball_color(white_ball, "white");
        add_ball_color(orange_striped_ball, "orange striped");
    }

    void add_ball_color(Toggle t, string name)
    {
        t.onValueChanged.AddListener(delegate {if (t.isOn) play.balls.set_ball_coloring(name);});
    }
    
    void bind_marker_buttons()
    {
        add_marker(show_paddle_marker, paddle_marker);
        add_marker(show_paddle_marker, robot_paddle_marker);
        add_marker(show_net_marker, net_marker);
        add_marker(show_table_marker, table_marker);
    }

    void add_marker(Toggle t, GameObject marker)
    {
        t.onValueChanged.AddListener(delegate {marker.SetActive(t.isOn);});
    }

    void bind_arrow_buttons()
    {
        hit_arrows.onValueChanged.AddListener(delegate {arrows.SetActive(hit_arrows.isOn);});
    }

    void bind_track_buttons()
    {
	show_ball_tracks.onValueChanged.AddListener(delegate {play.path_tracer.gameObject.SetActive(show_paddle_tracks.isOn || show_ball_tracks.isOn);});
        show_paddle_tracks.onValueChanged.AddListener(delegate {play.path_tracer.gameObject.SetActive(show_paddle_tracks.isOn || show_ball_tracks.isOn);});
    }
    
    void bind_serve_buttons()
    {
        Toggle[] serve_toggles = {
	    auto_serve, repeat_serve,
            serve_short, serve_medium, serve_long, serve_longest,
            serve_topspin, serve_backspin, serve_nospin,
            serve_to_forehand, serve_to_backhand,    serve_to_middle,
            serve_low, serve_medium_high,    serve_high
            };
        foreach (Toggle t in serve_toggles)
            t.onValueChanged.AddListener(serve_setting_changed);

        remove_serve.onClick.AddListener(delegate { remove_last_serve(); });
    }

    void remove_last_serve()
    {
	play.robot.serves.remove_serve(play.robot.current_serve());
	filter_serves();
	play.robot.new_serve();
    }
    
    void bind_move_speed_buttons()
    {
        add_move_speed(move_slow, "Slow");
        add_move_speed(move_medium,"Medium");
        add_move_speed(move_fast, "Fast");
        add_move_speed(move_unlimited, "Unlimited");
    }

    void add_move_speed(Toggle t, string name)
    {
        t.onValueChanged.AddListener(delegate {if (t.isOn) set_robot_speed(name);});
    }
    
    void bind_rally_pattern_buttons()
    {
        add_rally_pattern(place_forehand, "Forehand");
        add_rally_pattern(place_backhand, "Backhand");
        add_rally_pattern(place_random_forehand, "Random forehand");
        add_rally_pattern(place_random, "Random");
        add_rally_pattern(place_diagonal, "Diagonal");
        add_rally_pattern(place_down_line, "Down line");
        add_rally_pattern(place_elbow, "Elbow");
        add_rally_pattern(place_no_return, "No return");
    }

    void add_rally_pattern(Toggle t, string name)
    {
        t.onValueChanged.AddListener(delegate {if (t.isOn) set_rally_pattern(name);});
    }
    
    void bind_shot_type_buttons()
    {
        loop.onValueChanged.AddListener(delegate {play.robot.loop = loop.isOn;});
        smash.onValueChanged.AddListener(delegate {play.robot.smash = smash.isOn;});
        drop.onValueChanged.AddListener(delegate {play.robot.drop = drop.isOn;});
	chop.onValueChanged.AddListener(delegate {play.robot.chop = chop.isOn;});
	block.onValueChanged.AddListener(delegate {play.robot.block = block.isOn;});
    }

    void bind_rally_speed_buttons()
    {
        add_rally_speed(rally_slow, "Slow");
        add_rally_speed(rally_medium, "Medium");
        add_rally_speed(rally_fast, "Fast");
    }

    void add_rally_speed(Toggle t, string name)
    {
        t.onValueChanged.AddListener(delegate {if (t.isOn) set_rally_speed(name);});
    }

    void bind_rally_spin_buttons()
    {
        add_rally_spin(spin_top, "Top spin");
        add_rally_spin(spin_back, "Back spin");
        add_rally_spin(spin_none, "No spin");
        add_rally_spin(spin_back1top, "1 back spin, then top spin");
        add_rally_spin(spin_varied, "Varied");
        add_rally_spin(spin_varied_back, "Varied back spin");
        add_rally_spin(spin_topback, "Random top or back spin");
    }

    void add_rally_spin(Toggle t, string name)
    {
        t.onValueChanged.AddListener(delegate {if (t.isOn) set_rally_spin(name);});
    }

    void set_paddle_grip(string grip_name)
    {
        Grip grip = play.paddle_grips.find_grip(grip_name);
        Hand paddle_hand = play.paddle_hand;
        paddle_hand.grip = grip;
    }
    
    void set_paddle_hand(bool right)
    {
        Wand pw = (custom_paddle.isOn ? play.tracker_wand :
		   (right ? play.right_wand : play.left_wand));
        play.paddle_hand.set_hand_wand (pw);
	Wand fw = (right ? play.left_wand : play.right_wand);
        play.free_hand.set_hand_wand (fw);
	Debug.Log("Left " + play.left_wand.device_index() + " right " + play.right_wand.device_index()
		  + " tracker " + play.tracker_wand.device_index());
	/*
	var system = Valve.VR.OpenVR.System;
	Debug.Log("Actual left " +
		  system.GetTrackedDeviceIndexForControllerRole(ETrackedControllerRole.LeftHand) +
		  " right " +
 		  system.GetTrackedDeviceIndexForControllerRole(ETrackedControllerRole.RightHand)
//		  + " tracker " +
//		  system.GetTrackedDeviceIndexForControllerRole(ETrackedControllerRole.RightFoot)
		  );
	*/
    }

    void set_robot_speed(string speed)
    {
        Robot r = play.robot;
        r.movement_speed = Array.IndexOf(r.movement_speeds, speed);
        r.set_movement_speed ();
    }

    void set_rally_speed(string speed)
    {
        Robot r = play.robot;
        r.speed = Array.IndexOf(r.speeds, speed);
    }

    void set_rally_spin(string spin)
    {
        Robot r = play.robot;
        r.spin = Array.IndexOf(r.spins, spin);
    }

    void set_rally_pattern(string pattern)
    {
        Robot r = play.robot;
        r.pattern = Array.IndexOf(r.patterns, pattern);
    }

    void serve_setting_changed(bool ignore)
    {
	if (!auto_serve.isOn)
	    play.robot.auto_serve = false;
	
	int repeat = (repeat_serve.isOn ? 5 : 1);
	play.robot.set_serve_repeat(repeat);
	
        filter_serves();
    }
    
    public void filter_serves()
    {
        bool sshort = serve_short.isOn;
        bool smedium = serve_medium.isOn;
        bool slong = serve_long.isOn;
        bool slongest = serve_longest.isOn;

        bool topspin = serve_topspin.isOn;
        bool backspin = serve_backspin.isOn;
        bool nospin = serve_nospin.isOn;

        bool forehand = serve_to_forehand.isOn;
        bool backhand = serve_to_backhand.isOn;
        bool middle = serve_to_middle.isOn;

        bool low = serve_low.isOn;
        bool medhigh = serve_medium_high.isOn;
        bool high = serve_high.isOn;

        List<Serve> serves = new List<Serve>();
        foreach (Serve s in play.robot.serves.serves)
        {
            float depth = -s.bounce.z;
            float side = s.bounce.x;
            Vector3 vside = Vector3.Cross(s.ball_velocity, Vector3.up).normalized;
            float r = 0.02f;    // ball radius
            float spin = -Vector3.Dot(s.ball_angular_velocity, vside) * r;
            float height = s.top.y - 0.762f;    // height from table
            bool include = (
                ((sshort && s.double_bounce)
                 || (smedium && (!s.double_bounce && depth <= 0.9f))
                 || (slong && depth >= 0.9f)
                 || (slongest && depth >= 1.3f))
                && ((topspin && spin >= 2f)
                        || (backspin && spin <= -2f)
                                    || (nospin && spin < 2f && spin > -2f))
                && ((forehand && side >= 0.3f)
                        || (backhand && side <= -0.3f)
                                    || (middle && side <= 0.3f && side >= -0.3f))
                && ((low && height <= 0.2f)
                        || (medhigh && height >= 0.2f && height <= 0.25f)
                                    || (high && height >= 0.25f))
                );
                if (include)
                     serves.Add(s);
        }
        serves_matching.text = (serves.Count + " serves");
        if (serves.Count > 0)
            play.robot.use_these_serves(serves);
    }

    void save_settings()
    {
        SaveSettings s = new SaveSettings();
        s.grip = play.paddle_hand.grip.grip_name;

	foreach (Toggle t in ui_panels.GetComponentsInChildren<Toggle>(true))
	    if (t.name != "move table" && t.name != "adjustgrip")
	    {
		s.toggle_names.Add(t.name);
		s.toggle_values.Add(t.isOn);
	    }

	s.player_position = play.vr_camera.transform.position;
	s.use_player_position = true;

        string path = Path.Combine(Application.persistentDataPath,
				   "settings.json");
        string settings_data = JsonUtility.ToJson(s);
        File.WriteAllText(path, settings_data);
        Debug.Log("Saved settings to " + path);
    }

    bool load_settings()
    {
        string path = Path.Combine(Application.persistentDataPath,
				   "settings.json");
        string settings_data;
        if (File.Exists(path))
	{
	    settings_data = File.ReadAllText(path);
	    Debug.Log("Read settings " + path);
	}
	else
	{
	    settings_data = "{\"grip\":\"shake hands\",\"toggle_names\":[\"show arrows\",\"show paddle marker\",\"show net marker\",\"show table marker\",\"show ball tracks\",\"show paddle tracks\",\"right\",\"left\",\"tracked paddle\",\"shakehands\",\"penholder\",\"custom\",\"white\",\"orange stripe\",\"auto serve\",\"repeat serve\",\"serve short\",\"serve medium\",\"serve long\",\"serve longest\",\"serve topspin\",\"serve backspin\",\"serve nospin\",\"serve to forehand\",\"serve to backhand\",\"serve to middle\",\"serve low\",\"serve medium high\",\"serve high\",\"slow\",\"medium\",\"fast\",\"top\",\"back\",\"none\",\"varied\",\"varied back\",\"1 back, then top\",\"top and back\",\"forehand\",\"backhand\",\"random fh\",\"random\",\"diagonal\",\"down line\",\"elbow\",\"no return\",\"loop\",\"smash\",\"drop\",\"chop\",\"block\",\"move slow\",\"move medium\",\"move fast\",\"move unlimited\",\"show room\"],\"toggle_values\":[false,false,false,false,false,false,true,false,false,true,false,false,true,false,false,true,true,true,true,true,true,true,true,true,true,true,true,true,true,false,true,false,true,false,false,false,false,false,false,true,false,false,false,false,false,false,false,true,true,true,true,true,false,true,false,false,true],\"use_player_position\":true,\"player_position\":{\"x\":0.0,\"y\":0.0,\"z\":-1.5}}";
	    Debug.Log("Could not read settings " + path);
	}
	
        SaveSettings s = new SaveSettings();
        JsonUtility.FromJsonOverwrite(settings_data, s);

        set_paddle_grip(s.grip);
        customgrip.isOn = (s.grip == "custom");
        shakehands.isOn = (s.grip == "shake hands");
        penholder.isOn = (s.grip == "pen hold");

	Dictionary<string, Toggle> toggles = new Dictionary<string, Toggle>();
	foreach (Toggle t in ui_panels.GetComponentsInChildren<Toggle>(true))
	    toggles.Add(t.name, t);

	for (int i = 0 ; i < s.toggle_names.Count ; ++i)
	{
	    string name = s.toggle_names[i];
	    if (toggles.ContainsKey(name))
		toggles[name].isOn = s.toggle_values[i];
	}
	serve_setting_changed(true);
	
	if (s.use_player_position)
	    play.vr_camera.transform.position = s.player_position;
	
        return true;
    }

    void bind_show_room_button()
    {
	show_room.onValueChanged.AddListener(play.enable_show_room);
    }

    void bind_move_table_button()
    {
	move_table.onValueChanged.AddListener(buttons.enable_move_table);
    }

    void bind_hide_table_button()
    {
	hide_table.onValueChanged.AddListener(set_hide_table);
    }

    void set_hide_table(bool hide)
    {
       table.show_table(!hide);
    }

    void bind_adjust_grip_button()
    {
	adjust_grip.onValueChanged.AddListener(buttons.enable_adjust_grip);
    }
    
}

[Serializable]
public class SaveSettings
{
    public string grip;
    public List<string> toggle_names;
    public List<bool> toggle_values;
    public bool use_player_position;
    public Vector3 player_position;
    
    public SaveSettings() {
	toggle_names = new List<string>();
	toggle_values = new List<bool>();
	use_player_position = false;
    }
}
