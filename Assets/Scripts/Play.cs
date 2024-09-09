using System;  // use IntPtr
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//using System.Runtime.InteropServices;  // Use Marshal

public class Play : MonoBehaviour {
    public Hand paddle_hand;
    public Hand free_hand;
    public Grips paddle_grips;
    public Wand left_wand, right_wand, tracker_wand;
    public float haptics_duration = 0.01f;  // Seconds for hand pulse.
    public float haptics_strength = 1.0f;   // Strength of hand pulse, 0-1.
    public Robot robot;
    public Balls balls;
    public Ball ball_in_play;
    public BallTracking ball_tracking;
    PlayState play_state, player_to_serve, robot_to_serve;
    public TextMesh billboard;
    public List<Bouncer> marked_surfaces;
    public bool playing_game = false;
    int score_player = 0, score_robot = 0;
    public SettingsUI settings;
    public GameObject vr_camera;
    public OVRPassthroughLayer pass_through_video;
    public Renderer floor;  // Used to hide floor with pass-through video
    public Camera passthrough_camera;   // Used to hide skybox with pass-through video
    public PathTracer path_tracer;
    int track_countdown;
    
    Play() {
        create_play_states ();
    }

    void create_play_states() {
        PlayState pts = new PlayState("player to serve");
        PlayState psp = new PlayState("player serve paddle", "player_paddle");
        PlayState pst = new PlayState("player serve table", "table_near");
        PlayState rt = new PlayState ("robot table", "table_far");
        PlayState rh = new PlayState ("robot hit", "robot_paddle");
        PlayState pt = new PlayState ("player table", "table_near");
        PlayState ph = new PlayState ("player hit", "player_paddle");
        PlayState rts = new PlayState("robot to serve");
        PlayState rsp = new PlayState("robot serve paddle", "robot_paddle");
        PlayState rst = new PlayState("robot serve table", "table_far");
        PlayState nt = new PlayState("net touch", "net");
        pts.next_state = psp;
        psp.next_state = pst;
        pst.next_state = rt;
        rt.next_state = rh;
        rh.next_state = pt;
        rh.optional_next_state = nt;
        pt.next_state = ph;
        ph.next_state = rt;
        ph.optional_next_state = nt;
        rts.next_state = rsp;
        rsp.next_state = rst;
        rst.next_state = pt;
        player_to_serve = pts;
        robot_to_serve = rts;
    }
    void Awake() {
	// Initialize grips before SettingsUI.Start() initialize
	// settings so initial grip can be set.
        paddle_grips = new Grips();
        paddle_grips.initialize_grips();
    }
    
    void Start() {
        hold_ball (free_hand);
    }
    
    void Update() {
        float delta_t = Time.deltaTime;
        paddle_hand.update_paddle_position (delta_t);
        // Robot paddle motion must be before ball motion to keep
        // ball and paddle exactly in sync during robot serve.
        robot.move_paddle (delta_t);
        balls.move_balls (delta_t);
	bool tossed = free_hand.move_held_ball();

	record_tracks(tossed);
    }

    void record_tracks(bool start) {
	// Record paddle and ball tracks.
	if (start)
	{
	    path_tracer.start_tracking();
	    track_countdown = 30;  // Keep this many frames after play ends
	}
	else if (play_state == null)
	{
	    if (track_countdown > 0)
		track_countdown -= 1;
	    else if (path_tracer.is_tracking())
	    {
		path_tracer.end_tracking();
		path_tracer.show_tracks(settings.show_ball_tracks.isOn,
					settings.show_paddle_tracks.isOn);
	    }
	}
	path_tracer.new_position(paddle_hand.held_paddle, ball_in_play);
    }

    public void ball_bounced(Ball b, Bouncer bn) {
        if (b == ball_in_play && play_state != null) {
            PlayState nps = play_state.next_state;
	    PlayState onps = play_state.optional_next_state;
            if (bn.tag == nps.bouncer_tag) {
                play_state = nps;
                // Debug.Log ("Hit " + bn.tag + ", next player state " + play_state.name);
		if (play_state.name == "robot hit")
		    path_tracer.add_segment();
            } else if (onps != null && bn.tag == onps.bouncer_tag) {
                ; // net ball, play continues.
            } else {
                keep_score (play_state);
                // Debug.Log ("Point over, last state " + play_state.name + ", hit surface " + bn.tag);
                play_state = null;
		if (robot.auto_serve)
		    robot.serve();
            }
        }
        if (bn.tag == "player_paddle") {
            if (haptics_duration > 0 && haptics_strength > 0)
                paddle_hand.wand.haptic_pulse (haptics_duration, haptics_strength);
            Stroke s = return_ball (b);
            float return_time = (s == null ? 0f : s.contact_time - b.motion.time);
            report_speeds (b, bn, return_time);
            enable_markers();
//	    report_imu_state();  // Only for SteamVR
        }
        if (bn.tag == "robot_paddle") {
            disable_markers();
            /*
            Debug.Log("Robot paddle hit time " + b.motion.time
            + " hit height " + b.motion.position.y
            + " pos " + b.motion.position
            + " vel " + b.motion.velocity
            + " ang vel " + b.motion.angular_velocity);
            */
        }
    }

    Stroke return_ball(Ball b) {

        bool player_serve = (play_state != null &&
                          play_state.name == "player serve paddle");
        BallTrack bt = ball_tracking.predict_ball_flight (b, player_serve);
        //Debug.Log ("Ball " + (bt.in_play ? "in play" : "out of play")
        //    + " over net " + bt.over_net.Count + " after bounce " + bt.final.Count);

        if (!bt.in_play)
            return null;

        Stroke s = robot.return_ball (bt);
        /*
        if (player_serve && s != null)
            serves.set_last_player_serve(b);
        */
        if (player_serve)
        {
            Serve serve = new Serve(free_hand.last_toss, bt);
            robot.serves.add_serve(serve);
        }
        
        return s;
    }
    
