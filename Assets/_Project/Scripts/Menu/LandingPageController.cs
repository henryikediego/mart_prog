using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MartProg.Core;

namespace MartProg.Menu
{
    /// <summary>
    /// Phase 38 — the Landing Page. Item 3 of
    /// Backlog_Landing_Page_And_Navigation.md §5/§7, and the screen the
    /// researcher's original proposal was really about.
    ///
    /// **THE SHELL ONLY: Start Learning and About.** That scope is the whole
    /// point of taking this now rather than later. The proposal listed seven
    /// items; four of them (Feedback, Help, Continue Learning, module-order
    /// enforcement) each carry unresolved research-design questions, and two
    /// (Settings, Exit) were recommended against as participant-facing UI at
    /// all — Settings because per-participant differences in sound or text
    /// size are uncontrolled variables, Exit because an accidental exit
    /// mid-module loses a session and Android's own back gesture already
    /// exists. Building the frame first means each of those can arrive as its
    /// own commit against a navigation pattern that already works, which is
    /// the sequencing §5 argued for after three Safe Mode incidents made
    /// "one screen at a time" the rule.
    ///
    /// **Phase 39: a third button, Feedback.** Deliberately reopens the
    /// two-button scope above — reasonably, since Feedback was one of the
    /// four items excluded for having open design questions, and
    /// Backlog §6.6 answers both. Kept visually the quietest of the three
    /// (a faint `#242424` fill, barely a step up from the background, dim
    /// text) so it doesn't compete with Start Learning or read as equal in
    /// weight to About. First built fully transparent — corrected after
    /// seeing it on screen, where no fill at all read as loose caption text
    /// rather than a button; see Stage 39 doc §5. See FeedbackController's
    /// own class comment for why Feedback lives here rather than behind
    /// Help.
    ///
    /// **Phase 41: a fourth button, Help.** Backlog §5/§7 item 6, held back
    /// through Phases 38–40 on §3.1's intervention-vs-scaffolding question —
    /// resolved by §6.1 (no cohort boundary to protect) and §6.3 (the
    /// researcher's own framing accepted: scaffolding, not intervention).
    /// Given About's visual tier, not Feedback's quiet one — nothing in the
    /// backlog asks Help to read as secondary the way Feedback deliberately
    /// does, and a screen a confused participant needs to actually find
    /// should not be the hardest button to notice. See HelpController's own
    /// class comment for the content rule ("explains the APP, never the
    /// CONCEPT") and why this is the one Landing Page screen that logs its
    /// own opening.
    ///
    /// **NOT A PORT.** Godot's `main_menu.gd` in full is a student ID field, a
    /// dead programming-language dropdown, and a Start button that changes
    /// scene. There is no landing page, no About, no navigation. Every
    /// decision here was made rather than looked up — see the Backlog's §2.
    ///
    /// **What this changes about the app's shape.** Until now
    /// `ChangeModuleController.Start()` opened the module selector, which had
    /// been the first screen since Phase 27. It still hides the module world
    /// at boot — that part genuinely ports `_ready()` — but now shows this
    /// instead, and Start Learning calls back into
    /// `OpenSelectorFromLanding()`. Leaving `ChangeModuleController`'s
    /// `landingPage` field empty restores the old boot path exactly, which is
    /// why that fallback was kept rather than deleted.
    ///
    /// **Boot ordering is decided by ChangeModuleController, not here.** Unity
    /// does not guarantee `Start()` order between two MonoBehaviours in the
    /// same frame, and which screen the app opens on must not depend on it.
    /// This class only wires its own buttons in `Start()`; it is shown by an
    /// explicit `Show()` call from the script that already owned that
    /// decision. Same lifecycle discipline as the deferred-init convention in
    /// Migration Spec §5, applied to visibility rather than to singleton
    /// reads.
    ///
    /// **Nothing here is logged**, matching About (Phase 37). Opening the app
    /// is already implied by the session-start row `ResearchLogger` writes,
    /// and a "pressed Start Learning" row would record a step every
    /// participant takes exactly once — no variance, no covariate, nothing to
    /// analyse. Help (Phase 41) is different, on purpose: whether a
    /// participant opened it varies between people, which is precisely what
    /// makes it worth a row (Backlog §6.3) — see HelpController.Show().
    /// </summary>
    public class LandingPageController : MonoBehaviour
    {
        [Header("Panel")]
        [Tooltip("Root of the Landing Page overlay. Shown at boot by " +
                 "ChangeModuleController, hidden when the module selector opens.")]
        [SerializeField] private GameObject landingPanel;

        [Header("Buttons")]
        [Tooltip("The dominant call to action — opens the module selector.")]
        [SerializeField] private Button startLearningButton;

        [Tooltip("Opens the About screen (Phase 37). This is About's permanent " +
                 "home; the temporary button on ModuleSelectorPanel should be " +
                 "deleted from the scene.")]
        [SerializeField] private Button aboutButton;

        [Tooltip("Opens the Feedback screen (Phase 39). Third and visually " +
                 "quietest of the Landing Page's buttons — a faint #242424 " +
                 "fill, dim text. See Stage 39 doc §5 for the exact styling.")]
        [SerializeField] private Button feedbackButton;

