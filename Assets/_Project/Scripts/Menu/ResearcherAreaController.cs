using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MartProg.Core;

namespace MartProg.Menu
{
    /// <summary>
    /// Phase 40 — the Researcher Area. Backlog_Landing_Page_And_Navigation.md
    /// §5/§7 item 6 ("Researcher area"), consolidating five functions that
    /// were previously scattered across the app: Export Log and Clear Log
    /// (AdminController, now retired — this screen replaces it entirely),
    /// Export Feedback and Clear Feedback (FeedbackLogger's own methods,
    /// written in Phase 39 specifically for this screen to call), and
    /// Release Registered ID (previously a hidden long press on the Student
    /// ID field itself — see ChangeModuleController's class comment, porting
    /// note 11). A sixth control, the Student ID Mode toggle, is new: Phase
    /// 34 made study-design mismatches catchable, but only at build time;
    /// this is the first on-device control for it.
    ///
    /// Phase 43 adds a seventh: the Module Order Enforcement toggle, the
    /// same runtime-adjustable shape as Student ID Mode but for
    /// ChangeModuleController.EffectiveEnforceModuleOrder — see that class's
    /// porting note 12 for why this exists and what it gates.
    ///
    /// NOT A PORT. Godot has nothing resembling a gated researcher screen —
    /// research_logger.gd's Export/Clear were plain buttons on the same row
    /// as every module control, reachable by anyone holding the device. Same
    /// "new feature, not a translation" category as Landing Page, About, and
    /// Feedback — see their class comments for the same point made about the
    /// rest of the navigation shell.
    ///
    /// **Reached by a hidden long press on About's version block**, not a
    /// Landing Page button — see AboutController's class comment for the
    /// full reasoning. In short: every other screen this project has built
    /// since Phase 38 wants to be found; this one deliberately does not, so
    /// it sits behind a gesture rather than in the open navigation.
    ///
    /// **Every destructive or identity-changing action needs two presses.**
    /// Clear Log, Clear Feedback, and Release Registered ID all use the same
    /// arm-then-confirm shape AdminController.OnClearLogClicked() settled on
    /// in Phase 32, after that phase's research export showed its
    /// dialog-based confirmation had never actually appeared in practice —
    /// see AdminController's own history (now retired; its logic lives here
    /// instead). This screen drops the dialog-based path entirely and keeps
    /// only the proven one: press once to arm, press again within the window
    /// to confirm, do nothing to cancel. Export Log and Export Feedback are
    /// not destructive, so each is a single press.
    ///
    /// **Its own status label, not DashboardController.** AdminController
    /// reported through DashboardController.Instance because it only ever
    /// ran while a module's dashboard was already on screen. This screen must
    /// work identically whether or not a module session is active — a
    /// researcher can reach it straight from About before ever starting one
    /// — so it carries a self-contained result label instead of borrowing
    /// dashboard UI that might not even be visible right now.
    /// </summary>
    public class ResearcherAreaController : MonoBehaviour
    {
        [Header("Panel")]
        [Tooltip("Root of the Researcher Area overlay. Hidden at Awake(), matching " +
                 "every other overlay panel in this project since Phase 38's " +
                 "landingPage-unassigned lesson — see AboutController / " +
                 "LandingPageController / FeedbackController.")]
        [SerializeField] private GameObject researcherAreaPanel;

        [SerializeField] private Button backButton;

        [Header("Wiring")]
        [Tooltip("Needed for Release Registered ID and the Student ID Mode toggle.")]
        [SerializeField] private ChangeModuleController changeModuleController;

        [Header("Research Log")]
        [SerializeField] private Button exportLogButton;
        [SerializeField] private Button clearLogButton;
        [Tooltip("Phase 48 — exports, then hands the export to Android's " +
                 "native Share Sheet via NativeShare.ShareFile(). See " +
                 "OnShareLogClicked() and NativeShare's class comment.")]
        [SerializeField] private Button shareLogButton;

        [Header("Feedback Log")]
        [SerializeField] private Button exportFeedbackButton;
        [SerializeField] private Button clearFeedbackButton;
        [Tooltip("Phase 48 — same shape as Share Log, against " +
                 "mart_prog_feedback.csv. See OnShareFeedbackClicked().")]
        [SerializeField] private Button shareFeedbackButton;

