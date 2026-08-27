using System;
using System.IO;
using UnityEngine;

namespace MartProg.Core
{
    /// <summary>
    /// Phase 39 — the Feedback screen's data sink.
    /// Backlog_Landing_Page_And_Navigation.md §6.6.
    ///
    /// NOT A PORT. `main_menu.gd` has no Feedback concept at all, and neither
    /// does `research_logger.gd` — this is new, Unity-only infrastructure,
    /// deliberately shaped as a close sibling of <see cref="ResearchLogger"/>
    /// rather than an extension of it.
    ///
    /// **Why a second file, not a row in `mart_prog_log.csv`.** Two reasons,
    /// both recorded in the backlog rather than assumed. First, the CSV
    /// escaping in `ResearchLogger.CsvEscape()` took two phases to get right
    /// (Phase 29's newline fix, following an earlier quoting fix) — a
    /// paragraph of free-text feedback is exactly the input that exercises
    /// every edge of that code, and putting it in the file the whole research
    /// pipeline depends on makes it load-bearing again for no reason. Second,
    /// the main log is read QUANTITATIVELY — session counts, module timing —
    /// and a multi-line feedback comment in one cell breaks that read even
    /// when the escaping is technically correct.
    ///
    /// **Why a near-duplicate of ResearchLogger rather than a shared base
    /// class.** `CsvEscape()` and `NotifyAndroidMediaStore()` below are
    /// copied, not extracted. Both are small, pure, already proven across
    /// several phases of ResearchLogger, and touching `research_logger.gd`'s
    /// direct port to expose them would mean changing a file this project
    /// otherwise keeps as close to source as it can get, for the benefit of
    /// an unrelated concern. Same call this project already made once before
    /// for a similar duplication-vs-purity tradeoff — see the dev report's
    /// "On not splitting the timer out" note for Quiz. Worth revisiting if a
    /// THIRD CSV file ever shows up; two is not yet a pattern.
    ///
    /// **Where the Student ID comes from — read this before changing it.**
    /// `LogFeedback()` reads <see cref="ChangeModuleController.RegisteredStudentId"/>,
    /// NOT `ResearchLogger.Instance.StudentId`. The two are not
    /// interchangeable here: `ResearchLogger.StudentId` only becomes the real
    /// participant ID once a module session actually starts
    /// (`ChangeModuleController.OnConfirmClicked()`), and stays at its
    /// "TEST_STUDENT" default until then. Feedback is reachable from the
    /// Landing Page BEFORE that ever happens — a participant can tap Feedback
    /// as their very first action. Reading `ResearchLogger.StudentId` here
    /// would silently write "TEST_STUDENT" as the identity on exactly that
    /// path, which is the same shape of bug `ChangeModuleController` was
    /// built in Phase 27 to fix for the main log (see its own class comment).
    /// `RegisteredStudentId` is the one property in the project already built
    /// to answer "who is this device's participant" independent of session
    /// state, which is why it was made `public static` in Phase 38.
    ///
    /// **`FeedbackController.Show()` guards this at the UI level too** — if
    /// no ID is registered yet, the form is not shown at all, so in practice
    /// this method should never be called with an empty ID. It is still
    /// handled here (an empty string is written, not skipped) rather than
    /// assumed impossible, because a CSV row that is silently dropped is
    /// worse than one that visibly needs a second look.
    ///
    /// **Export and Clear exist on this class but are not wired to any
    /// button yet.** Deliberately — Backlog §5/§7 item 6 ("Researcher area")
    /// is where Export Log / Clear Log get consolidated onto one admin row,
    /// and that is the natural point to make both buttons also cover this
    /// file, so a researcher resetting between participants or between pilot
    /// and study cannot reset the behavioural log and forget this one. Wiring
    /// it now would mean doing Researcher-area-shaped work inside the
    /// Feedback phase, which is exactly the scope-mixing "one screen at a
    /// time" (§5) exists to prevent. The methods are ready to be called from
    /// there without any change to this class.
    /// </summary>
    public class FeedbackLogger : MonoBehaviour
    {
        public static FeedbackLogger Instance { get; private set; }

        private string LogPath =>
            Path.Combine(Application.persistentDataPath, "mart_prog_feedback.csv");

        // Provenance columns first, content after — same convention Phase 36
        // established for the main log (Timestamp, Version, StudentID ahead
        // of anything that describes what happened).
        private const string CsvHeader = "Timestamp,Version,StudentID,Rating,FeedbackText";

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
            CreateFileIfNeeded();
            Debug.Log("Feedback log path: " + LogPath);
        }

