using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MartProg.Core
{
    /// <summary>
    /// Ports the "admin" buttons from ar_learning_screen.gd: Export Log and
    /// Clear Log. Both act on the ResearchLogger singleton (Phase 14) and
    /// report their result through the same DashboardController
    /// Operation/Explanation panels every module controller already uses.
    ///
    /// Quiz is intentionally NOT part of this controller — see
    /// MART_PROG_Unity_Migration_Spec.md §12 for why it's still deferred.
    /// Change Module was also deferred through Phase 19, but as of Phase 20
    /// it's built as its own separate ChangeModuleController.cs (Scripts/Core/),
    /// not folded into this one — see Migration Spec §8f. Its trigger button
    /// still sits in the same shared admin row as Export Log/Clear Log in the
    /// scene, it's just wired to ChangeModuleController instead of here, to
    /// keep this controller's responsibility scoped to logging.
    /// </summary>
    public class AdminController : MonoBehaviour
    {
        [Header("Admin Buttons")]
        [SerializeField] private Button exportLogButton;
        [SerializeField] private Button clearLogButton;

        [Header("Clear Log Confirmation")]
        [SerializeField] private GameObject clearLogConfirmDialog;
        [SerializeField] private Button clearLogConfirmYesButton;
        [SerializeField] private Button clearLogConfirmNoButton;

        [Header("Layout (optional)")]
        [SerializeField] private RectTransform dashboardContent;

        private void Start()
        {
            if (exportLogButton != null)
                exportLogButton.onClick.AddListener(OnExportLogClicked);

            if (clearLogButton != null)
                clearLogButton.onClick.AddListener(OnClearLogClicked);

            if (clearLogConfirmYesButton != null)
                clearLogConfirmYesButton.onClick.AddListener(OnClearLogConfirmed);

            if (clearLogConfirmNoButton != null)
                clearLogConfirmNoButton.onClick.AddListener(OnClearLogCancelled);

            // Same pattern as Godot's ConfirmationDialog: hidden until Clear Log
            // is clicked, so the destructive action always needs a second tap.
            if (clearLogConfirmDialog != null)
                clearLogConfirmDialog.SetActive(false);
        }

        private void OnExportLogClicked()
        {
            LogEvent("Export Log", "", "Requested", "User requested research log export");

            string exportedPath = ResearchLogger.Instance != null
                ? ResearchLogger.Instance.ExportLogToDownloads()
                : "";

            // Phase 29 fix (F8) — DEVIATION from source, on research-data
            // grounds. Both builds logged exactly one row for Export Log,
            // Result = "Requested", written BEFORE the export was attempted
            // and never followed up. A failed export was therefore
            // indistinguishable from a successful one in the CSV — which
            // matters precisely because a failed export is the case where
            // the researcher most needs to know something went wrong.
            //
            // The "Requested" row above is kept so the row Godot writes
            // still appears; this adds the outcome that was missing.
            if (!string.IsNullOrEmpty(exportedPath))
            {
                SetFeedback(
                    "Export Log successful.",
                    $"The research log was copied to: {exportedPath}"
                );

                LogEvent("Export Log", exportedPath, "Success", "Research log exported");
            }
            else
            {
                SetFeedback(
                    "Export Log failed.",
                    "The research log could not be exported."
                );

                LogEvent("Export Log", "", "Failed", "Research log export failed");
            }
        }

        // Phase 32: how long the button stays "armed" when no dialog exists.
        private const float ClearLogArmSeconds = 6f;
        private float clearLogArmedUntil = -1f;

        /// <summary>
        /// Clearing the log is destructive and unrecoverable, so it always
        /// takes TWO deliberate actions. Godot did that with a
        /// ConfirmationDialog; this method keeps that when the dialog exists
        /// and provides an equivalent when it does not.
        ///
        /// **Phase 32 — why the fallback exists.** The 25 Aug research export
        /// settled it: 1,046 rows going back to 5 August, and the only
        /// "Clear Log" row in the whole file is from 5 August. Every press
        /// since then produced no row at all, which means
        /// OnClearLogConfirmed() has never run — the confirmation has never
        /// been confirmed, because the dialog never appears.
        ///
        /// Rather than keep diagnosing a scene object that may simply never
        /// have been built, the confirmation now falls back to arming the
        /// button itself: press once to arm, press again within
        /// ClearLogArmSeconds to clear. Two deliberate taps, no scene work,
        /// and it cannot be reached by accident.
        ///
        /// The dialog path is still preferred when a dialog is assigned AND
        /// can actually become visible, so a properly wired scene behaves
        /// exactly as before.
        /// </summary>
        private void OnClearLogClicked()
        {
            // ---- Preferred path: a real, visible confirmation dialog ----
            if (clearLogConfirmDialog != null)
            {
                clearLogConfirmDialog.SetActive(true);

                // SetActive(true) does NOT guarantee the dialog is on screen:
                // if any ancestor is inactive the object stays hidden while
                // reporting activeSelf == true. activeInHierarchy is what
                // tells the two apart.
                if (clearLogConfirmDialog.activeInHierarchy && clearLogConfirmYesButton != null)
                {
                    clearLogArmedUntil = -1f;
                    return;
                }

                // Unusable — put it back and fall through to the button
                // fallback rather than leaving a dead button.
                clearLogConfirmDialog.SetActive(false);

                Debug.LogWarning("AdminController: Clear Log Confirm Dialog is assigned but cannot be " +
                                 "shown (an ancestor is inactive) or has no Yes button; " +
                                 "falling back to two-tap confirmation on the button.");
            }

            // ---- Fallback: arm the button, confirm with a second press ----
            if (Time.time <= clearLogArmedUntil)
            {
                clearLogArmedUntil = -1f;
                OnClearLogConfirmed();
                return;
            }

            clearLogArmedUntil = Time.time + ClearLogArmSeconds;

            SetFeedback(
                "Clear Log — press again to confirm.",
                $"This erases the whole research log and cannot be undone. Press Clear Log " +
                $"again within {ClearLogArmSeconds:0} seconds to go ahead, or do nothing to cancel.");
        }

        private void OnClearLogCancelled()
        {
            if (clearLogConfirmDialog != null)
                clearLogConfirmDialog.SetActive(false);
        }

        private void OnClearLogConfirmed()
        {
            if (clearLogConfirmDialog != null)
                clearLogConfirmDialog.SetActive(false);

            bool cleared = ResearchLogger.Instance != null && ResearchLogger.Instance.ClearLog();

            if (cleared)
            {
                SetFeedback(
                    "Clear Log successful.",
                    "The research log has been cleared."
                );

                // Phase 29 fix (F7) — DEVIATION from source's TEXT, to stop
                // the log making a false claim about itself.
                //
                // Both builds logged "Research log cleared and restarted",
                // but ClearLog() only rewrites the header — it never touches
                // SessionId or calls StartNewSession(). The next row carries
                // the SAME SessionID, so a researcher segmenting the CSV by
                // session was told a boundary existed where none did.
                //
                // The wording is corrected rather than the behaviour: making
                // ClearLog() actually restart the session would change session
                // semantics mid-study, which is the riskier of the two
                // changes. If a real session boundary at clear time is wanted
                // later, call StartNewSession() from ClearLog() and restore
                // the original wording.
                LogEvent("Clear Log", "", "Success", "Research log cleared");
            }
            else
            {
                SetFeedback(
                    "Clear Log failed.",
                    "The research log could not be cleared."
                );
            }
        }

        private void SetFeedback(string operation, string explanation)
        {
            DashboardController.Instance?.SetOperation(operation);
            DashboardController.Instance?.SetExplanation(explanation);
            RebuildDashboardLayout();
        }

        private void RebuildDashboardLayout()
        {
            if (dashboardContent == null) return;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(dashboardContent);
        }

        private static void LogEvent(string action, string value, string result, string details)
        {
            ResearchLogger.Instance?.LogEvent(action, value, result, details);
        }
    }
}
