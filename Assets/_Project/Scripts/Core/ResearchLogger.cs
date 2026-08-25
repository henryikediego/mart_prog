using System;
using System.IO;
using UnityEngine;

// Direct port of research_logger.gd.
// Godot's "user://" path becomes Unity's Application.persistentDataPath —
// both point to a writable, app-private folder that survives updates.
public class ResearchLogger : MonoBehaviour
{
    public static ResearchLogger Instance { get; private set; }

    public string StudentId = "TEST_STUDENT";
    public string SessionId = "";
    public string ModuleName = "ArrayList";

    private string LogPath => Path.Combine(Application.persistentDataPath, "mart_prog_log.csv");

    private const string CsvHeader = "Timestamp,StudentID,SessionID,Module,Action,Value,Result,Details";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        CreateLogFileIfNeeded();

        // Fix (Phase 17): Godot only calls start_new_session() once a student
        // actually begins a module, from the module-selector "Start" flow in
        // ar_learning_screen.gd -- that flow (Change Module / student_panel)
        // is deferred in Unity until more modules exist (see Migration Spec
        // §12). Without ever calling it, SessionId stayed "" for the whole
        // app run, which made ExportLogToDownloads()'s destination filename
        // collapse to exactly the same path as the source log file (see the
        // comment there) -- copying a file onto itself throws a sharing
        // violation. Starting a session here, once per app run, is the
        // closest current equivalent to Godot's "begin session" moment.
        // Once the real module-selector flow is built, its own start handler
        // can call StartNewSession() again per chosen module, matching Godot
        // exactly -- doing it early now doesn't preclude that.
        StartNewSession();

