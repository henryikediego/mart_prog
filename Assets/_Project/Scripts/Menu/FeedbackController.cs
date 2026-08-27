using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MartProg.Core;

namespace MartProg.Menu
{
    /// <summary>
    /// Phase 39 — the Feedback screen. Backlog_Landing_Page_And_Navigation.md
    /// §5/§7 item 4, and the first screen since About/Landing that actually
    /// collects data rather than only presenting it.
    ///
    /// NOT A PORT. `main_menu.gd` has nothing resembling this — see
    /// About's and Landing's class comments for the same point made about
    /// the rest of the navigation shell. The content (1–5 star rating, one
    /// open feedback field) and the two open design questions (storage,
    /// placement) are resolved in Backlog §6.6.
    ///
    /// **Third button on the Landing Page, deliberately reopening Phase 38's
    /// two-button scope.** Phase 38 kept Landing to Start Learning and About
    /// specifically because Feedback's design questions were still open —
    /// now that they're answered (§6.6), the reason to exclude it is gone.
    /// It stays visually the quietest of the three: no fill at all, dim
    /// text, sitting below About rather than beside Start Learning. See
    /// LandingPageController's own comment and the Stage 39 doc §3 for the
    /// exact values.
    ///
    /// **Where the identity comes from.** See FeedbackLogger's class comment
    /// for the full reasoning — the short version is that this screen reads
    /// <see cref="ChangeModuleController.RegisteredStudentId"/> directly
    /// (Phase 38's public static property) rather than assuming a session is
    /// already running, because a participant can reach Feedback from the
    /// Landing Page before ever starting a module. If no ID is registered
    /// yet, the form itself is not shown — see <see cref="Show"/>.
    ///
    /// **A rating is required to submit; the written comment is not.** The
    /// star rating is the one field guaranteed to produce an analysable
    /// number from every submission; requiring a paragraph of text as well
    /// would cost submissions for no research gain. Standard shape for a
    /// feedback form generally, not something specific to this study.
    ///
    /// **No animation, matching Backlog §4.6's deferral.** The confirmation
    /// after Submit is a plain state swap (form hidden, thanks message
    /// shown), the same "no idle motion, but press feedback is real" stance
    /// already recorded for Landing Page.
    /// </summary>
    public class FeedbackController : MonoBehaviour
    {
        [Header("Panel")]
        [Tooltip("Root of the Feedback overlay. Hidden at Awake(), matching " +
                 "the Awake()-not-Start() lesson from Phase 38's landingPage " +
                 "fallback bug — see AboutController/LandingPageController.")]
        [SerializeField] private GameObject feedbackPanel;

        [SerializeField] private Button backButton;

        [Header("No-ID state")]
        [Tooltip("Shown INSTEAD of the form when no research ID is registered " +
                 "on this device yet (ChangeModuleController.RegisteredStudentId " +
                 "is empty) — a participant can reach this screen from the " +
                 "Landing Page before ever starting a module. Plain explanatory " +
                 "text; no button needed since Back (top-left) already returns " +
                 "to the Landing Page, where Start Learning registers the ID.")]
        [SerializeField] private GameObject noIdGroup;

        [Header("Form")]
        [SerializeField] private GameObject formGroup;

        [Tooltip("Exactly 5 Button - TextMeshPro objects, left to right, index " +
                 "0 = 1 star through index 4 = 5 stars. Each toggles its own " +
                 "label between filledStarColor and emptyStarColor; no sprites " +
                 "needed. See Stage 39 doc §3.3.")]
        [SerializeField] private List<Button> starButtons = new List<Button>();

        [SerializeField] private Color filledStarColor = new Color(1f, 0.84f, 0.29f); // gold
        [SerializeField] private Color emptyStarColor = new Color(1f, 1f, 1f, 0.25f);  // dim

        [Tooltip("Multi-line. Optional — a rating alone is a valid submission.")]
        [SerializeField] private TMP_InputField feedbackText;

        [Tooltip("Interactable only once a star is selected — see RefreshSubmitInteractable().")]
        [SerializeField] private Button submitButton;

        [Header("Confirmation")]
        [Tooltip("Shown in place of the form after a successful submit. Plain " +
                 "text (\"Thanks for your feedback!\"), no animation — see the " +
                 "class comment.")]
        [SerializeField] private GameObject thanksGroup;

        private int selectedRating = 0;

        private void Awake()
        {
            // Hidden by default, in Awake() rather than Start() — same
            // lifecycle discipline as About and Landing (Phase 38's
            // landingPage-unassigned bug is the reason this is now the
            // standing convention for every overlay panel in this project,
            // not an optional nicety).
            if (feedbackPanel != null) feedbackPanel.SetActive(false);
        }

        private void Start()
        {
            if (backButton != null) backButton.onClick.AddListener(Hide);
            if (submitButton != null) submitButton.onClick.AddListener(OnSubmitClicked);

            WireStarButtons();
            RefreshSubmitInteractable();
        }

