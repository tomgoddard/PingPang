using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Runs the programmatically controlled comparative test and logs every
// session to CSV (GhostCoach/logs/P03_20261013_140522.csv).
//
// Each app launch, or holding the left thumbstick in (N on the keyboard),
// starts a session for the next participant, P01, P02, ...  Scored strokes
// in phase 2 count toward the current block; after strokes_per_block
// strokes the next variant in the schedule takes over.  Logging goes on
// after the schedule is done, and when run_schedule is off.
public class Experiment {

    public ExperimentConfig config;
    public string participant;
    public string session;
    public List<string> schedule;
    public int block;                 // 0-based index into schedule, -1 when done.
    public int stroke_in_block, stroke_total;
    public Variant variant;
    public SessionLog log;

    GhostCoach coach;
    float start_time;

    public Experiment(GhostCoach coach) {
        this.coach = coach;
    }

    public bool finished {
        get { return block < 0; }
    }

    public string block_label {
        get { return (config.run_schedule && !finished ? (block + 1) + "/" + schedule.Count : ""); }
    }

    public void start_session() {
        end_session();
        config = ExperimentConfig.load_or_create();
        ExperimentState state = ExperimentState.load();
        int number = state.next_participant;
        state.next_participant = number + 1;
        state.save();

        participant = "P" + number.ToString("00");
        session = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        schedule = config.schedule_for(number);
        start_time = Time.time;
        stroke_total = 0;
        string path = Path.Combine(GhostCoachFiles.directory("logs"), participant + "_" + session + ".csv");
        log = new SessionLog(path);
        log_event("session_start", "schedule " + string.Join(" ", schedule)
                  + ", " + config.strokes_per_block + " strokes per block"
                  + (config.run_schedule ? "" : ", schedule off"));
        start_block(config.run_schedule && schedule.Count > 0 ? 0 : -1);
        Debug.Log("GhostCoach session " + participant + " logging to " + path);
    }

    public void end_session() {
        if (log == null)
            return;
        log_event("session_end", stroke_total + " strokes");
        log.close();
        log = null;
    }

    void start_block(int b) {
        block = b;
        stroke_in_block = 0;
        variant = config.variant(finished ? (schedule.Count > 0 ? schedule[schedule.Count-1] : "A")
                                 : schedule[block]);
        log_event(finished ? "free_play" : "block_start", variant.name + ": " + variant.description);
    }

    // Log a scored stroke.  Returns a message to show when this stroke
    // ended a block, or null.
    public string stroke(StrokeScore s, bool text_shown, bool haptic_given) {
        stroke_in_block += 1;
        stroke_total += 1;
        write_row("stroke", "", s, text_shown, haptic_given);
        if (finished || stroke_in_block < config.strokes_per_block)
            return null;
        log_event("block_end", variant.name);
        if (block + 1 < schedule.Count) {
            start_block(block + 1);
            return "Block " + (block + 1) + " av " + schedule.Count + "\nFortsätt spela";
        }
        start_block(-1);
        log_event("schedule_done", "");
        return "Klart, tack!\nDu kan fortsätta spela";
    }

    public void log_event(string name, string detail) {
        write_row(name, detail, null, false, false);
    }

    void write_row(string name, string detail, StrokeScore s, bool text_shown, bool haptic_given) {
        if (log == null)
            return;
        MotionClip clip = coach.coach_clip;
        log.write(DateTime.Now, Time.time - start_time, participant, session, name, detail,
                  finished ? "" : (object)(block + 1), variant != null ? variant.name : "",
                  s != null ? (object)stroke_in_block : null, s != null ? (object)stroke_total : null,
                  coach.phase == GhostCoach.Phase.Coach ? "coach" : "path",
                  coach.play.paddle_hand.wand.left,
                  clip != null ? clip.name : "", clip != null ? clip.source : "",
                  s != null ? (object)s.score : null,
                  s != null ? (object)(100f * s.shape_error) : null,
                  s != null ? (object)s.face_angle : null,
                  s != null ? (object)s.speed_ratio : null,
                  s != null ? (object)(100f * s.contact_offset) : null,
                  s != null ? s.tip : null,
                  s != null ? (object)text_shown : null,
                  s != null ? (object)haptic_given : null);
    }
}
