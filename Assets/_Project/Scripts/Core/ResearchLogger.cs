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
    private string CsvEscape(string text)
    {
        string escaped = text.Replace("\"", "\"\"");
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
    // Framework (scoped storage) for a real device build — this is a
    // placeholder that works in the Editor / standalone builds. We'll
    // revisit this properly when we wire up the actual Export Log
    // button later, matching the spec's own build order.
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

        try
        {
            File.Copy(LogPath, destinationPath, true);
        }
        catch (Exception e)
        {
            Debug.LogError("Could not export research log file: " + e.Message);
            return "";
        }

        Debug.Log("Research log exported to: " + destinationPath);
        return destinationPath;
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

        Debug.Log("Research log cleared.");
        return true;
    }
}