    public void hold_ball(Hand hand) {
        Ball b = balls.new_ball ();
        hand.hold_ball (b);
        ball_in_play = b;
        play_state = player_to_serve;
        robot.new_rally();
	path_tracer.clear_tracking();
    }

    public bool ball_held(Ball b) {
        return (free_hand.held_ball == b || paddle_hand.held_ball == b);
    }

    public Ball start_robot_serve() {
        play_state = robot_to_serve;
        Ball ball = balls.new_ball ();
        ball_in_play = ball;
	record_tracks(true);
        return ball;
    }

    public void start_game() {
        playing_game = true;
        score_player = 0;
        score_robot = 0;
        report_score ();
    }

    void keep_score(PlayState last_state) {
        if (playing_game) {
            string sname = last_state.name;
            if (sname.StartsWith ("robot"))
                score_player += 1;
            else if (sname != "player to serve")
                score_robot += 1;
            report_score ();
        }
    }

    public bool player_serves() {
        int total_score = score_player + score_robot;
        bool ps = (
            total_score < 20 ?
            (((total_score / 2) % 2) == 0) :  // Have not reached deuce.
            ((total_score % 2) == 0));        // Have reached deuce.
        return ps;
    }

    void report_score() {
        string nl = System.Environment.NewLine;
        string msg = "Player " + score_player + " Robot " + score_robot;
        if (score_player >= 11 && score_player >= score_robot + 2) {
            msg += nl + nl + "You win!!!";
            playing_game = false;
        } else if (score_robot >= 11 && score_robot >= score_player + 2) {
            msg += nl + nl + "Robot wins";
            playing_game = false;
        } else {
            msg += nl + nl + (player_serves() ? "You serve" : "Robot serves");
            msg += nl + nl + "Game to 11" + (score_player + score_robot >= 20 ? ", win by 2" : "");
        }
        billboard.text = msg;

    }

    void report_speeds(Ball b, Bouncer bn_paddle, float tb_time) {
        string nl = System.Environment.NewLine;
        Vector3 v = bn_paddle.wall_strike_velocity;  // Paddle velocity.
        Vector3 n = paddle_hand.held_paddle.forehand_normal();
        if (bn_paddle.z_top)
            n = -n; // backhand normal
        float vf = Vector3.Dot (v, n);
        float vg = (v - vf * n).magnitude;
        BallState bs = b.motion;
        Vector3 s = bs.angular_velocity * b.radius;
        float ss = -Vector3.Dot (s, Vector3.up);
        Vector3 vb = bs.velocity;
        float ts = Vector3.Dot (s, Vector3.Cross (Vector3.up, vb).normalized);
        float paddle_width = 0.15f;
        Vector3 vbi = bn_paddle.ball_strike_velocity;
        float td = (Vector3.Dot (v, n) * vbi - Vector3.Dot (vbi, n) * v).magnitude;
        float timing = 0.5f * paddle_width * Vector3.Dot (v - vbi, n) / td;
        billboard.text = ("Paddle speed " + v.magnitude.ToString("F1") + " m/s" + 
            ", flat " + vf.ToString("F1") + " m/s, graze " + vg.ToString("F1") + " m/s" + nl +
            "Ball speed " + vb.magnitude.ToString ("F1") + " m/s" + nl +
            "Ball spin " + s.magnitude.ToString("F1") + " m/s" +
            ", top " + ts.ToString("F1") + " m/s, side " + ss.ToString("F1") + " m/s" + nl +
            "Time to top of bounce " + tb_time.ToString("F3") + " sec" + ", timing " + timing.ToString("F3"));
    }

    void enable_markers() {
        foreach (Bouncer bn in marked_surfaces)
            bn.mark = true;
    }

    void disable_markers() {
        foreach (Bouncer bn in marked_surfaces)
            bn.mark = false;
    }
    void report_imu_state() {
	/*
	double time = 0;
	Vector3 accel = new Vector3(0,0,0);
	Vector3 rot = new Vector3(0,0,0);
	int out_of_range = 0;
	if (paddle_hand.wand.imu_state(ref time, ref accel, ref rot,
				       ref out_of_range))
	{
	    Debug.Log("IMU sample: time " + time.ToString("F3") +
		      " accel " + accel.x.ToString("F") + "," +
		      accel.y.ToString("F") + "," + accel.z.ToString("F") +
		      " rotation " + rot.x.ToString("F") + "," +
		      rot.y.ToString("F") + "," + rot.z.ToString("F"));
	    if (out_of_range != 0)
		Debug.Log("IMU out of range " + out_of_range);
	}
	else
	    Debug.Log("Failed to get IMU state.");
	*/
    }
    
    public void enable_show_room(bool show_room)
    {
	pass_through_video.enabled = show_room;
	floor.enabled = !show_room;
	passthrough_camera.clearFlags = (show_room ? CameraClearFlags.SolidColor : CameraClearFlags.Skybox);
    }
}

public class PlayState {
    public string name;
    public PlayState next_state, optional_next_state;
    public string bouncer_tag;       // Surface to hit to reach this state.
    public PlayState(string name, string bouncer_tag ="") {
        this.name = name;
        this.next_state = null;
        this.bouncer_tag = bouncer_tag;
    }
}