        [Header("Registered ID")]
        [Tooltip("Optional. Shows the currently registered ID, or a plain " +
                 "\"No ID registered\" message. Refreshed every time this " +
                 "screen opens and after a successful release — see Show() " +
                 "and RefreshRegisteredIdLabel().")]
        [SerializeField] private TMP_Text registeredIdLabel;
        [SerializeField] private Button releaseIdButton;

        [Header("Student ID Mode")]
        [Tooltip("Three buttons, one per ChangeModuleController.StudentIdMode value. " +
                 "The active mode's label is drawn in activeModeColor; the other two " +
                 "in inactiveModeColor — see RefreshModeButtons().")]
        [SerializeField] private Button acceptEitherButton;
        [SerializeField] private Button twoGroupsButton;
        [SerializeField] private Button singleGroupButton;

        [SerializeField] private Color activeModeColor = Color.white;
        [SerializeField] private Color inactiveModeColor = new Color(1f, 1f, 1f, 0.4f);

        [Header("Module Order Enforcement")]
        [Tooltip("Two buttons, mirroring the Student ID Mode row above but for a " +
                 "plain on/off (ChangeModuleController.EffectiveEnforceModuleOrder is a " +
                 "bool, not a 3-way enum). Reuses activeModeColor/inactiveModeColor — " +
                 "see RefreshEnforceOrderButtons().")]
        [SerializeField] private Button enforceOrderOnButton;
        [SerializeField] private Button enforceOrderOffButton;

        [Header("Status")]
        [Tooltip("Shared result label for every action on this screen — see the " +
                 "class comment for why this doesn't route through " +
                 "DashboardController the way AdminController used to.")]
        [SerializeField] private TMP_Text statusText;

        // Phase 32's arm-then-confirm window, reused as-is for every
        // destructive or identity-changing action here rather than each
        // button inventing its own timing — see AdminController's own
        // history (in its class comment) for why this exact shape, not a
        // dialog, is the one that actually works on device.
        private const float ArmSeconds = 6f;

        private float clearLogArmedUntil = -1f;
        private float clearFeedbackArmedUntil = -1f;
        private float releaseIdArmedUntil = -1f;

        private void Awake()
        {
            // Hidden by default, in Awake() — same lifecycle discipline as
            // every overlay panel in this project since Phase 38's
            // landingPage-unassigned bug made it the standing convention,
            // not an optional nicety.
            if (researcherAreaPanel != null) researcherAreaPanel.SetActive(false);
        }

        private void Start()
        {
            if (backButton != null) backButton.onClick.AddListener(Hide);

            if (exportLogButton != null) exportLogButton.onClick.AddListener(OnExportLogClicked);
            if (clearLogButton != null) clearLogButton.onClick.AddListener(OnClearLogClicked);
            if (shareLogButton != null) shareLogButton.onClick.AddListener(OnShareLogClicked);

            if (exportFeedbackButton != null) exportFeedbackButton.onClick.AddListener(OnExportFeedbackClicked);
            if (clearFeedbackButton != null) clearFeedbackButton.onClick.AddListener(OnClearFeedbackClicked);
            if (shareFeedbackButton != null) shareFeedbackButton.onClick.AddListener(OnShareFeedbackClicked);

            if (releaseIdButton != null) releaseIdButton.onClick.AddListener(OnReleaseIdClicked);

            if (acceptEitherButton != null)
                acceptEitherButton.onClick.AddListener(
                    () => OnModeClicked(ChangeModuleController.StudentIdMode.AcceptEither));

            if (twoGroupsButton != null)
                twoGroupsButton.onClick.AddListener(
                    () => OnModeClicked(ChangeModuleController.StudentIdMode.TwoGroups));

            if (singleGroupButton != null)
                singleGroupButton.onClick.AddListener(
                    () => OnModeClicked(ChangeModuleController.StudentIdMode.SingleGroup));

            if (enforceOrderOnButton != null)
                enforceOrderOnButton.onClick.AddListener(() => OnEnforceOrderClicked(true));

            if (enforceOrderOffButton != null)
                enforceOrderOffButton.onClick.AddListener(() => OnEnforceOrderClicked(false));
        }

