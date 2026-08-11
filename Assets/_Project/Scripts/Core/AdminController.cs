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

            if (!string.IsNullOrEmpty(exportedPath))
            {
                SetFeedback(
                    "Export Log successful.",
                    $"The research log was copied to: {exportedPath}"
                );
            }
            else
            {
                SetFeedback(
                    "Export Log failed.",
                    "The research log could not be exported."
                );
            }
        }

        private void OnClearLogClicked()
        {
            // Mirrors Godot's clear_log_confirm_dialog.popup_centered() — Clear
            // Log never fires directly from this click, only the confirmation.
            if (clearLogConfirmDialog != null)
            {
                clearLogConfirmDialog.SetActive(true);
            }
            else
            {
                // No dialog wired: fail safe by not clearing anything, rather
                // than silently skipping the confirmation step.
                Debug.LogWarning("AdminController: Clear Log Confirm Dialog is not assigned; Clear Log was not performed.");
            }
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

                LogEvent("Clear Log", "", "Success", "Research log cleared and restarted");
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