        Debug.Log("Research log path: " + LogPath);
    }

    // Godot: func create_log_file_if_needed()
    public void CreateLogFileIfNeeded()
    {
        if (!File.Exists(LogPath))
        {
            try
            {
                File.WriteAllText(LogPath, CsvHeader + "\n");
            }
            catch (Exception e)
            {
                Debug.LogError("Could not create research log file: " + e.Message);
            }
        }
    }

    // Godot: func log_event(action, value = "", result = "", details = "")
    public void LogEvent(string action, string value = "", string result = "", string details = "")
    {
        CreateLogFileIfNeeded();

        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        string line =
            CsvEscape(timestamp) + "," +
            CsvEscape(StudentId) + "," +
            CsvEscape(SessionId) + "," +
            CsvEscape(ModuleName) + "," +
            CsvEscape(action) + "," +
            CsvEscape(value) + "," +
            CsvEscape(result) + "," +
            CsvEscape(details);

        try
        {
            File.AppendAllText(LogPath, line + "\n");
        }
        catch (Exception e)
        {
            Debug.LogError("Could not write to research log file: " + e.Message);
            return;
        }

        Debug.Log("LOG: " + line);
    }

    // Godot: func csv_escape(text: String) -> String
    /// <summary>
    /// Phase 29 fix (F6) — DEVIATION from source, on research-data grounds.
    ///
    /// Godot's csv_escape() doubles quotes but never touches newlines, and
    /// Unity's port matched it. Memory's Debug Exercise and Final Assessment
    /// log multi-line code as the Value field, so those rows carried a raw
    /// newline INSIDE a quoted field.
    ///
    /// That is legal RFC-4180 and a real CSV parser handles it. But anything
    /// that splits on lines before parsing — a quick wc -l, a diff, a
    /// spreadsheet import with the wrong settings, most sanity-check scripts
    /// — sees one row as two broken ones and can lose quote-state sync for
    /// the rest of the file. A research log that cannot survive being opened
    /// naively is a hazard, so newlines are folded to spaces here.
    ///
    /// Consequence to know: a multi-line code snippet in the Value column
    /// now reads as one line. The line breaks were never semantically
    /// meaningful in that column — the same snippet is shown to the student
    /// with its formatting intact on screen.
    /// </summary>
    private string CsvEscape(string text)
    {
        string escaped = text
            .Replace("\r\n", " ")
            .Replace("\n", " ")
            .Replace("\r", " ")
            .Replace("\"", "\"\"");

        return "\"" + escaped + "\"";
    }

    // Godot: func start_new_session()
    public void StartNewSession()
    {
        DateTime now = DateTime.Now;

        SessionId =
            now.Year.ToString() +
            PadNumber(now.Month) +
            PadNumber(now.Day) +
            "_" +
            PadNumber(now.Hour) +
            PadNumber(now.Minute) +
            PadNumber(now.Second);

        Debug.Log("Session ID: " + SessionId);
    }

    // Godot: func pad_number(number_value: int) -> String
    private string PadNumber(int numberValue)
    {
        if (numberValue < 10)
        {
            return "0" + numberValue;
        }

        return numberValue.ToString();
    }

    // Godot: func get_log_file_path() -> String
    public string GetLogFilePath()
    {
        return LogPath;
    }

    // Godot: func export_log_to_downloads() -> String
    // NOTE: Android's public Downloads folder needs the Storage Access
    // Framework (scoped storage) for a real device build -- this is a
    // same-app-folder copy for now, an explicit, discussed decision (see
    // Migration Spec §5/§7), not an oversight. The file is still fully
    // retrievable via a file manager app that can browse app-private
    // storage, or `adb pull`.
    public string ExportLogToDownloads()
    {
        CreateLogFileIfNeeded();

        string fileName = "mart_prog_log";
        if (!string.IsNullOrEmpty(SessionId))
        {
            fileName += "_" + SessionId;
        }
        fileName += ".csv";

        string destinationPath = Path.Combine(Application.persistentDataPath, fileName);

        // Defensive guard (Phase 17 fix): source and destination live in the
        // same folder in this same-app-folder-copy placeholder, so if
        // SessionId is ever somehow still empty when this runs, the
        // destination filename would collapse to exactly LogPath -- and
        // File.Copy(same, same, overwrite: true) throws a sharing-violation
        // IOException rather than doing anything useful. StartNewSession()
        // now always runs in Start() (see above) so this should never
        // trigger in practice, but it's cheap insurance against copying a
        // file onto itself if that ever changes.
        if (string.Equals(destinationPath, LogPath, StringComparison.Ordinal))
        {
            fileName = "mart_prog_log_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv";
            destinationPath = Path.Combine(Application.persistentDataPath, fileName);
        }

        // Phase 33: also write a fixed-name copy alongside the timestamped
        // one. Reported on device — with a new file per session, finding the
        // newest export means reading timestamps off a folder that already
        // holds dozens. "mart_prog_log_latest.csv" is always the most recent
        // export, so there is one predictable file to collect.
        string latestPath = Path.Combine(Application.persistentDataPath, "mart_prog_log_latest.csv");

        try
        {
            File.Copy(LogPath, destinationPath, true);

            if (!string.Equals(latestPath, LogPath, StringComparison.Ordinal))
            {
                File.Copy(LogPath, latestPath, true);
            }
        }
        catch (Exception e)
        {
            Debug.LogError("Could not export research log file: " + e.Message);
            return "";
        }

        // Phase 33 — the file was always being written; Android just did not
        // know about it.
        //
        // Reported on device: newly exported files were not visible in the
        // folder, while previously-seen files reappeared after being deleted.
        // That combination is diagnostic. Android serves a MEDIA INDEX to
        // MTP and to file managers, not a live directory listing. A file
        // written by ordinary C# File IO is not in that index until something
        // tells the scanner, and deleting an indexed entry without deleting
        // the underlying file just restores it on the next refresh.
        //
        // MediaScannerConnection.scanFile() is the supported way to say "this
        // path changed". Reached through JNI rather than a plugin, compiled
        // out off-Android, and wrapped so a failure can never lose an export
        // that has already been written successfully.
        NotifyAndroidMediaStore(destinationPath);
        NotifyAndroidMediaStore(latestPath);

        Debug.Log("Research log exported to: " + destinationPath +
                  " (and " + latestPath + ")");
        return destinationPath;
    }

    /// <summary>
    /// Tells Android's media scanner that a file at this path was created or
    /// changed, so it shows up over USB/MTP and in file managers. No-op
    /// everywhere else. See the note in ExportLogToDownloads().
    /// </summary>
    private static void NotifyAndroidMediaStore(string path)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaClass scanner = new AndroidJavaClass("android.media.MediaScannerConnection"))
            {
                scanner.CallStatic("scanFile", activity, new string[] { path }, null, null);
            }
        }
        catch (Exception e)
        {
            // Never fatal: the export itself already succeeded.
            Debug.LogWarning("Could not notify the Android media scanner about " +
                             path + ": " + e.Message);
        }
#endif
    }

    // Godot: func clear_log() -> bool
    public bool ClearLog()
    {
        try
        {
            File.WriteAllText(LogPath, CsvHeader + "\n");
        }
        catch (Exception e)
        {
            Debug.LogError("Could not clear research log file: " + e.Message);
            return false;
        }

        // Phase 33: same media-index problem as the export — a cleared file
        // that Android still has indexed at its old size will keep serving
        // the old content to MTP and file managers.
        NotifyAndroidMediaStore(LogPath);

        Debug.Log("Research log cleared.");
        return true;
    }
}