        /// <summary>
        /// Called from AboutController's long-press reveal. Resets every
        /// armed state and clears the status label on each open — a
        /// researcher who armed Clear Log, backed out, and reopens this
        /// screen later (or a different researcher on a shared device)
        /// should never find a half-confirmed destructive action still
        /// waiting for a second press. Same "fully determine visible state
        /// on every Show(), never assume leftover state" lesson as
        /// FeedbackController.Show() — see its own class comment for the
        /// bug that lesson came from.
        /// </summary>
        public void Show()
        {
            clearLogArmedUntil = -1f;
            clearFeedbackArmedUntil = -1f;
            releaseIdArmedUntil = -1f;

            if (statusText != null) statusText.text = "";

            RefreshRegisteredIdLabel();
            RefreshModeButtons();
            RefreshEnforceOrderButtons();

            if (researcherAreaPanel != null) researcherAreaPanel.SetActive(true);
        }

        public void Hide()
        {
            if (researcherAreaPanel != null) researcherAreaPanel.SetActive(false);
        }

        // ---------------------------------------------------------------
        // Research Log
        // ---------------------------------------------------------------

        private void OnExportLogClicked()
        {
            ResearchLogger.Instance?.LogEvent(
                "Export Log", "", "Requested", "Researcher requested log export");

            string exportedPath = ResearchLogger.Instance != null
                ? ResearchLogger.Instance.ExportLogToDownloads()
                : "";

            if (!string.IsNullOrEmpty(exportedPath))
            {
                SetStatus("Export Log successful. Saved to: " + exportedPath);
                ResearchLogger.Instance?.LogEvent(
                    "Export Log", exportedPath, "Success", "Research log exported");
            }
            else
            {
                SetStatus("Export Log failed. The research log could not be exported.");
                ResearchLogger.Instance?.LogEvent(
                    "Export Log", "", "Failed", "Research log export failed");
            }
        }

        private void OnClearLogClicked()
        {
            if (Time.time <= clearLogArmedUntil)
            {
                clearLogArmedUntil = -1f;

                bool cleared = ResearchLogger.Instance != null && ResearchLogger.Instance.ClearLog();

                if (cleared)
                {
                    SetStatus("Clear Log successful. The research log has been cleared.");

                    // Wording matches the Phase 29 fix (F7) already applied
                    // to AdminController: ClearLog() only rewrites the
                    // header, it never calls StartNewSession(), so "cleared"
                    // is accurate and "restarted" would not be.
                    ResearchLogger.Instance?.LogEvent(
                        "Clear Log", "", "Success", "Research log cleared");
                }
                else
                {
                    SetStatus("Clear Log failed. The research log could not be cleared.");
                }

                return;
            }

            clearLogArmedUntil = Time.time + ArmSeconds;
            SetStatus("Clear Log — press again within " + ArmSeconds.ToString("0") +
                      " seconds to confirm. This erases the whole research log and cannot be undone.");
        }

        /// <summary>
        /// Phase 48. Single press, same non-destructive tier as Export Log —
        /// no arm-then-confirm needed, nothing here can lose data. Exports
        /// first (reusing ExportLogToDownloads() exactly as Export Log
        /// does) rather than sharing the live mart_prog_log.csv directly:
        /// the export is a stable, named snapshot that can't be mutated by
        /// a LogEvent() call landing mid-share, and reusing it means this
        /// button needs zero new export logic of its own.
        ///
        /// "Opened", not "Success" — NativeShare.ShareFile() only confirms
        /// Android's share chooser was launched, never that the researcher
        /// actually finished sending the file (no completion callback
        /// without startActivityForResult, deliberately not built — see
        /// NativeShare's class comment). Logged as its own honest category
        /// rather than reusing "Success", which would overclaim.
        /// </summary>
        private void OnShareLogClicked()
        {
            ResearchLogger.Instance?.LogEvent(
                "Share Log", "", "Requested", "Researcher requested to share the research log");

            string exportedPath = ResearchLogger.Instance != null
                ? ResearchLogger.Instance.ExportLogToDownloads()
                : "";

            if (string.IsNullOrEmpty(exportedPath))
            {
                SetStatus("Share Log failed. The research log could not be exported.");
                ResearchLogger.Instance?.LogEvent(
                    "Share Log", "", "Failed", "Research log export (for sharing) failed");
                return;
            }

            bool shared = NativeShare.ShareFile(exportedPath, "text/csv", "Share Research Log", out string shareError);

            if (!shared)
            {
                SetStatus("Share Log could not open the Share Sheet: " + shareError);
                ResearchLogger.Instance?.LogEvent(
                    "Share Log", exportedPath, "Failed", "NativeShare could not open the Share Sheet: " + shareError);
                return;
            }

            SetStatus("Research log ready — choose where to send it.");
            ResearchLogger.Instance?.LogEvent(
                "Share Log", exportedPath, "Opened", "Researcher opened the Share Sheet for the research log");
        }

