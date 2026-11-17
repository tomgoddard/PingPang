using System;
using System.Globalization;
using System.IO;
using System.Text;

// CSV log of one session, one row per event.  Comma separated with a
// decimal point, which Google Sheets, Python and R read directly (in
// Excel with Swedish settings use Data > From Text/CSV).  Every row is
// flushed right away so nothing is lost if the app is closed.
public class SessionLog {

    public static readonly string[] columns = {
        "time", "session_seconds", "participant", "session", "event", "detail",
        "block", "variant", "stroke_in_block", "stroke_total", "phase", "left_handed",
        "coach_clip", "coach_source", "score", "shape_error_cm", "face_angle_deg",
        "speed_ratio", "contact_offset_cm", "tip", "feedback_text", "feedback_haptic",
    };

    public string path;
    StreamWriter writer;

    public SessionLog(string path) {
        this.path = path;
        writer = new StreamWriter(path, false, new UTF8Encoding(false));
        writer.AutoFlush = true;
        writer.WriteLine(string.Join(",", columns));
    }

    // Values in the order of columns.  Missing values are written empty.
    public void write(params object[] values) {
        if (writer == null)
            return;
        string[] cells = new string[columns.Length];
        for (int i = 0 ; i < cells.Length ; ++i)
            cells[i] = (i < values.Length ? cell(values[i]) : "");
        writer.WriteLine(string.Join(",", cells));
    }

    public void close() {
        if (writer != null)
            writer.Close();
        writer = null;
    }

    public static string cell(object value) {
        if (value == null)
            return "";
        string s;
        if (value is float f)
            s = f.ToString("0.###", CultureInfo.InvariantCulture);
        else if (value is double d)
            s = d.ToString("0.###", CultureInfo.InvariantCulture);
        else if (value is bool b)
            s = (b ? "1" : "0");
        else if (value is DateTime t)
            s = t.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
        else
            s = Convert.ToString(value, CultureInfo.InvariantCulture);
        if (s.IndexOfAny(new char[] { ',', '"', '\n', '\r' }) >= 0)
            s = "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }
}