        /// <summary>
        /// Same schema-migration guard as ResearchLogger.CreateLogFileIfNeeded()
        /// (Phase 34/36) — if a future change to CsvHeader doesn't match what's
        /// on disk, the old file is archived under a timestamped name rather
        /// than silently mixing two row shapes in one file.
        /// </summary>
        public void CreateFileIfNeeded()
        {
            try
            {
                if (!File.Exists(LogPath))
                {
                    File.WriteAllText(LogPath, CsvHeader + "\n");
                    return;
                }

                string firstLine;
                using (StreamReader reader = new StreamReader(LogPath))
                {
                    firstLine = reader.ReadLine() ?? "";
                }

                if (string.Equals(firstLine.Trim(), CsvHeader, StringComparison.Ordinal)) return;

                string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string archivedPath = Path.Combine(
                    Application.persistentDataPath, "mart_prog_feedback_pre_migration_" + stamp + ".csv");

                File.Move(LogPath, archivedPath);
                File.WriteAllText(LogPath, CsvHeader + "\n");

                Debug.LogWarning("Feedback log schema changed (header did not match this build's). " +
                                 "The previous file was preserved as: " + archivedPath);

                NotifyAndroidMediaStore(archivedPath);
                NotifyAndroidMediaStore(LogPath);
            }
            catch (Exception e)
            {
                Debug.LogError("Could not create or migrate feedback log file: " + e.Message);
            }
        }

        /// <summary>
        /// Writes one feedback submission. See the class comment above for
        /// why the Student ID comes from <see cref="ChangeModuleController.RegisteredStudentId"/>
        /// rather than <see cref="ResearchLogger"/>.
        /// </summary>
        public bool LogFeedback(int rating, string feedbackText)
        {
            CreateFileIfNeeded();

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string studentId = ChangeModuleController.RegisteredStudentId;

            string line =
                CsvEscape(timestamp) + "," +
                CsvEscape(Application.version) + "," +
                CsvEscape(studentId) + "," +
                CsvEscape(rating.ToString()) + "," +
                CsvEscape(feedbackText ?? "");

            try
            {
                File.AppendAllText(LogPath, line + "\n");
            }
            catch (Exception e)
            {
                Debug.LogError("Could not write to feedback log file: " + e.Message);
                return false;
            }

            Debug.Log("FEEDBACK: " + line);
            return true;
        }

        // Copied from ResearchLogger.CsvEscape() (Phase 29 fix, F6) rather
        // than shared — see the class comment. Folds embedded newlines to
        // spaces so a naive line-split (a quick wc -l, a diff, a spreadsheet
        // import with the wrong settings) can't desync quote state for the
        // rest of the file — exactly the input a multi-paragraph feedback
        // comment is likely to contain.
        private string CsvEscape(string text)
        {
            string escaped = (text ?? "")
                .Replace("\r\n", " ")
                .Replace("\n", " ")
                .Replace("\r", " ")
                .Replace("\"", "\"\"");

            return "\"" + escaped + "\"";
        }

        public string GetLogFilePath()
        {
            return LogPath;
        }

        /// <summary>
        /// Not yet wired to any button — see the class comment ("Export and
        /// Clear exist on this class but are not wired to any button yet").
        /// Mirrors ResearchLogger.ExportLogToDownloads(): a same-app-folder
        /// copy (Migration Spec §5/§7's discussed placeholder for the real
        /// Storage-Access-Framework Downloads export), plus a fixed-name
        /// "_latest" copy (Phase 33) and the Android media-scanner notify
        /// (Phase 33) so both are actually visible over MTP/file managers.
        /// </summary>
        public string ExportFeedbackToDownloads()
        {
            CreateFileIfNeeded();

            string fileName = "mart_prog_feedback_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv";
            string destinationPath = Path.Combine(Application.persistentDataPath, fileName);
            string latestPath = Path.Combine(Application.persistentDataPath, "mart_prog_feedback_latest.csv");

            try
            {
                File.Copy(LogPath, destinationPath, true);
                File.Copy(LogPath, latestPath, true);
            }
            catch (Exception e)
            {
                Debug.LogError("Could not export feedback log file: " + e.Message);
                return "";
            }

            NotifyAndroidMediaStore(destinationPath);
            NotifyAndroidMediaStore(latestPath);

            Debug.Log("Feedback log exported to: " + destinationPath + " (and " + latestPath + ")");
            return destinationPath;
        }

        /// <summary>
        /// Not yet wired to any button — see the class comment.
        /// </summary>
        public bool ClearFeedback()
        {
            try
            {
                File.WriteAllText(LogPath, CsvHeader + "\n");
            }
            catch (Exception e)
            {
                Debug.LogError("Could not clear feedback log file: " + e.Message);
                return false;
            }

            NotifyAndroidMediaStore(LogPath);

            Debug.Log("Feedback log cleared.");
            return true;
        }

        // Copied from ResearchLogger.NotifyAndroidMediaStore() (Phase 33) —
        // see the class comment for why this is duplicated rather than
        // shared. No-op everywhere except a real Android device build.
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
                Debug.LogWarning("Could not notify the Android media scanner about " +
                                 path + ": " + e.Message);
            }
#endif
        }
    }
}