        // ---------------------------------------------------------------
        // Feedback Log
        // ---------------------------------------------------------------

        private void OnExportFeedbackClicked()
        {
            string exportedPath = FeedbackLogger.Instance != null
                ? FeedbackLogger.Instance.ExportFeedbackToDownloads()
                : "";

            if (!string.IsNullOrEmpty(exportedPath))
            {
                SetStatus("Export Feedback successful. Saved to: " + exportedPath);
                ResearchLogger.Instance?.LogEvent(
                    "Export Feedback", exportedPath, "Success", "Feedback log exported");
            }
            else
            {
                SetStatus("Export Feedback failed. The feedback log could not be exported.");
                ResearchLogger.Instance?.LogEvent(
                    "Export Feedback", "", "Failed", "Feedback log export failed");
            }
        }

        private void OnClearFeedbackClicked()
        {
            if (Time.time <= clearFeedbackArmedUntil)
            {
                clearFeedbackArmedUntil = -1f;

                bool cleared = FeedbackLogger.Instance != null && FeedbackLogger.Instance.ClearFeedback();

                if (cleared)
                {
                    SetStatus("Clear Feedback successful. The feedback log has been cleared.");
                    ResearchLogger.Instance?.LogEvent(
                        "Clear Feedback", "", "Success", "Feedback log cleared");
                }
                else
                {
                    SetStatus("Clear Feedback failed. The feedback log could not be cleared.");
                }

                return;
            }

            clearFeedbackArmedUntil = Time.time + ArmSeconds;
            SetStatus("Clear Feedback — press again within " + ArmSeconds.ToString("0") +
                      " seconds to confirm. This erases every submitted comment and cannot be undone.");
        }

        /// <summary>
        /// Phase 48. Same shape as OnShareLogClicked() — see that method's
        /// comment for why export-then-share, and why "Opened" rather than
        /// "Success" — mirrored here against mart_prog_feedback.csv instead.
        /// </summary>
        private void OnShareFeedbackClicked()
        {
            ResearchLogger.Instance?.LogEvent(
                "Share Feedback", "", "Requested", "Researcher requested to share the feedback log");

            string exportedPath = FeedbackLogger.Instance != null
                ? FeedbackLogger.Instance.ExportFeedbackToDownloads()
                : "";

            if (string.IsNullOrEmpty(exportedPath))
            {
                SetStatus("Share Feedback failed. The feedback log could not be exported.");
                ResearchLogger.Instance?.LogEvent(
                    "Share Feedback", "", "Failed", "Feedback log export (for sharing) failed");
                return;
            }

            bool shared = NativeShare.ShareFile(exportedPath, "text/csv", "Share Feedback Log", out string shareError);

            if (!shared)
            {
                SetStatus("Share Feedback could not open the Share Sheet: " + shareError);
                ResearchLogger.Instance?.LogEvent(
                    "Share Feedback", exportedPath, "Failed", "NativeShare could not open the Share Sheet: " + shareError);
                return;
            }

            SetStatus("Feedback log ready — choose where to send it.");
            ResearchLogger.Instance?.LogEvent(
                "Share Feedback", exportedPath, "Opened", "Researcher opened the Share Sheet for the feedback log");
        }

        // ---------------------------------------------------------------
        // Registered ID
        // ---------------------------------------------------------------

