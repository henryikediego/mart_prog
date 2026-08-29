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
    /// **Phase 42: a text-size control, not a button.** Backlog §4.4/§3.2 —
    /// the one exception to "researcher sets it, participant doesn't see
    /// it": text size is inclusion, not a confound. Deliberately NOT a
    /// Settings menu (§3.2 rejects that shape outright) and deliberately
    /// NOT live-adjustable mid-module — every dashboard InformationPanel and
    /// every AR-space label is set once, at creation time, with no
    /// re-render hook, so a live control would need all of them to start
    /// listening for changes instead of just reading a value once. This
    /// phase wires the dashboard half only (DashboardController.SetTextScale(),
    /// which reaches all six InformationPanels); the AR labels are their own
    /// separate, larger follow-up — see InformationPanel.SetTextScale()'s
    /// comment for why.
    ///
    /// **The scale is applied twice, on purpose, and neither call is the
    /// "real" one.** The choice, once made, is written straight to
    /// `PlayerPrefs` (both here and in `OnTextScaleClicked()`), and
    /// `DashboardController.Initialize()` reads it and self-applies the
    /// instant its panels are wired — regardless of when that turns out to
    /// be relative to anything on this screen. `OnStartLearningClicked()`
    /// also pushes it explicitly, as a live re-apply for a participant who
    /// changes their mind and presses Start Learning a second time. The
    /// first version of this only pushed from the click, on the assumption
    /// that a user-triggered click necessarily lands after
    /// `DashboardContainer.Start()` has already run — a real Play-mode test
    /// proved that assumption wrong (see `DashboardController.SetTextScale()`'s
    /// comment), which is why the pull-based fix exists at all.
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
    /// **Phase 44: a fifth button, Continue Learning — the one from the
    /// original mockup that was conditional from the start ("if progress
    /// tracking is implemented").** Hidden by default and shown only once
    /// `ChangeModuleController.CanContinueLearning()` says there's somewhere
    /// to go — no registered participant, or nothing left incomplete, and it
    /// stays off. Unlike every other button here, this one does not open an
    /// overlay on top of the Landing Page: `ChangeModuleController
    /// .ContinueLearning()` goes straight into the target module, hiding
    /// this page the same way `OpenSelectorFromLanding()` already does for
    /// Start Learning. See `ChangeModuleController`'s class comment, porting
    /// note 13, for what "next" means and how Memory resumes to a specific
    /// activity rather than just reopening.
    ///
    /// **Nothing here is logged**, matching About (Phase 37). Opening the app
    /// is already implied by the session-start row `ResearchLogger` writes,
    /// and a "pressed Start Learning" row would record a step every
    /// participant takes exactly once — no variance, no covariate, nothing to
    /// analyse. Help (Phase 41) is different, on purpose: whether a
    /// participant opened it varies between people, which is precisely what
    /// makes it worth a row (Backlog §6.3) — see HelpController.Show().
    ///
    /// **Phase 45: the AR-label half Phase 42 deferred.** That phase's note
    /// on `InformationPanel.cs` named eight files (Array, ArrayCell, Stack,
    /// Queue, LinkedList, Loop, Function, Selection) that each set
    /// `tmp.fontSize` directly at creation with no shared hook; Memory's own
    /// `BuildLabel()` does the same and is included here too. Unlike the
    /// dashboard panels, none of those labels are long-lived — every one of
    /// them is destroyed and rebuilt from scratch on every full redraw
    /// (`DrawStack()`, `DrawArray()`, and their siblings all `ClearContainer()`
    /// + recreate before drawing a single marker), and `EnterModule()` runs
    /// that reset-and-redraw on every entry, including a return visit. So
    /// there is no re-render hook to build: each module's own fontSize
    /// assignment just multiplies by `CurrentTextScale` below, and whatever
    /// is current at the moment of that redraw is what gets drawn — the same
    /// value this class has already been writing to `PlayerPrefs` since
    /// Phase 42, reused rather than duplicated under a second key. The
    /// Landing Page's own text-scale button is unreachable while a module is
    /// open (it is hidden behind the module selector the moment one opens),
    /// so "does it update live while a module is on screen" was never a real
    /// case to design for — the only way to change it is to back out to this
    /// screen first, which is exactly the moment every AR label the
    /// participant next sees gets rebuilt anyway.
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

        [Tooltip("Continue Learning (Phase 44). Jumps straight into the next " +
                 "incomplete module — no selector, no dropdown — or resumes " +
                 "Memory to its next incomplete activity if that's the module. " +
                 "Hidden whenever ChangeModuleController.CanContinueLearning() " +
                 "is false: no registered ID, or nothing left to continue. " +
                 "Refreshed every time this page shows, in RefreshContinueLearningButton().")]
        [SerializeField] private Button continueLearningButton;

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

        [Header("Accessibility (Phase 42)")]
        [Tooltip("Cycles Normal -> Large -> Extra Large -> Normal. Backlog " +
                 "§4.4 — dashboard text only this phase, see the class " +
                 "comment. Not a Settings menu: one button, one label, " +
                 "nothing else.")]
        [SerializeField] private Button textScaleButton;

        [Tooltip("Shows the current choice, e.g. \"Text Size: Normal\". " +
                 "Refreshed on load from the saved PlayerPrefs value and " +
                 "every time textScaleButton is pressed.")]
        [SerializeField] private TMP_Text textScaleLabel;

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

        // ----- Accessibility text scale (Phase 42) -----
        //
        // Three fixed, named steps rather than a continuous slider —
        // easier to reason about, easier to log as a discrete category
        // (see OnTextScaleClicked()), and matches the common
        // Normal/Large/Extra-Large accessibility pattern rather than
        // inventing a new one. 1.0 is the prefab's own authored size, so
        // "Normal" is always a true no-op.
        private static readonly float[] TextScaleValues = { 1.0f, 1.15f, 1.3f };
        private static readonly string[] TextScaleNames = { "Normal", "Large", "Extra Large" };
        private const string TextScaleIndexPrefsKey = "MartProg_TextScaleIndex";

        // Separate from the index above on purpose: DashboardController
        // reads this one directly (PlayerPrefs.GetFloat) to self-apply the
        // moment its Initialize() runs, with no reason to know about
        // Normal/Large/Extra-Large naming or which index that maps to —
        // just the resulting multiplier. Kept in lockstep with the index
        // everywhere the index is written (Start() and OnTextScaleClicked()).
        private const string TextScaleFloatPrefsKey = "MartProg_TextScale";

        // Phase 45: the same pull this class already gives DashboardController
        // (ApplySavedTextScale() reads TextScaleFloatPrefsKey directly), now
        // exposed as a public static so the eight AR-label module files can
        // read it too, at the exact point each of them assigns tmp.fontSize.
        // A static rather than an instance member on purpose — a module's
        // helper method has no reference to this MonoBehaviour and has no
        // reason to acquire one just to read one float; every other
        // app-wide setting this project reads from an arbitrary call site
        // (RegisteredStudentId, EffectiveEnforceModuleOrder) already uses
        // this same "static property, backed directly by PlayerPrefs" shape.
        // Defaults to 1.0 (Normal), matching ApplySavedTextScale()'s own
        // default for a fresh install where the key has never been written.
        public static float CurrentTextScale
        {
            get { return PlayerPrefs.GetFloat(TextScaleFloatPrefsKey, 1.0f); }
        }

        private int currentTextScaleIndex = 0;

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

            if (continueLearningButton != null)
                continueLearningButton.onClick.AddListener(OnContinueLearningClicked);

            if (aboutButton != null)
                aboutButton.onClick.AddListener(OnAboutClicked);

            if (feedbackButton != null)
                feedbackButton.onClick.AddListener(OnFeedbackClicked);

            if (helpButton != null)
                helpButton.onClick.AddListener(OnHelpClicked);

            if (textScaleButton != null)
                textScaleButton.onClick.AddListener(OnTextScaleClicked);

            // Loaded here rather than Awake(): PlayerPrefs reads are cheap
            // and synchronous either way, but Start() is where every other
            // one-time setup in this class already lives, and there's no
            // reason for this one to be the exception.
            currentTextScaleIndex = Mathf.Clamp(
                PlayerPrefs.GetInt(TextScaleIndexPrefsKey, 0), 0, TextScaleValues.Length - 1);
            RefreshTextScaleLabel();

            // Written here too, not just in OnTextScaleClicked(): on a
            // fresh install this key has never been set, and
            // DashboardController.ApplySavedTextScale()'s own 1.0 default
            // already matches index 0 — but writing it explicitly keeps
            // the float authoritative from the very first frame rather
            // than relying on two independent defaults happening to agree.
            PlayerPrefs.SetFloat(TextScaleFloatPrefsKey, TextScaleValues[currentTextScaleIndex]);
        }

        public void Show()
        {
            RefreshSignedInLabel();
            RefreshContinueLearningButton();

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

            // CORRECTED after the first real test: this used to be the ONLY
            // place the accessibility scale got applied, on the theory that
            // a user-triggered click necessarily lands after
            // DashboardContainer.Start() has already called
            // DashboardController.Instance.Initialize(). That theory was
            // wrong — a real Play-mode run hit
            // "SetTextScale() called before Initialize() finished wiring
            // panels" on this exact click, meaning whatever activates the
            // Dashboard hierarchy runs later than this one. The real fix is
            // on DashboardController's side now: Initialize() itself pulls
            // the saved scale and self-applies the moment it's wired,
            // regardless of ordering (see its comment). This call is no
            // longer load-bearing for the first Start Learning press — it's
            // a live re-apply for the case where Initialize() already ran
            // and the participant changed their choice before pressing
            // Start Learning a second time.
            DashboardController.Instance?.SetTextScale(TextScaleValues[currentTextScaleIndex]);

            // Hide() is not called here — OpenSelectorFromLanding() runs
            // ShowSelector(), which hides this page itself. Doing it in one
            // place means the Change Module path mid-session behaves the same
            // way as this one, rather than the two drifting apart.
            changeModuleController.OpenSelectorFromLanding();
        }

        /// <summary>
        /// Phase 44 — Continue Learning. Skips the selector entirely:
        /// ChangeModuleController.ContinueLearning() already knows which
        /// module is next and hides this page itself, the same "one place
        /// decides visibility" discipline OnStartLearningClicked() follows
        /// for OpenSelectorFromLanding(). Only reachable when
        /// RefreshContinueLearningButton() left the button active, but this
        /// method does not assume that — ContinueLearning() itself
        /// re-checks and no-ops safely either way.
        /// </summary>
        public void OnContinueLearningClicked()
        {
            if (changeModuleController == null)
            {
                Debug.LogWarning(
                    "LandingPageController: Continue Learning was pressed but no " +
                    "ChangeModuleController is assigned, so there is nothing to open.");
                return;
            }

            // Same live re-apply as OnStartLearningClicked() above, and for
            // the same reason — see its own comment. Not load-bearing: the
            // target module's own DashboardController.Initialize() will pull
            // the saved scale regardless of whether this call lands before
            // or after it.
            DashboardController.Instance?.SetTextScale(TextScaleValues[currentTextScaleIndex]);

            changeModuleController.ContinueLearning();
        }

        /// <summary>
        /// Shows or hides Continue Learning based on whether there's
        /// anything to continue to right now — re-evaluated every time this
        /// page is about to display (see Show()), never cached, since
        /// progress can change from anywhere in a session (a quiz just
        /// completed, an ID just released). Hidden rather than shown-and-
        /// disabled when there's nothing to continue to, matching the
        /// original proposal's own framing: "Continue Learning — resume
        /// where the user stopped, if progress is stored."
        /// </summary>
        private void RefreshContinueLearningButton()
        {
            if (continueLearningButton == null) return;

            bool canContinue = changeModuleController != null && changeModuleController.CanContinueLearning();
            continueLearningButton.gameObject.SetActive(canContinue);
        }

        public void OnTextScaleClicked()
        {
            currentTextScaleIndex = (currentTextScaleIndex + 1) % TextScaleValues.Length;
            PlayerPrefs.SetInt(TextScaleIndexPrefsKey, currentTextScaleIndex);
            PlayerPrefs.SetFloat(TextScaleFloatPrefsKey, TextScaleValues[currentTextScaleIndex]);
            RefreshTextScaleLabel();

            // Logged for the same reason Help logs its own opening (see
            // HelpController.Show()): this is a between-participants choice,
            // not a step everyone takes identically, which is what makes it
            // worth a row rather than noise.
            ResearchLogger.Instance?.LogEvent(
                "Text Size Changed",
                TextScaleNames[currentTextScaleIndex],
                "Selected",
                "Participant set the dashboard text size before starting.");
        }

        private void RefreshTextScaleLabel()
        {
            if (textScaleLabel == null) return;

            textScaleLabel.text = "Text Size: " + TextScaleNames[currentTextScaleIndex];
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
