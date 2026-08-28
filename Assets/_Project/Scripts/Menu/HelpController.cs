using UnityEngine;
using UnityEngine.UI;
using MartProg.Core;

namespace MartProg.Menu
{
    /// <summary>
    /// Phase 41 — the Help screen. Backlog_Landing_Page_And_Navigation.md
    /// §5/§7 item 6, held back at Phase 38 (About/Landing) and Phase 40
    /// (Researcher Area) specifically because §3.1 flagged it as "the one to
    /// think hardest about" — a tutorial can be part of the intervention a
    /// study is trying to measure, not just a convenience feature. §6.1
    /// (data collection has not started) and §6.3 (the researcher's own
    /// framing, "it's just to help the student use the app effectively")
    /// resolved that: Help ships as scaffolding, not instruction, under one
    /// rule stated in both the backlog and the content itself —
    ///
    /// **Help explains the APP. It never explains the CONCEPT.**
    ///
    /// "Tap Push to add a value to the top of the stack" is in scope. "A
    /// stack is Last-In-First-Out" is not — that sentence already lives in
    /// each module's own Explanation panel, which *is* the intervention. If
    /// this screen ever starts teaching data structures instead of buttons,
    /// it has stopped being neutral regardless of intent.
    ///
    /// NOT A PORT — same as every other Landing Page screen (see
    /// LandingPageController's class comment). `main_menu.gd` has nothing
    /// resembling a help screen.
    ///
    /// **Structurally, this is About with two differences.** Same full-
    /// screen-overlay-in-one-scene pattern, same Back button, same optional
    /// ScrollRect for content longer than one screen (Help's mechanical
    /// walkthrough of eight modules' worth of buttons is likely to need it
    /// even more than About's prose did). The two differences: (1) every
    /// word of the content is plain static TextMeshPro text set directly in
    /// the Editor — nothing here is code-driven the way About's version
    /// block is, so this script does not even need a reference to the
    /// content itself; (2) unlike About, **this screen IS logged**. See
    /// Show() below.
    ///
    /// **Logged because whether a participant opens it is the research
    /// variable, not what it says.** Backlog §3.1: "If it ships, log
    /// whether each participant opened it — otherwise the variable exists
    /// but is invisible." §6.3 restates it: "one row... turns an unknown
    /// into a covariate you can check rather than a variable you have to
    /// hope did not matter." Logged on every open, the same convention
    /// "Module Opened" and "Change Module" already use (Migration Spec
    /// §8f/§8n) — an analysis pass can derive "opened Help at least once"
    /// per participant from whether any such row exists for that StudentID,
    /// the same way §6.4 proposes deriving the module order participants
    /// actually took from existing "Module Opened" rows rather than adding
    /// a second, purpose-built column for it.
    /// </summary>
    public class HelpController : MonoBehaviour
    {
        [Header("Panel")]
        [Tooltip("Root of the Help overlay. Hidden at Awake(), shown by " +
                 "Show(), hidden again by Hide() (wired to the Back button) " +
                 "— identical lifecycle to About/Feedback/Researcher Area.")]
        [SerializeField] private GameObject helpPanel;

        [SerializeField] private Button backButton;

        [Header("Content")]
        [Tooltip("Optional. The Help panel's Scroll Rect (the ScrollView " +
                 "object). If assigned, the panel reopens scrolled back to " +
                 "the top rather than wherever the last reader left it — " +
                 "same reasoning and same fix as AboutController.Show(). " +
                 "Leave empty and the scroll position simply persists.")]
        [SerializeField] private ScrollRect contentScroll;

        private void Awake()
        {
            // Hidden by default, in Awake() rather than Start() — the
            // standing convention for every overlay panel since Phase 38's
            // landingPage-unassigned bug (see LandingPageController's own
            // comment on why this ordering matters).
            if (helpPanel != null) helpPanel.SetActive(false);
        }

        private void Start()
        {
            if (backButton != null) backButton.onClick.AddListener(Hide);
        }

        /// <summary>
        /// Shown from the Landing Page's Help button. Logs a "Help Opened"
        /// row on every call — see the class comment for why every open is
        /// logged rather than just the first.
        /// </summary>
        public void Show()
        {
            ResearchLogger.Instance?.LogEvent(
                "Help Opened",
                "",
                "Opened",
                "Participant opened the Help screen");

            if (helpPanel != null) helpPanel.SetActive(true);

            // ForceUpdateCanvases() before resetting scroll position, same
            // fix and same reasoning as AboutController.Show() — the
            // Content Size Fitter may not have recomputed content height
            // yet on the frame the panel is enabled.
            if (contentScroll != null)
            {
                Canvas.ForceUpdateCanvases();
                contentScroll.verticalNormalizedPosition = 1f;
            }
        }

        public void Hide()
        {
            if (helpPanel != null) helpPanel.SetActive(false);
        }
    }
}