        private void WireStarButtons()
        {
            if (starButtons == null) return;

            for (int i = 0; i < starButtons.Count; i++)
            {
                Button button = starButtons[i];
                if (button == null) continue;

                // Local copy of the loop index before the closure captures
                // it. In a `for` loop (unlike `foreach`, fixed since C# 5) a
                // lambda capturing the loop variable itself would see
                // whatever value `i` holds when the button is actually
                // clicked — the loop's FINAL value, i.e. every star would
                // report "5". `starValue` is a fresh local per iteration, so
                // each closure keeps its own value.
                int starValue = i + 1;
                button.onClick.AddListener(() => OnStarClicked(starValue));
            }
        }

        private void OnStarClicked(int value)
        {
            selectedRating = value;
            RefreshStars();
            RefreshSubmitInteractable();
        }

        private void RefreshStars()
        {
            if (starButtons == null) return;

            for (int i = 0; i < starButtons.Count; i++)
            {
                Button button = starButtons[i];
                if (button == null) continue;

                TMP_Text label = button.GetComponentInChildren<TMP_Text>();
                if (label == null) continue;

                label.color = (i < selectedRating) ? filledStarColor : emptyStarColor;
            }
        }

        private void RefreshSubmitInteractable()
        {
            if (submitButton != null) submitButton.interactable = selectedRating > 0;
        }

        /// <summary>
        /// Shown from LandingPageController's Feedback button. Decides
        /// between the form and the no-ID message before anything else runs
        /// — see the class comment and FeedbackLogger's for why this can't
        /// assume a session is already active.
        ///
        /// Explicitly sets all THREE mutually-exclusive groups
        /// (<see cref="noIdGroup"/>, <see cref="formGroup"/>,
        /// <see cref="thanksGroup"/>) on every call, rather than only two of
        /// them and leaving the third to whatever it happened to be before.
        /// The first version left `thanksGroup` untouched on the no-ID path
        /// — found on device: a participant with no registered ID who opened
        /// Feedback saw the no-ID message with "Thanks for your feedback!"
        /// still showing underneath it, left over from `thanksGroup` either
        /// being active by default in the Editor scene or from an earlier
        /// submission in the same session. Same root cause as the Phase 38
        /// `landingPage`-unassigned bug — a screen's visible state must be
        /// fully determined every time it's shown, never assumed left over
        /// from whatever ran before.
        /// </summary>
        public void Show()
        {
            bool hasId = !string.IsNullOrEmpty(ChangeModuleController.RegisteredStudentId);

            if (noIdGroup != null) noIdGroup.SetActive(!hasId);

            if (hasId)
            {
                ResetForm(); // also sets formGroup active, thanksGroup inactive
            }
            else
            {
                if (formGroup != null) formGroup.SetActive(false);
                if (thanksGroup != null) thanksGroup.SetActive(false);
            }

            if (feedbackPanel != null) feedbackPanel.SetActive(true);
        }

        public void Hide()
        {
            if (feedbackPanel != null) feedbackPanel.SetActive(false);
        }

        /// <summary>
        /// Puts the form back to a blank, ready-to-submit state. Called every
        /// time the screen opens with an ID present, so a participant who
        /// submits, presses Back, and reopens Feedback later in the session
        /// (a second module, a different day) gets a fresh form rather than
        /// their previous rating still selected or the old "Thanks" message
        /// still showing.
        /// </summary>
        private void ResetForm()
        {
            selectedRating = 0;
            RefreshStars();
            RefreshSubmitInteractable();

            if (feedbackText != null) feedbackText.text = "";
            if (formGroup != null) formGroup.SetActive(true);
            if (thanksGroup != null) thanksGroup.SetActive(false);
        }

        private void OnSubmitClicked()
        {
            // Defensive — submitButton.interactable already prevents this,
            // but a direct call (or a future second entry point) shouldn't
            // rely on a UI state to enforce a data rule.
            if (selectedRating <= 0) return;

            string text = feedbackText != null ? feedbackText.text : "";

            bool saved = FeedbackLogger.Instance != null &&
                         FeedbackLogger.Instance.LogFeedback(selectedRating, text);

            if (saved)
            {
                // A row in the main behavioural log too — not the text
                // itself (that stays only in the feedback file, see
                // FeedbackLogger's class comment on why the two are
                // separate), just the fact and the rating. Lets a researcher
                // see feedback engagement against session data without
                // opening the second file for that question alone. Same
                // two-sink shape as Student ID Registered (Phase 35):
                // PlayerPrefs/CSV row for the state, a LogEvent() row for
                // the moment it happened.
                ResearchLogger.Instance?.LogEvent(
                    "Feedback Submitted", selectedRating.ToString(), "Success",
                    "Participant submitted app feedback");

                if (formGroup != null) formGroup.SetActive(false);
                if (thanksGroup != null) thanksGroup.SetActive(true);
            }
            else
            {
                ResearchLogger.Instance?.LogEvent(
                    "Feedback Submitted", selectedRating.ToString(), "Failed",
                    "Feedback could not be written to file");

                // Form is left exactly as it was — nothing typed is lost,
                // and Submit stays available to try again.
            }
        }
    }
}