        [Tooltip("Opens the Help screen (Phase 41). Same visual tier as " +
                 "About, not Feedback's quiet one — see the class comment.")]
        [SerializeField] private Button helpButton;

        [Header("Wiring")]
        [SerializeField] private ChangeModuleController changeModuleController;
        [SerializeField] private AboutController aboutController;
        [SerializeField] private FeedbackController feedbackController;
        [SerializeField] private HelpController helpController;

        [Header("Optional")]
        [Tooltip("Optional. Shows \"Signed in as s1c\" when an ID is registered on " +
                 "this device (Phase 35), and is emptied when none is.\n\n" +
                 "Worth wiring: without it, a returning participant sees a generic " +
                 "welcome screen with no confirmation that the device still knows " +
                 "who they are — and the ID field on the next screen is greyed out, " +
                 "which reads as broken rather than as remembered.")]
        [SerializeField] private TMP_Text signedInLabel;

        [Tooltip("Rich-text wrapper for the signed-in line. {0} is replaced with the " +
                 "registered ID.\n\n" +
                 "Smaller and dimmer than the tagline on purpose: this is status, " +
                 "not content. At full size and brightness it competes with the " +
                 "tagline directly above it, and the eye has no reason to prefer " +
                 "either.\n\n" +
                 "Tags follow the same system as the About screen — see " +
                 "Stage37_About_Copy_Styled.txt. Remember <alpha> has no closing " +
                 "tag.")]
        [SerializeField]
        private string signedInFormat = "<size=85%><alpha=#99>Signed in as {0}";

        private void Awake()
        {
            // Hidden by default, and specifically in Awake() rather than
            // Start(): Unity runs EVERY Awake() before ANY Start(), so this
            // always lands before ChangeModuleController.Start() decides
            // whether to Show() this page. Hiding in Start() instead would be
            // a coin toss between "hidden then shown" and "shown then hidden".
            //
            // This is also what makes the landingPage-unassigned fallback
            // work. Found by testing it (Stage 38 §8 step 8): with the field
            // empty, ShowSelector()'s `if (landingPage != null)
            // landingPage.Hide()` cannot fire, so nothing hid LandingPanel —
            // it stayed active on top, the selector opened invisibly behind
            // it, and Start Learning looked dead. Defaulting to hidden means
            // "nobody calls Show()" resolves to "the page never appears",
            // which is what a fallback to the pre-Phase-38 boot path needs.
            if (landingPanel != null) landingPanel.SetActive(false);
        }

        private void Start()
        {
            if (startLearningButton != null)
                startLearningButton.onClick.AddListener(OnStartLearningClicked);

            if (aboutButton != null)
                aboutButton.onClick.AddListener(OnAboutClicked);

            if (feedbackButton != null)
                feedbackButton.onClick.AddListener(OnFeedbackClicked);

            if (helpButton != null)
                helpButton.onClick.AddListener(OnHelpClicked);
        }

        public void Show()
        {
            RefreshSignedInLabel();

            if (landingPanel != null) landingPanel.SetActive(true);
        }

        public void Hide()
        {
            if (landingPanel != null) landingPanel.SetActive(false);
        }

        public void OnStartLearningClicked()
        {
            if (changeModuleController == null)
            {
                Debug.LogWarning(
                    "LandingPageController: Start Learning was pressed but no " +
                    "ChangeModuleController is assigned, so there is nothing to open.");
                return;
            }

            // Hide() is not called here — OpenSelectorFromLanding() runs
            // ShowSelector(), which hides this page itself. Doing it in one
            // place means the Change Module path mid-session behaves the same
            // way as this one, rather than the two drifting apart.
            changeModuleController.OpenSelectorFromLanding();
        }

        public void OnAboutClicked()
        {
            // About draws on top and hides nothing underneath it, so the
            // landing page deliberately stays active behind it — its Back
            // button returns here with no state to restore. See
            // AboutController's class comment.
            if (aboutController != null) aboutController.Show();
        }

        public void OnFeedbackClicked()
        {
            // Same draws-on-top, hides-nothing pattern as About — see
            // FeedbackController's class comment. Its own Show() decides
            // whether to present the form or the no-ID message.
            if (feedbackController != null) feedbackController.Show();
        }

        public void OnHelpClicked()
        {
            // Same draws-on-top, hides-nothing pattern as About — see
            // HelpController's class comment. Show() also logs a "Help
            // Opened" row, unlike every other button on this screen.
            if (helpController != null) helpController.Show();
        }

        private void RefreshSignedInLabel()
        {
            if (signedInLabel == null) return;

            // Read through ChangeModuleController rather than reading
            // PlayerPrefs directly: it owns the key, and a second copy of that
            // string is exactly how two screens end up disagreeing about who
            // is signed in.
            string registered = ChangeModuleController.RegisteredStudentId;

            // Replace() rather than string.Format(), same reasoning as
            // AboutController's version block: the format string is
            // Inspector-editable and full of rich-text tags, and one stray
            // brace typed into it would throw at runtime on a screen that is
            // otherwise incapable of failing.
            signedInLabel.text = string.IsNullOrEmpty(registered)
                ? ""
                : signedInFormat.Replace("{0}", registered);
        }
    }
}
