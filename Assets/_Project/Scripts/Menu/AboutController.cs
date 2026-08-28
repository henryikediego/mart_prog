using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MartProg.Menu
{
    /// <summary>
    /// Phase 37 — the About screen. Backlog_Landing_Page_And_Navigation.md §5/§7
    /// item 2: the first of the new navigation-shell screens, chosen specifically
    /// because it is static, has no data implications, and is the cheapest place
    /// to establish the Back/Home pattern the rest of the landing-page work
    /// (Feedback, the Researcher area, Help) will reuse.
    ///
    /// THIS IS NEW CONTENT, NOT A PORT. Godot has nothing to check this against —
    /// main_menu.gd in full is a student ID field, a dead language dropdown, and
    /// a Start button (Backlog §2). Every word on this screen was written for
    /// this phase, from the researcher's own spec in Backlog §1: app name,
    /// purpose, objectives, target users, version, developer/researcher
    /// information. See the Phase 37 stage doc for the suggested copy — it lives
    /// in the scene as plain TextMeshPro text, not in this script, so editing it
    /// later needs no code change.
    ///
    /// TEMPORARY ENTRY POINT. There is no Landing Page yet — ModuleSelectorPanel
    /// (Phase 20/27) is still the app's real first screen, shown from
    /// ChangeModuleController.Start() before anything else. This phase adds one
    /// small "About" button directly onto that panel so the screen can be built,
    /// wired, and tested now rather than waiting on the Landing Page shell
    /// (Backlog item 3, next). When that shell exists, its own About button
    /// takes over and the temporary one on ModuleSelectorPanel comes back out —
    /// nothing about AboutController changes when that happens, only which
    /// button calls Show(). Flagged with "TEMPORARY" at both ends of the wiring
    /// (here and in ChangeModuleController's own Inspector, per the stage doc)
    /// so it isn't mistaken for the permanent home.
    ///
    /// ARCHITECTURE: a full-screen overlay Panel inside the one Learning.unity
    /// scene, not a second Unity Scene. This project has used exactly one scene
    /// since Phase 8 (Migration Spec §3) — AR Foundation's session/plane/camera
    /// subsystems live there, and every other secondary screen so far
    /// (ModuleSelectorPanel, ClearLogConfirmDialog) is the same pattern: a
    /// hidden-by-default GameObject toggled active/inactive, drawn on top of
    /// whatever is currently showing. About follows it exactly, which is also
    /// why it needs no "what to restore on Back" logic — it never hides
    /// anything underneath it the way ModuleSelectorPanel does; it only draws
    /// over the top, so Hide() just turns itself off and whatever was already
    /// there is still there.
    ///
    /// NOT logged. Unlike Help (Backlog §6.3 — logging whether a participant
    /// opened it matters because Help sits next to the intervention/scaffolding
    /// question), About carries no research-validity weight either way
    /// (Backlog §3.7: "the least risky item on the list"). Considered and
    /// deliberately left out, not an oversight.
    ///
    /// **Phase 40: the version block is also a hidden entry point.** A long
    /// press on it (3 seconds, twice — see WireVersionLongPress()) opens the
    /// new Researcher Area, the gated screen consolidating Export/Clear Log,
    /// Export/Clear Feedback, Release Registered ID, and the Student ID Mode
    /// toggle. Chosen deliberately to mirror Android's own "tap the build
    /// number to unlock developer options" convention — a place on a settings-
    /// adjacent screen that a participant has no reason to touch repeatedly,
    /// but a researcher who knows can reach in seconds. See
    /// ResearcherAreaController's class comment for the rest of that screen,
    /// and ChangeModuleController's class comment (porting note 11) for why
    /// this replaces what used to be a long press on the Student ID field.
    /// </summary>
    public class AboutController : MonoBehaviour
    {
        [Header("Panel")]
        [Tooltip("Root of the About overlay. Hidden at Start(), shown by Show(), " +
                 "hidden again by Hide() (wired to the Back button).")]
        [SerializeField] private GameObject aboutPanel;

        [SerializeField] private Button backButton;

        [Header("Content")]
        [Tooltip("The one line of content this script actually sets. Everything " +
                 "else on the panel (app name, purpose, objectives, target users, " +
                 "researcher information) is plain static TextMeshPro text set " +
                 "directly in the Editor — see the Phase 37 stage doc for the " +
                 "suggested copy. This field is the exception because it must stay " +
                 "correct automatically as the build version changes; hand-typing " +
                 "it would silently go stale exactly the way bundleVersion itself " +
                 "did for thirty-five phases before Phase 36.")]
        [SerializeField] private TMP_Text versionText;

        [TextArea(3, 6)]
        [Tooltip("How the version block is written, including its heading. {0} is " +
                 "replaced with Application.version.\n\n" +
                 "The heading lives HERE rather than in a separate scene object " +
                 "because this whole block's text is rewritten on every open — a " +
                 "heading typed into the scene above it would sit at section " +
                 "spacing rather than heading spacing, and could drift out of step " +
                 "with the value it labels.\n\n" +
                 "The tags match the system in Stage37_About_Copy_Styled.txt: " +
                 "<size=118%><b> for a heading, <size=45%> around a blank line for " +
                 "a half-height gap, <alpha=#CC> for body text. Change them here if " +
                 "you change them there, so this block doesn't become the one that " +
                 "looks different.\n\n" +
                 "NOTE <alpha> has NO closing tag in TextMeshPro — it is a state " +
                 "setter, so </alpha> is not a tag and TMP prints it literally. " +
                 "Restore full brightness with <alpha=#FF>, or just let the block " +
                 "end; alpha resets at the start of every text object.\n\n" +
                 "To show the bare number instead, delete the second 'Version ' " +
                 "so the last line reads just {0}.")]
        [SerializeField]
        private string versionFormat =
            "<size=118%><b><color=#8AB4F8>Version</color></b></size>\n" +
            "<size=45%>\n</size><alpha=#CC>Version {0}";

        [Tooltip("Optional. The About panel's Scroll Rect (the ScrollView object). " +
                 "If assigned, the panel reopens scrolled back to the top rather " +
                 "than wherever the last reader left it — see Show(). Leave empty " +
                 "and the scroll position simply persists, which is Unity's default " +
                 "and harmless, just untidy.")]
        [SerializeField] private ScrollRect contentScroll;

        [Header("Researcher Area (Phase 40)")]
        [Tooltip("Opened by a long press on the version block below — see " +
                 "WireVersionLongPress(). Leave unassigned to disable the reveal " +
                 "gesture entirely; the version block then behaves as plain text, " +
                 "exactly as it did before Phase 40.")]
        [SerializeField] private ResearcherAreaController researcherAreaController;

        [Tooltip("Seconds to hold the version block to arm the reveal gesture. " +
                 "Mirrors the exact timing the Student ID release long-press used " +
                 "before Phase 40 moved it to a plain button — see " +
                 "ChangeModuleController's class comment, porting note 11.")]
        [SerializeField] private float researcherAreaHoldSeconds = 3f;

        private float versionPressStartedAt = -1f;
        private float researcherAreaArmedUntil = -1f;

        private void Awake()
        {
            // Hidden by default, in Awake() rather than Start() — Unity runs
            // every Awake() before any Start(), so no other script's Start()
            // can be racing this one to decide what is visible at boot.
            // Moved here in Phase 38 after the equivalent line on the Landing
            // Page turned out to be order-dependent; About was never actually
            // bitten, but the two screens should not differ in something this
            // easy to get wrong.
            if (aboutPanel != null) aboutPanel.SetActive(false);
        }

        private void Start()
        {
            if (backButton != null) backButton.onClick.AddListener(Hide);

            WireVersionLongPress();
        }

        /// <summary>
        /// Shown from the temporary button on ModuleSelectorPanel today; from
        /// the Landing Page's own About button once that exists.
        /// </summary>
        public void Show()
        {
            if (versionText != null)
            {
                // Phase 36's Version column reads the same Application.version.
                // Deliberately un-set as of this phase (researcher's decision:
                // leave it until the first official launch, see Stage36 §3) —
                // this will read 0.1.0 until then, and update itself with no
                // further change once it's set.
                //
                // Replace() rather than string.Format(): the format string is
                // Inspector-editable and full of rich-text tags, and one stray
                // brace typed into it would make string.Format throw at runtime
                // on a screen that is otherwise incapable of failing. Replace
                // cannot throw, and a typo'd placeholder shows up as visible
                // text rather than an exception.
                versionText.text = versionFormat.Replace("{0}", Application.version);
            }

            if (aboutPanel != null) aboutPanel.SetActive(true);

            // A ScrollRect keeps its scroll position across being disabled and
            // re-enabled, so without this a student who scrolled to the bottom,
            // pressed Back, and opened About again would land mid-document with
            // no indication there is anything above — reading as a broken screen
            // rather than a scrolled one.
            //
            // ForceUpdateCanvases() first: the Content Size Fitter may not have
            // recomputed the content height yet on the frame the panel is
            // enabled, and setting a normalised position against a stale height
            // lands in the wrong place. Same force-rebuild-before-measuring rule
            // every dashboard update in this project follows (Migration Spec §5).
            if (contentScroll != null)
            {
                Canvas.ForceUpdateCanvases();
                contentScroll.verticalNormalizedPosition = 1f;
            }
        }

        public void Hide()
        {
            if (aboutPanel != null) aboutPanel.SetActive(false);
        }

        /// <summary>
        /// Phase 40: a hidden long-press reveal for the Researcher Area, on
        /// the version block only — mirrors Android's own "tap the build
        /// number" convention for reaching developer options, adapted to this
        /// project's existing long-press/arm-then-confirm shape (the Student
        /// ID release gesture used the same 3-second/twice timing on a
        /// different field before Phase 40 moved it to a plain button — see
        /// ChangeModuleController's class comment, porting note 11).
        ///
        /// Deliberately gives NO visible feedback at either step — no armed
        /// message, no on-screen state change until the panel itself opens.
        /// The participant-facing gesture this mirrors showed "hold again to
        /// confirm" because it was meant to be openly known and used by a
        /// trained researcher with a participant present. This one is the
        /// opposite: it exists so the entry point stays quietly tucked away
        /// rather than announcing itself to whoever is holding the device. A
        /// Debug.Log marks each step for a developer reading the device log;
        /// nothing reaches the screen.
        ///
        /// Wired at runtime through an EventTrigger rather than in the scene,
        /// same approach as the gesture it replaces — versionText is plain
        /// TMP_Text with no Selectable at all, so EventTrigger is actually
        /// the only way to give it pointer handling, not just the safer one.
        /// </summary>
        private void WireVersionLongPress()
        {
            if (researcherAreaController == null) return;
            if (versionText == null) return;

            EventTrigger trigger = versionText.gameObject.GetComponent<EventTrigger>();
            if (trigger == null) trigger = versionText.gameObject.AddComponent<EventTrigger>();

            // Start() runs once per GameObject ACTIVATION. AboutController
            // sits on an object that is active for the app's whole lifetime
            // (only aboutPanel toggles), so this only ever runs once in
            // practice — cleared anyway as cheap insurance against the exact
            // class of bug the Phase 27 button grid had, matching the same
            // defensive line in every other EventTrigger wiring in this
            // project.
            trigger.triggers.Clear();

            EventTrigger.Entry down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            down.callback.AddListener(_ => versionPressStartedAt = Time.unscaledTime);
            trigger.triggers.Add(down);

            EventTrigger.Entry up = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            up.callback.AddListener(_ =>
            {
                float held = versionPressStartedAt < 0f ? 0f : Time.unscaledTime - versionPressStartedAt;
                versionPressStartedAt = -1f;

                if (held >= researcherAreaHoldSeconds) OnVersionLongPress();
            });
            trigger.triggers.Add(up);
        }

        private void OnVersionLongPress()
        {
            if (researcherAreaController == null) return;

            if (Time.unscaledTime <= researcherAreaArmedUntil)
            {
                researcherAreaArmedUntil = -1f;
                Debug.Log("AboutController: Researcher Area reveal confirmed.");
                researcherAreaController.Show();
                return;
            }

            researcherAreaArmedUntil = Time.unscaledTime + 10f;
            Debug.Log("AboutController: Researcher Area reveal armed — hold the version block again to confirm.");
        }
    }
}
