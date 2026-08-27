using TMPro;
using UnityEngine;
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
                 "To show the bare number instead, delete the second 'Version ' " +
                 "so the last line reads just {0}.")]
        [SerializeField]
        private string versionFormat =
            "<size=118%><b>Version</b></size>\n<size=45%>\n</size><alpha=#CC>Version {0}";

        [Tooltip("Optional. The About panel's Scroll Rect (the ScrollView object). " +
                 "If assigned, the panel reopens scrolled back to the top rather " +
                 "than wherever the last reader left it — see Show(). Leave empty " +
                 "and the scroll position simply persists, which is Unity's default " +
                 "and harmless, just untidy.")]
        [SerializeField] private ScrollRect contentScroll;

        private void Start()
        {
            if (backButton != null) backButton.onClick.AddListener(Hide);

            if (aboutPanel != null) aboutPanel.SetActive(false);
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
    }
}