        private void OnReleaseIdClicked()
        {
            if (changeModuleController == null) return;

            string current = ChangeModuleController.RegisteredStudentId;

            if (string.IsNullOrEmpty(current))
            {
                SetStatus("No ID is registered on this device.");
                return;
            }

            if (Time.time <= releaseIdArmedUntil)
            {
                releaseIdArmedUntil = -1f;

                // ReleaseRegisteredId() does its own logging and clears the
                // input field on ChangeModuleController's panel — see its
                // doc comment. The returned value is what was ACTUALLY
                // released, read before the PlayerPrefs key was deleted,
                // rather than re-reading RegisteredStudentId afterward (which
                // would already be "").
                string released = changeModuleController.ReleaseRegisteredId();
                RefreshRegisteredIdLabel();

                SetStatus(string.IsNullOrEmpty(released)
                    ? "No ID was registered on this device."
                    : "Research ID " + released + " released. The next participant can register a new one.");

                return;
            }

            releaseIdArmedUntil = Time.time + ArmSeconds;
            SetStatus("Release the registered ID " + current + "? Press Release ID again within " +
                      ArmSeconds.ToString("0") + " seconds to confirm.");
        }

        private void RefreshRegisteredIdLabel()
        {
            if (registeredIdLabel == null) return;

            string current = ChangeModuleController.RegisteredStudentId;
            registeredIdLabel.text = string.IsNullOrEmpty(current)
                ? "No ID registered"
                : "Registered ID: " + current;
        }

        // ---------------------------------------------------------------
        // Student ID Mode
        // ---------------------------------------------------------------

        private void OnModeClicked(ChangeModuleController.StudentIdMode mode)
        {
            if (changeModuleController == null) return;

            changeModuleController.SetStudentIdMode(mode);
            RefreshModeButtons();

            string displayName = ModeDisplayName(mode);
            SetStatus("Student ID Mode set to " + displayName + ".");

            ResearchLogger.Instance?.LogEvent(
                "Student ID Mode Changed", displayName, "Success",
                "Researcher changed the study's student ID mode");
        }

        /// <summary>
        /// Same idea as FeedbackController's star buttons — the active
        /// choice reads full-color, the other two dim, no sprites needed.
        /// </summary>
        private void RefreshModeButtons()
        {
            if (changeModuleController == null) return;

            ChangeModuleController.StudentIdMode current = changeModuleController.EffectiveStudentIdMode;

            SetModeButtonColor(acceptEitherButton, current == ChangeModuleController.StudentIdMode.AcceptEither);
            SetModeButtonColor(twoGroupsButton, current == ChangeModuleController.StudentIdMode.TwoGroups);
            SetModeButtonColor(singleGroupButton, current == ChangeModuleController.StudentIdMode.SingleGroup);
        }

        private void SetModeButtonColor(Button button, bool isActive)
        {
            if (button == null) return;

            TMP_Text label = button.GetComponentInChildren<TMP_Text>();
            if (label == null) return;

            label.color = isActive ? activeModeColor : inactiveModeColor;
        }

        private string ModeDisplayName(ChangeModuleController.StudentIdMode mode)
        {
            switch (mode)
            {
                case ChangeModuleController.StudentIdMode.TwoGroups:   return "Two Groups";
                case ChangeModuleController.StudentIdMode.SingleGroup: return "Single Group";
                default:                                               return "Accept Either";
            }
        }

        // ---------------------------------------------------------------
        // Module Order Enforcement (Phase 43)
        // ---------------------------------------------------------------

        private void OnEnforceOrderClicked(bool enforce)
        {
            if (changeModuleController == null) return;

            changeModuleController.SetEnforceModuleOrder(enforce);
            RefreshEnforceOrderButtons();

            string displayName = enforce ? "On" : "Off";
            SetStatus("Module Order Enforcement set to " + displayName + ".");

            ResearchLogger.Instance?.LogEvent(
                "Module Order Enforcement Changed", displayName, "Success",
                "Researcher changed whether modules must be attempted in order");
        }

        /// <summary>
        /// Same idea as RefreshModeButtons() — the active choice reads
        /// full-color, the other dims. Two buttons instead of three since
        /// EffectiveEnforceModuleOrder is a bool, not a 3-way enum.
        /// </summary>
        private void RefreshEnforceOrderButtons()
        {
            if (changeModuleController == null) return;

            bool current = changeModuleController.EffectiveEnforceModuleOrder;

            SetModeButtonColor(enforceOrderOnButton, current);
            SetModeButtonColor(enforceOrderOffButton, !current);
        }

        private void SetStatus(string message)
        {
            if (statusText != null) statusText.text = message;
        }
    }
}
