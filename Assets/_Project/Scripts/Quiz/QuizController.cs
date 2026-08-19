using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// NOTE: ResearchLogger and DashboardController are both defined in the
// GLOBAL namespace (see ResearchLogger.cs / DashboardController.cs — neither
// has a `namespace` wrapper), same as every other controller in this
// project references them, so no `using` is needed to reach them from here.

namespace MartProg.Quiz
{
    /// <summary>
    /// Phase 22: the per-module "Mini Quiz" overlay, ported from
    /// ar_learning_screen.gd's quiz-flow functions — _on_quiz_button_pressed(),
    /// randomize_quiz_options(), load_quiz_for_current_module(),
    /// clean_quiz_question_text(), display_quiz_question(),
    /// _on_option_a/b/c/d_pressed(), answer_quiz_question(),
    /// _on_next_quiz_button_pressed(), show_quiz_result(),
    /// _on_close_quiz_button_pressed(), use_quiz_question_layout(),
    /// use_quiz_result_layout(), update_quiz_progress(),
    /// reset_option_button_styles(), and set_button_color() — read directly
    /// from source this round, not from any prior summary. Question data
    /// itself lives in QuizQuestionBank.cs (the Module-vs-Controller split
    /// used by every other phase: data/logic in one file, UI wiring here).
    ///
    /// Cross-cutting, not per-module: unlike ArrayListController etc., this
    /// does not implement IModuleController — it is one shared overlay that
    /// reads whichever module is currently active and quizzes on it, same
    /// relationship ChangeModuleController has to the dashboard.
    ///
    /// Porting decisions and preserved/adapted source quirks, confirmed by
    /// reading ar_learning_screen.gd directly:
    ///
    /// 1. **Module lookup: ResearchLogger.ModuleName, not
    ///    AppState.CurrentModule.** Both exist and are meant to track the
    ///    same thing, but only ResearchLogger.ModuleName is guaranteed
    ///    correct from the moment a module's controller Start()s — every
    ///    module controller (ArrayListController, StackController,
    ///    QueueController, LinkedListController) sets
    ///    ResearchLogger.Instance.ModuleName itself in Start(). AppState's
    ///    CurrentModule is only ever set by ChangeModuleController, on a
    ///    confirmed switch — it is never touched by a module's own Start(),
    ///    so it would stay at its compiled-in default for whichever module
    ///    the Hierarchy simply starts active on, until the student
    ///    explicitly opens Change Module at least once. Since Godot's own
    ///    load_quiz_for_current_module() keys off current_module (kept in
    ///    sync with module_name at the one call site that sets both), and
    ///    ResearchLogger.ModuleName is the field that's actually reliable
    ///    from boot, quiz module lookup here reads ResearchLogger.ModuleName
    ///    — see CurrentModuleKey() below.
    ///
    /// 2. **Scope: Array/List, Stack, Queue, Linked List, Functions, Loops,
    ///    and Selection are all wired; Memory Management is deferred.** See
    ///    QuizQuestionBank.cs's class comment for the full reasoning — in
    ///    short, Functions/Loops/Selection have complete verified data and
    ///    cost nothing to include now (matching ChangeModuleController's own
    ///    "list all 8, wire what's ready" precedent), while Memory's bank is
    ///    keyed by an in-module "activity" concept that doesn't exist in
    ///    Unity yet and wasn't even fully read from source. In practice
    ///    Functions/Loops/Selection/Memory are all unreachable today anyway
    ///    — ChangeModuleController only lets a student switch to a module
    ///    whose ModuleGroupEntry.IsBuilt is true.
    ///
    /// 3. **Styling is Editor-configured, not runtime code.** Godot's
    ///    style_quiz_interface() / style_quiz_text() / style_quiz_buttons() /
    ///    apply_quiz_panel_padding() / setup_quiz_panel_layout() /
    ///    setup_quiz_overlay() exist because Godot Control nodes are styled
    ///    imperatively (StyleBoxFlat, custom_minimum_size, anchors, z_index
    ///    all set from code). None of that is ported here — matching every
    ///    other panel already built in this project (e.g.
    ///    ChangeModuleController's moduleSelectorPanel), Unity styling
    ///    (colors, anchors, corner radius, font sizes) belongs on the
    ///    Inspector-configured Image/TMP_Text components in the scene
    ///    itself; this script only ever toggles GameObject.SetActive() and
    ///    Button.interactable, plus the two genuinely data-driven runtime
    ///    colors below. See the accompanying Stage22 walkthrough for the
    ///    Editor-side setup that replaces those source functions.
    ///
    /// 4. **set_button_color() IS ported as real runtime code** — the one
    ///    piece of styling that's genuinely data-driven (which button turns
    ///    green/red depends on which option was clicked and which one was
    ///    correct, decided at runtime). Godot overrides all four style
    ///    states (normal/hover/pressed/disabled) to the same flat color;
    ///    SetButtonColor() below sets the Button's target Image.color, the
    ///    direct Unity equivalent. reset_option_button_styles() in source
    ///    calls remove_theme_stylebox_override() to fall back to the theme
    ///    default; since Unity Buttons don't have a "remove override" concept
    ///    the same way, this controller instead caches each option button's
    ///    original Image.color once in Start() and restores that cached
    ///    color — same end result (back to whatever the Editor-configured
    ///    default looked like), different mechanism.
    ///
    /// 5. **Genuinely preserved redundancies from source, kept rather than
    ///    "cleaned up":**
    ///    - The empty-question-bank check happens twice — once inside
    ///      LoadQuizForCurrentModule() itself, again in
    ///      OnQuizButtonClicked() right after calling it. Harmless (the
    ///      feedback message is simply set twice); kept because it's exactly
    ///      what source does, not because it serves a purpose.
    ///    - UseQuizQuestionLayout() is called both from OnQuizButtonClicked()
    ///      and again from inside DisplayQuizQuestion() itself — matching
    ///      _on_quiz_button_pressed() calling use_quiz_question_layout()
    ///      then display_quiz_question(), which calls it again internally.
    ///
    /// 6. **The progress bar/label are dual-purpose, matching source
    ///    exactly, not a bug:** during questions they show "Question X / N"
    ///    and fill toward the question count; at the results screen the
    ///    SAME bar and label are repurposed to show "Final Score" and fill
    ///    toward the score instead (see ShowQuizResult()). The meaning of a
    ///    full bar changes at that transition — this is Godot's own design,
    ///    preserved as-is.
    ///
    /// 7. **ScrollRect direction is inverted from Godot's ScrollContainer.**
    ///    quiz_feedback_scroll.scroll_vertical = 0 means "scrolled to the
    ///    top" in Godot (pixel offset from top). Unity's
    ///    ScrollRect.verticalNormalizedPosition = 1 means the same visual
    ///    thing ("scrolled to top" — Unity's convention is 0 = bottom,
    ///    1 = top). Every "scroll to top" call below uses 1f accordingly —
    ///    a direct idiom translation, not a behavior change.
    ///
    /// 8. **clean_quiz_question_text()'s Memory-prefix-stripping branch is
    ///    NOT ported.** It would need a current_memory_activity string this
    ///    project doesn't have (Memory isn't built — see point 2). Since
    ///    Memory's question bank is always empty here, DisplayQuizQuestion()
    ///    can never actually be reached with a Memory question in the first
    ///    place, making that branch dead code regardless of whether it's
    ///    written. CleanQuizQuestionText() here is a straight Trim() —
    ///    revisit this if Memory's quiz is ever built. Selection's own
    ///    "Mode: X\n\n" question prefix is NOT stripped by source either
    ///    (only Memory's is) — so Selection's questions display with that
    ///    prefix baked in verbatim, preserved as-is in QuizQuestionBank.cs.
    ///
    /// 9. **A likely-unintentional Godot indentation bug was found and is
    ///    NOT reproduced.** In ar_learning_screen.gd's _ready(), the block
    ///    that sets quiz_button.text, wires quiz_button's initial styling
    ///    calls, and sets quiz_panel.visible = false sits nested one level
    ///    too deep — INSIDE the
    ///    `if not start_module_button.pressed.is_connected(...):` guard,
    ///    rather than at _ready()'s own top level alongside everything else.
    ///    Since _ready() only ever runs once per session in this app (no
    ///    scene reload), this never actually manifests as a visible bug —
    ///    the block still runs exactly once, on the only _ready() call there
    ///    is. There's no clean literal translation for an indentation
    ///    accident in C# regardless, so this controller simply runs its
    ///    equivalent setup (hiding the overlay/panel) unconditionally in
    ///    Start(), which is what happens in practice in Godot too.
    ///
    /// 10. **Trigger button lives per-module, not in a shared admin row —
    ///     corrected after an unverified first draft.** Godot's
    ///     ar_learning_screen.gd wires a single quiz_button once in
    ///     _ready(), but the .gd scripts available to this project never
    ///     included the .tscn scene files that would show where that button
    ///     actually sits in the Control tree. An earlier draft of this file
    ///     assumed it sat in the same admin row as export/clear/change-module
    ///     — that was never actually confirmed from source and was wrong.
    ///     The Unity port instead places one Quiz button inside each
    ///     module's own control strip, alongside that module's own
    ///     Search/Traverse buttons (the admin row only ever holds Export
    ///     Log / Clear Log / Change Module). See arrayQuizButton /
    ///     stackQuizButton / queueQuizButton / linkedListQuizButton below —
    ///     all four fire the exact same OnQuizButtonClicked() handler, since
    ///     which module's quiz loads is decided by ResearchLogger.ModuleName
    ///     (porting note 1), not by which button was pressed.
    ///
    /// 11. **THE QUIZ TIMER IS NOT A PORT. It has no Godot counterpart at
    ///     all.** Every other behavior in this file traces back to a
    ///     specific ar_learning_screen.gd function; the timer does not.
    ///     ar_learning_screen.gd's quiz flow has no timing concept of any
    ///     kind — no countdown, no time limit, no elapsed-time tracking, no
    ///     duration picker. This was added at the user's explicit request,
    ///     after Quiz was otherwise complete and on-device validated, to
    ///     give students a sense of time boundaries.
    ///
    ///     Recorded here rather than left to look like a port, because this
    ///     project's whole discipline is that Unity is a faithful port of a
    ///     finished Godot model and any deviation gets flagged loudly. If
    ///     the Godot original is ever treated as the reference again, this
    ///     is a Unity-only divergence to be aware of — nothing here is a
    ///     "restored" or "corrected" version of some Godot behavior.
    ///
    ///     Design decisions, all confirmed with the user before building:
    ///     - The timer is OPTIONAL. The duration picker includes a "No time
    ///       limit" choice, which runs the quiz exactly as it behaved before
    ///       this feature existed. The untimed path is preserved, not
    ///       replaced.
    ///     - When time expires with questions unreached, those questions
    ///       count as missed (score stays out of the FULL question count)
    ///       and appear in the end-of-quiz review list with their correct
    ///       answers and explanations — so students still learn what they
    ///       didn't get to.
    ///     - Elapsed time is tracked and reported at the end for BOTH timed
    ///       and untimed runs, since "time spent" is useful research data
    ///       either way. It's logged to ResearchLogger alongside the score.
    ///     - The countdown label is hidden entirely on untimed runs: a
    ///       student who explicitly chose "no time limit" shouldn't get a
    ///       running clock reintroducing time pressure. Their elapsed time
    ///       is still reported at the end.
    /// </summary>
    public class QuizController : MonoBehaviour
    {
        [Header("Trigger")]
        [Tooltip("One button per module, living in that module's own control strip " +
                 "(same ButtonGrid as its Search/Traverse buttons) — see porting note 10. " +
                 "All four fire the same OnQuizButtonClicked() handler below; which module's " +
                 "quiz loads is decided by ResearchLogger.ModuleName, not by which button was " +
                 "pressed. Each of these Button GameObjects is also referenced a second time, " +
                 "by its own module Controller (e.g. ArrayListController.quizButton), purely " +
                 "so that Controller's CountAssignedButtons()/ApplyResponsiveButtonLayout() " +
                 "grid math includes it — the Controller does not wire its click behavior.")]
        [SerializeField] private Button arrayQuizButton;
        [SerializeField] private Button stackQuizButton;
        [SerializeField] private Button queueQuizButton;
        [SerializeField] private Button linkedListQuizButton;
        [Tooltip("Added in Phase 23 alongside the Functions module. Its question bank was " +
                 "already ported and waiting in QuizQuestionBank (porting note 2), so wiring " +
                 "this button is all that was needed to make the Functions quiz reachable.")]
        [SerializeField] private Button functionsQuizButton;
        [Tooltip("Added in Phase 24 alongside the Loops module. Its question bank was also " +
                 "already ported and waiting in QuizQuestionBank (porting note 2).")]
        [SerializeField] private Button loopsQuizButton;
        [Tooltip("Added in Phase 25 alongside the Selection module. Its 13-question bank was " +
                 "ported in Phase 22 — note it keeps the \"Mode: X\" prefix baked into the " +
                 "question text, which source's clean_quiz_question_text() only strips for " +
                 "Memory (porting note 2).")]
        [SerializeField] private Button selectionQuizButton;

        [Header("Quiz Panel")]
        [Tooltip("Full-screen dimmed background behind the quiz panel (Image, raycastTarget ON so " +
                 "clicks don't pass through to the dashboard/AR content behind it) — the Unity " +
                 "equivalent of quiz_overlay's ColorRect + MOUSE_FILTER_STOP.")]
        [SerializeField] private GameObject quizOverlay;
        [SerializeField] private GameObject quizPanel;
        [SerializeField] private TMP_Text quizTitleLabel;
        [SerializeField] private TMP_Text quizQuestionLabel;
        [SerializeField] private TMP_Text quizProgressLabel;
        [Tooltip("Optional — Unity's closest equivalent to Godot's ProgressBar. Leave unassigned if " +
                 "not using a visual bar.")]
        [SerializeField] private Slider quizProgressBar;
        [Tooltip("Optional — wraps quizFeedbackLabel so long missed-question review text can scroll. " +
                 "Scrolled to top on every new question and on showing results.")]
        [SerializeField] private ScrollRect quizFeedbackScroll;
        [SerializeField] private TMP_Text quizFeedbackLabel;

        [Header("Option Buttons (A-D)")]
        [SerializeField] private Button optionAButton;
        [SerializeField] private TMP_Text optionAText;
        [SerializeField] private Button optionBButton;
        [SerializeField] private TMP_Text optionBText;
        [SerializeField] private Button optionCButton;
        [SerializeField] private TMP_Text optionCText;
        [SerializeField] private Button optionDButton;
        [SerializeField] private TMP_Text optionDText;

        [Header("Nav Buttons")]
        [SerializeField] private Button nextQuizButton;
        [SerializeField] private Button closeQuizButton;

        [Header("Restore on close (optional, defensive)")]
        [Tooltip("Matches _on_close_quiz_button_pressed()'s defensive main_ui.visible = true — " +
                 "quiz never hides these on open, but forces them visible again on close in case " +
                 "something else left them hidden. Safe to leave unassigned.")]
        [SerializeField] private GameObject dashboardRoot;
        [Tooltip("Matches the same handler's ar_world.visible = true. Safe to leave unassigned.")]
        [SerializeField] private GameObject arContentRoot;

        [Header("Timer — NOT a Godot port, see porting note 11")]
        [Tooltip("The duration-picker card shown when a Quiz button is tapped, BEFORE the " +
                 "quiz itself opens. A sibling of QuizPanel, not a child — see " +
                 "Stage22_Quiz_Walkthrough.md 3.7. Leave every field in this section " +
                 "unassigned to disable the timer feature entirely and go straight into an " +
                 "untimed quiz, exactly as this controller behaved before the timer existed.")]
        [SerializeField] private GameObject quizDurationPanel;
        [Tooltip("One button per time limit. Each starts the quiz immediately with that limit.")]
        [SerializeField] private Button duration1MinuteButton;
        [SerializeField] private Button duration2MinuteButton;
        [SerializeField] private Button duration3MinuteButton;
        [SerializeField] private Button duration4MinuteButton;
        [SerializeField] private Button duration5MinuteButton;
        [Tooltip("Starts the quiz with no time limit — the pre-timer behavior.")]
        [SerializeField] private Button noTimeLimitButton;
        [Tooltip("Optional. Backs out of the picker without starting a quiz.")]
        [SerializeField] private Button cancelDurationButton;

        [Tooltip("Countdown display, shown inside QuizPanel while a TIMED quiz runs. " +
                 "Automatically hidden for untimed runs (porting note 11). Optional.")]
        [SerializeField] private TMP_Text quizTimerLabel;
        [Tooltip("Color the countdown switches to once Timer Warning Seconds remain.")]
        [SerializeField] private Color timerWarningColor = new Color(0.90f, 0.30f, 0.30f, 1f);
        [Tooltip("Seconds remaining at which the countdown turns Timer Warning Color. " +
                 "Set to 0 to disable the warning color entirely.")]
        [SerializeField] private float timerWarningSeconds = 30f;

        [Header("Answer Feedback Colors")]
        [Tooltip("Color applied to the selected option when it's correct, and to the correct " +
                 "option when the user picked wrong. Inspector-editable (unlike most of this " +
                 "file's Editor-configured styling, these two started as hardcoded constants " +
                 "here — moved to fields after on-device testing found the original muted " +
                 "tones too dim to read clearly). Tune freely without another code change.")]
        [SerializeField] private Color correctColor = new Color(0.20f, 0.70f, 0.25f, 1f);
        [Tooltip("Color applied to the selected option when it's wrong.")]
        [SerializeField] private Color incorrectColor = new Color(0.80f, 0.18f, 0.18f, 1f);

        private List<QuizQuestion> quizQuestions = new List<QuizQuestion>();
        private int currentQuizIndex;
        private int quizScore;
        private bool answeredCurrentQuestion;
        private readonly List<MissedQuizQuestion> missedQuizQuestions = new List<MissedQuizQuestion>();

        // Cached once in Start() — see porting note 4 above.
        private readonly Color[] defaultOptionButtonColors = new Color[4];

        // Timer state — see porting note 11 above. None of this has a Godot counterpart.
        private float quizTimeLimitSeconds;   // 0 = untimed
        private float quizTimeRemaining;
        private float quizElapsedSeconds;     // tracked for timed AND untimed runs
        private bool quizInProgress;          // drives Update(); false once results show
        private bool quizTimedOut;
        private int quizUnansweredCount;
        private Color defaultTimerLabelColor = Color.white;
        private bool cachedTimerLabelColor;

        private const string UnansweredAnswerText = "(not answered — time ran out)";

        private class MissedQuizQuestion
        {
            public int QuestionNumber;
            public string Question;
            public string SelectedAnswer;
            public string CorrectAnswer;
            public string Explanation;
        }

        private void Start()
        {
            WireButtons();
            CacheDefaultOptionButtonColors();

            // See porting note 9 above — equivalent of Godot's
            // (accidentally-nested-but-harmless) one-time setup.
            if (quizOverlay != null) quizOverlay.SetActive(false);
            if (quizPanel != null) quizPanel.SetActive(false);

            // Timer additions — see porting note 11.
            if (quizDurationPanel != null) quizDurationPanel.SetActive(false);
            CacheDefaultTimerLabelColor();
            ValidateSceneSetup();
        }

        /// Startup setup check. Deliberately SILENT when everything is either
        /// correctly wired or deliberately switched off, so it costs nothing in
        /// a normal run — it only speaks up for states that are always a
        /// mistake. The temporary step-by-step flow logging that lived here
        /// while the timer was being brought up has been removed; these two
        /// guards are what proved worth keeping.
        ///
        /// Click any line it prints to ping the GameObject that logged it.
        private void ValidateSceneSetup()
        {
            // Guard 1: more than one QuizController in the scene. Always a bug —
            // every instance wires its own listeners to the SAME four per-module
            // Quiz buttons, so a tap runs the whole open sequence once per
            // instance and whichever runs last wins, silently undoing the
            // others. Inactive ones are included on purpose: they contribute
            // nothing now but are live the moment something enables them.
            QuizController[] allControllers =
                FindObjectsByType<QuizController>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            if (allControllers.Length > 1)
            {
                Debug.LogWarning(
                    "[QuizController] " + allControllers.Length + " QuizController components exist in this "
                    + "scene — there should be exactly one. Each wires its own listeners to the same Quiz "
                    + "buttons, so tapping Quiz runs the open sequence once per component and the last one "
                    + "to run wins. Keep whichever is fully wired in the Inspector and delete the rest.", this);
            }

            // Guard 2: the timer is half-wired. An unassigned Quiz Duration
            // Panel with NOTHING else assigned means the feature is switched
            // off on purpose (porting note 11) and is not worth a warning — but
            // an unassigned panel alongside assigned buttons means someone
            // wired the pieces and missed the one field that actually enables
            // the picker, which looks exactly like the feature being broken.
            bool anyOtherTimerFieldAssigned =
                duration1MinuteButton != null || duration2MinuteButton != null
                || duration3MinuteButton != null || duration4MinuteButton != null
                || duration5MinuteButton != null || noTimeLimitButton != null
                || cancelDurationButton != null || quizTimerLabel != null;

            if (quizDurationPanel == null)
            {
                if (anyOtherTimerFieldAssigned)
                {
                    Debug.LogWarning(
                        "[QuizController] Quiz timer is OFF because 'Quiz Duration Panel' is unassigned, "
                        + "but other Timer fields ARE assigned — so this is almost certainly an oversight "
                        + "rather than a deliberate opt-out. Tapping Quiz will skip the picker and go "
                        + "straight into an untimed quiz until that one field is filled in. "
                        + "NOTE: Inspector changes made while Play mode is running are discarded when Play "
                        + "stops — wire it in Edit mode.", this);
                }

                return;
            }

            List<string> missing = new List<string>();
            if (duration1MinuteButton == null) missing.Add("Duration 1 Minute Button");
            if (duration2MinuteButton == null) missing.Add("Duration 2 Minute Button");
            if (duration3MinuteButton == null) missing.Add("Duration 3 Minute Button");
            if (duration4MinuteButton == null) missing.Add("Duration 4 Minute Button");
            if (duration5MinuteButton == null) missing.Add("Duration 5 Minute Button");
            if (noTimeLimitButton == null) missing.Add("No Time Limit Button");
            if (quizTimerLabel == null) missing.Add("Quiz Timer Label");

            if (missing.Count > 0)
            {
                Debug.LogWarning(
                    "[QuizController] Quiz timer is ON, but these Timer fields are still unassigned: "
                    + string.Join(", ", missing)
                    + ". The picker will appear, but any unassigned button will do nothing when tapped.", this);
            }
        }

        private void WireButtons()
        {
            if (arrayQuizButton != null) arrayQuizButton.onClick.AddListener(OnQuizButtonClicked);
            if (stackQuizButton != null) stackQuizButton.onClick.AddListener(OnQuizButtonClicked);
            if (queueQuizButton != null) queueQuizButton.onClick.AddListener(OnQuizButtonClicked);
            if (linkedListQuizButton != null) linkedListQuizButton.onClick.AddListener(OnQuizButtonClicked);
            if (functionsQuizButton != null) functionsQuizButton.onClick.AddListener(OnQuizButtonClicked);
            if (loopsQuizButton != null) loopsQuizButton.onClick.AddListener(OnQuizButtonClicked);
            if (selectionQuizButton != null) selectionQuizButton.onClick.AddListener(OnQuizButtonClicked);
            if (optionAButton != null) optionAButton.onClick.AddListener(OnOptionAClicked);
            if (optionBButton != null) optionBButton.onClick.AddListener(OnOptionBClicked);
            if (optionCButton != null) optionCButton.onClick.AddListener(OnOptionCClicked);
            if (optionDButton != null) optionDButton.onClick.AddListener(OnOptionDClicked);
            if (nextQuizButton != null) nextQuizButton.onClick.AddListener(OnNextQuizButtonClicked);
            if (closeQuizButton != null) closeQuizButton.onClick.AddListener(OnCloseQuizButtonClicked);

            // Timer additions — see porting note 11.
            if (duration1MinuteButton != null) duration1MinuteButton.onClick.AddListener(OnDuration1MinuteClicked);
            if (duration2MinuteButton != null) duration2MinuteButton.onClick.AddListener(OnDuration2MinuteClicked);
            if (duration3MinuteButton != null) duration3MinuteButton.onClick.AddListener(OnDuration3MinuteClicked);
            if (duration4MinuteButton != null) duration4MinuteButton.onClick.AddListener(OnDuration4MinuteClicked);
            if (duration5MinuteButton != null) duration5MinuteButton.onClick.AddListener(OnDuration5MinuteClicked);
            if (noTimeLimitButton != null) noTimeLimitButton.onClick.AddListener(OnNoTimeLimitClicked);
            if (cancelDurationButton != null) cancelDurationButton.onClick.AddListener(OnCancelDurationClicked);
        }

        private void CacheDefaultTimerLabelColor()
        {
            if (quizTimerLabel == null || cachedTimerLabelColor) return;
            defaultTimerLabelColor = quizTimerLabel.color;
            cachedTimerLabelColor = true;
        }

        private void CacheDefaultOptionButtonColors()
        {
            Button[] buttons = GetOptionButtons();
            for (int i = 0; i < buttons.Length; i++)
            {
                defaultOptionButtonColors[i] = (buttons[i] != null && buttons[i].image != null)
                    ? buttons[i].image.color
                    : Color.white;
            }
        }

        // ---------------------------------------------------------------
        // Open / load
        // ---------------------------------------------------------------

        // Godot: _on_quiz_button_pressed()
        public void OnQuizButtonClicked()
        {
            LoadQuizForCurrentModule();

            if (quizQuestions.Count == 0)
            {
                DashboardController.Instance?.SetOperation("Quiz: No quiz questions are available for this module.");
                RebuildDashboardLayout();
                return;
            }

            // Timer addition — see porting note 11. If no duration panel is
            // wired, the feature is simply off and this behaves exactly as it
            // did before the timer existed: straight into an untimed quiz.
            if (quizDurationPanel != null)
            {
                ShowDurationPicker();
                return;
            }

            BeginQuiz(0f);
        }

        // Timer addition — see porting note 11. No Godot counterpart.
        private void ShowDurationPicker()
        {
            if (quizPanel != null) quizPanel.SetActive(false);
            if (quizOverlay != null) quizOverlay.SetActive(true);
            if (quizDurationPanel != null) quizDurationPanel.SetActive(true);

        }

        public void OnDuration1MinuteClicked() => BeginQuiz(60f);
        public void OnDuration2MinuteClicked() => BeginQuiz(120f);
        public void OnDuration3MinuteClicked() => BeginQuiz(180f);
        public void OnDuration4MinuteClicked() => BeginQuiz(240f);
        public void OnDuration5MinuteClicked() => BeginQuiz(300f);
        public void OnNoTimeLimitClicked() => BeginQuiz(0f);

        // Backs out of the picker without starting anything.
        public void OnCancelDurationClicked()
        {
            if (quizDurationPanel != null) quizDurationPanel.SetActive(false);
            if (quizOverlay != null) quizOverlay.SetActive(false);
        }

        /// Timer addition — see porting note 11. This is the old body of
        /// OnQuizButtonClicked() (the state reset + first question + show panel),
        /// factored out so both the timed and untimed paths share it verbatim.
        /// timeLimitSeconds of 0 means untimed — the pre-timer behavior.
        private void BeginQuiz(float timeLimitSeconds)
        {
            currentQuizIndex = 0;
            quizScore = 0;
            answeredCurrentQuestion = false;
            missedQuizQuestions.Clear();

            quizTimeLimitSeconds = Mathf.Max(0f, timeLimitSeconds);
            quizTimeRemaining = quizTimeLimitSeconds;
            quizElapsedSeconds = 0f;
            quizTimedOut = false;
            quizUnansweredCount = 0;
            quizInProgress = true;

            if (quizDurationPanel != null) quizDurationPanel.SetActive(false);

            UseQuizQuestionLayout();
            DisplayQuizQuestion();
            UpdateTimerLabel();

            if (quizOverlay != null) quizOverlay.SetActive(true);
            if (quizPanel != null) quizPanel.SetActive(true);

            LogEvent(
                "Quiz Started",
                QuizQuestionBank.GetDisplayName(CurrentModuleKey()),
                quizTimeLimitSeconds > 0f ? "Timed" : "Untimed",
                quizTimeLimitSeconds > 0f
                    ? "Time limit: " + FormatTime(quizTimeLimitSeconds)
                    : "No time limit selected");
        }

        // Godot: load_quiz_for_current_module()
        private void LoadQuizForCurrentModule()
        {
            string moduleKey = CurrentModuleKey();

            quizQuestions = QuizQuestionBank.GetQuestionsForModule(moduleKey);

            if (quizTitleLabel != null)
            {
                quizTitleLabel.text = QuizQuestionBank.GetDisplayName(moduleKey) + " Mini Quiz";
            }

            if (quizQuestions.Count == 0)
            {
                // See porting note 5 above — matches source's own redundant
                // double check (OnQuizButtonClicked() checks again too).
                DashboardController.Instance?.SetOperation("Quiz: No quiz questions are available for this module.");
                RebuildDashboardLayout();
                return;
            }

            quizQuestions = RandomizeQuizOptions(quizQuestions);
            ShuffleList(quizQuestions);

            Debug.Log("Loaded quiz for module: " + moduleKey + " | Questions: " + quizQuestions.Count);
        }

        // Godot: randomize_quiz_options(source_questions)
        // GetQuestionsForModule() already handed us freshly cloned
        // QuizQuestions (the combined equivalent of source's two separate
        // duplicate(true) calls), so this mutates them in place.
        private static List<QuizQuestion> RandomizeQuizOptions(List<QuizQuestion> questions)
        {
            foreach (QuizQuestion q in questions)
            {
                if (q.Options == null || q.Options.Length == 0) continue;
                if (q.Answer < 0 || q.Answer >= q.Options.Length) continue;

                List<(string text, bool isCorrect)> pairs = new List<(string, bool)>(q.Options.Length);
                for (int i = 0; i < q.Options.Length; i++)
                {
                    pairs.Add((q.Options[i], i == q.Answer));
                }

                ShuffleList(pairs);

                string[] newOptions = new string[pairs.Count];
                int newAnswerIndex = 0;

                for (int i = 0; i < pairs.Count; i++)
                {
                    newOptions[i] = pairs[i].text;
                    if (pairs[i].isCorrect) newAnswerIndex = i;
                }

                q.Options = newOptions;
                q.Answer = newAnswerIndex;
            }

            return questions;
        }

        private static void ShuffleList<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        // Godot: get_current_module_display_name() / current_module lookup
        // — see porting note 1 above for why this reads ResearchLogger,
        // not AppState.
        private string CurrentModuleKey()
        {
            return ResearchLogger.Instance != null ? ResearchLogger.Instance.ModuleName : "";
        }

        // Godot: clean_quiz_question_text() — see porting note 8 above.
        private string CleanQuizQuestionText(string questionText)
        {
            return string.IsNullOrEmpty(questionText) ? "" : questionText.Trim();
        }

        // ---------------------------------------------------------------
        // Question display / answering
        // ---------------------------------------------------------------

        // Godot: display_quiz_question()
        private void DisplayQuizQuestion()
        {
            if (currentQuizIndex >= quizQuestions.Count)
            {
                ShowQuizResult();
                return;
            }

            // See porting note 5 above — redundant with the caller's own
            // call, matching source.
            UseQuizQuestionLayout();
            ResetOptionButtonStyles();

            QuizQuestion question = quizQuestions[currentQuizIndex];
            answeredCurrentQuestion = false;

            UpdateQuizProgress();

            if (quizQuestionLabel != null)
            {
                quizQuestionLabel.text = CleanQuizQuestionText(question.Question);
            }

            Button[] optionButtons = GetOptionButtons();
            TMP_Text[] optionTexts = GetOptionTexts();

            for (int i = 0; i < optionButtons.Length; i++)
            {
                if (optionButtons[i] != null)
                {
                    optionButtons[i].gameObject.SetActive(true);
                    optionButtons[i].interactable = true;
                }

                if (optionTexts[i] != null && question.Options != null && i < question.Options.Length)
                {
                    optionTexts[i].text = question.Options[i];
                }
            }

            if (quizFeedbackLabel != null) quizFeedbackLabel.text = "Select an answer.";
            RefreshFeedbackLayout();

            if (nextQuizButton != null)
            {
                nextQuizButton.gameObject.SetActive(true);
                nextQuizButton.interactable = false;
            }

            if (closeQuizButton != null)
            {
                closeQuizButton.gameObject.SetActive(true);
                closeQuizButton.interactable = true;
            }
        }

        // Godot: _on_option_a/b/c/d_pressed()
        public void OnOptionAClicked() { Debug.Log("Option A pressed"); AnswerQuizQuestion(0); }
        public void OnOptionBClicked() { Debug.Log("Option B pressed"); AnswerQuizQuestion(1); }
        public void OnOptionCClicked() { Debug.Log("Option C pressed"); AnswerQuizQuestion(2); }
        public void OnOptionDClicked() { Debug.Log("Option D pressed"); AnswerQuizQuestion(3); }

        // Godot: answer_quiz_question(selected_index)
        private void AnswerQuizQuestion(int selectedIndex)
        {
            if (answeredCurrentQuestion) return;
            if (currentQuizIndex < 0 || currentQuizIndex >= quizQuestions.Count) return;

            QuizQuestion question = quizQuestions[currentQuizIndex];
            string[] options = question.Options ?? new string[0];

            if (selectedIndex < 0 || selectedIndex >= options.Length) return;

            answeredCurrentQuestion = true;

            int correctIndex = question.Answer;

            if (correctIndex < 0 || correctIndex >= options.Length)
            {
                if (quizFeedbackLabel != null)
                {
                    quizFeedbackLabel.text = "Quiz error: Correct answer index is invalid for this question.";
                }
                RefreshFeedbackLayout();
                return;
            }

            string selectedAnswer = options[selectedIndex];
            string correctAnswer = options[correctIndex];
            string explanationText = string.IsNullOrEmpty(question.Explanation)
                ? "No explanation provided."
                : question.Explanation;
            string moduleDisplayName = QuizQuestionBank.GetDisplayName(CurrentModuleKey());

            ResetOptionButtonStyles();

            Button[] optionButtons = GetOptionButtons();

            if (selectedIndex == correctIndex)
            {
                quizScore++;

                SetButtonColor(optionButtons[selectedIndex], correctColor);

                if (quizFeedbackLabel != null)
                {
                    quizFeedbackLabel.text = "Correct.\n\n" + explanationText;
                }
                RefreshFeedbackLayout();

                LogEvent(
                    "Quiz Answer",
                    moduleDisplayName + " Question " + (currentQuizIndex + 1),
                    "Correct",
                    "Selected: " + selectedAnswer);
            }
            else
            {
                SetButtonColor(optionButtons[selectedIndex], incorrectColor);
                SetButtonColor(optionButtons[correctIndex], correctColor);

                missedQuizQuestions.Add(new MissedQuizQuestion
                {
                    QuestionNumber = currentQuizIndex + 1,
                    Question = question.Question,
                    SelectedAnswer = selectedAnswer,
                    CorrectAnswer = correctAnswer,
                    Explanation = explanationText
                });

                if (quizFeedbackLabel != null)
                {
                    quizFeedbackLabel.text = "Incorrect.\n\nCorrect answer: " + correctAnswer + "\n\n" + explanationText;
                }
                RefreshFeedbackLayout();

                LogEvent(
                    "Quiz Answer",
                    moduleDisplayName + " Question " + (currentQuizIndex + 1),
                    "Incorrect",
                    "Selected: " + selectedAnswer + " | Correct: " + correctAnswer);
            }

            foreach (Button button in optionButtons)
            {
                if (button != null) button.interactable = false;
            }

            if (nextQuizButton != null) nextQuizButton.interactable = true;
        }

        // Godot: _on_next_quiz_button_pressed()
        public void OnNextQuizButtonClicked()
        {
            currentQuizIndex++;

            if (currentQuizIndex >= quizQuestions.Count)
            {
                ShowQuizResult();
            }
            else
            {
                DisplayQuizQuestion();
            }
        }

        // ---------------------------------------------------------------
        // Timer — see porting note 11. Nothing below has a Godot counterpart.
        // ---------------------------------------------------------------

        private void Update()
        {
            if (!quizInProgress) return;

            quizElapsedSeconds += Time.deltaTime;

            if (quizTimeLimitSeconds <= 0f) return; // untimed: track elapsed only

            quizTimeRemaining -= Time.deltaTime;

            if (quizTimeRemaining <= 0f)
            {
                quizTimeRemaining = 0f;
                UpdateTimerLabel();
                HandleQuizTimeExpired();
                return;
            }

            UpdateTimerLabel();
        }

        private void UpdateTimerLabel()
        {
            if (quizTimerLabel == null) return;

            // Untimed runs get no visible clock at all — see porting note 11.
            if (quizTimeLimitSeconds <= 0f)
            {
                quizTimerLabel.gameObject.SetActive(false);
                return;
            }

            quizTimerLabel.gameObject.SetActive(true);
            quizTimerLabel.text = "Time left: " + FormatTime(quizTimeRemaining);

            CacheDefaultTimerLabelColor();
            bool warning = timerWarningSeconds > 0f && quizTimeRemaining <= timerWarningSeconds;
            quizTimerLabel.color = warning ? timerWarningColor : defaultTimerLabelColor;
        }

        /// Time ran out. Every question the student never reached — plus the
        /// current one if it was still unanswered — is recorded as missed, so
        /// the score stays out of the FULL question count and the review list
        /// still teaches the material they didn't get to (see porting note 11).
        private void HandleQuizTimeExpired()
        {
            quizInProgress = false;
            quizTimedOut = true;

            // The current question is already scored and recorded if it was
            // answered; only start marking from the next one in that case.
            int firstUnreached = answeredCurrentQuestion ? currentQuizIndex + 1 : currentQuizIndex;

            for (int i = firstUnreached; i < quizQuestions.Count; i++)
            {
                QuizQuestion question = quizQuestions[i];
                if (question == null) continue;

                string correctAnswer = (question.Options != null
                                        && question.Answer >= 0
                                        && question.Answer < question.Options.Length)
                    ? question.Options[question.Answer]
                    : "(unavailable)";

                missedQuizQuestions.Add(new MissedQuizQuestion
                {
                    QuestionNumber = i + 1,
                    Question = question.Question,
                    SelectedAnswer = UnansweredAnswerText,
                    CorrectAnswer = correctAnswer,
                    Explanation = string.IsNullOrEmpty(question.Explanation)
                        ? "No explanation provided."
                        : question.Explanation
                });

                quizUnansweredCount++;
            }

            LogEvent(
                "Quiz Timed Out",
                QuizQuestionBank.GetDisplayName(CurrentModuleKey()),
                "Time Expired",
                "Limit: " + FormatTime(quizTimeLimitSeconds)
                    + " | Reached question " + Mathf.Min(currentQuizIndex + 1, quizQuestions.Count)
                    + " of " + quizQuestions.Count);

            ShowQuizResult();
        }

        private static string FormatTime(float seconds)
        {
            if (seconds < 0f) seconds = 0f;
            int total = Mathf.CeilToInt(seconds);
            return (total / 60) + ":" + (total % 60).ToString("00");
        }

        // ---------------------------------------------------------------
        // Result
        // ---------------------------------------------------------------

        // Godot: show_quiz_result()
        private void ShowQuizResult()
        {
            // Timer addition — see porting note 11. Stops Update()'s countdown
            // whether we got here by answering everything or by running out of
            // time (HandleQuizTimeExpired() has already set this false itself).
            quizInProgress = false;
            if (quizTimerLabel != null) quizTimerLabel.gameObject.SetActive(false);

            UseQuizResultLayout();

            string moduleDisplayName = QuizQuestionBank.GetDisplayName(CurrentModuleKey());
            bool isMemory = CurrentModuleKey() == "Memory"; // unreachable today — see porting note 2/8

            string quizCompletedTitle = isMemory ? "Quiz Completed" : moduleDisplayName + " Quiz Completed";

            if (quizQuestionLabel != null) quizQuestionLabel.text = quizCompletedTitle;
            if (quizProgressLabel != null) quizProgressLabel.text = "Final Score";

            // See porting note 6 above — the bar is repurposed here, not a
            // separate control.
            if (quizProgressBar != null)
            {
                quizProgressBar.maxValue = quizQuestions.Count;
                quizProgressBar.value = quizScore;
            }

            // Godot's Memory branch here calls mark_memory_activity_completed()
            // + update_memory_status_label() — deferred along with the rest
            // of Memory (see porting note 2), not reachable today.

            StringBuilder resultMessage = new StringBuilder();
            resultMessage.Append("You scored ").Append(quizScore).Append(" out of ")
                .Append(quizQuestions.Count).Append(" in this quiz.");

            // Timer addition — see porting note 11. Reported for timed and
            // untimed runs alike; the limit and the ran-out notice only appear
            // when there actually was a limit.
            resultMessage.Append("\n\nTime spent: ").Append(FormatTime(quizElapsedSeconds));

            if (quizTimeLimitSeconds > 0f)
            {
                resultMessage.Append(" of ").Append(FormatTime(quizTimeLimitSeconds)).Append(" allowed.");

                if (quizTimedOut && quizUnansweredCount > 0)
                {
                    resultMessage.Append("\nTime ran out with ").Append(quizUnansweredCount)
                        .Append(quizUnansweredCount == 1 ? " question" : " questions")
                        .Append(" unanswered — they are listed below with the correct answers.");
                }
                else if (quizTimedOut)
                {
                    resultMessage.Append("\nTime ran out, but you had already answered every question.");
                }
            }
            else
            {
                resultMessage.Append(" (no time limit).");
            }

            if (missedQuizQuestions.Count == 0)
            {
                resultMessage.Append("\n\nExcellent work. You answered all questions correctly.");
            }
            else
            {
                resultMessage.Append("\n\nReview the questions you missed:\n");

                foreach (MissedQuizQuestion missed in missedQuizQuestions)
                {
                    resultMessage.Append("\nQ").Append(missed.QuestionNumber).Append(": ").Append(missed.Question);
                    resultMessage.Append("\nYour answer: ").Append(missed.SelectedAnswer);
                    resultMessage.Append("\nCorrect answer: ").Append(missed.CorrectAnswer);

                    if (!string.IsNullOrEmpty(missed.Explanation))
                    {
                        resultMessage.Append("\nExplanation: ").Append(missed.Explanation);
                    }

                    resultMessage.Append("\n");
                }
            }

            if (quizFeedbackLabel != null) quizFeedbackLabel.text = resultMessage.ToString();
            RefreshFeedbackLayout();

            foreach (Button button in GetOptionButtons())
            {
                if (button != null) button.gameObject.SetActive(false);
            }

            if (nextQuizButton != null)
            {
                nextQuizButton.gameObject.SetActive(false);
                nextQuizButton.interactable = false;
            }

            if (closeQuizButton != null)
            {
                closeQuizButton.gameObject.SetActive(true);
                closeQuizButton.interactable = true;
            }

            // See porting note 7 above — 1f is "scrolled to top" in Unity's
            // ScrollRect convention.
            if (quizFeedbackScroll != null) quizFeedbackScroll.verticalNormalizedPosition = 1f;

            LogEvent(
                "Quiz Completed",
                quizScore + "/" + quizQuestions.Count,
                quizTimedOut ? "Completed (time expired)" : "Completed",
                moduleDisplayName + " final quiz score | Time spent: " + FormatTime(quizElapsedSeconds)
                    + (quizTimeLimitSeconds > 0f
                        ? " of " + FormatTime(quizTimeLimitSeconds) + " allowed"
                        : " (untimed)"));

            if (missedQuizQuestions.Count > 0)
            {
                LogEvent(
                    "Quiz Review",
                    missedQuizQuestions.Count + " missed question(s)",
                    "Review Needed",
                    moduleDisplayName + " quiz review displayed");
            }
        }

        // Godot: _on_close_quiz_button_pressed()
        public void OnCloseQuizButtonClicked()
        {
            // Timer addition — see porting note 11. Without this the countdown
            // would keep running after the student closed a quiz mid-way and
            // could fire HandleQuizTimeExpired() over a closed panel.
            quizInProgress = false;

            if (quizPanel != null) quizPanel.SetActive(false);
            if (quizDurationPanel != null) quizDurationPanel.SetActive(false);
            if (quizOverlay != null) quizOverlay.SetActive(false);

            // Defensive restore — see the [Header("Restore on close")] tooltip above.
            if (dashboardRoot != null) dashboardRoot.SetActive(true);
            if (arContentRoot != null) arContentRoot.SetActive(true);
        }

        // ---------------------------------------------------------------
        // Layout / styling helpers — see porting notes 3 and 4 above.
        // ---------------------------------------------------------------

        // Godot: use_quiz_question_layout()
        private void UseQuizQuestionLayout()
        {
            foreach (Button button in GetOptionButtons())
            {
                if (button != null) button.gameObject.SetActive(true);
            }

            if (nextQuizButton != null)
            {
                nextQuizButton.gameObject.SetActive(true);
                nextQuizButton.interactable = false;
            }

            if (closeQuizButton != null)
            {
                closeQuizButton.gameObject.SetActive(true);
                closeQuizButton.interactable = true;
            }

            if (quizFeedbackScroll != null) quizFeedbackScroll.verticalNormalizedPosition = 1f;
        }

        // Godot: use_quiz_result_layout()
        private void UseQuizResultLayout()
        {
            foreach (Button button in GetOptionButtons())
            {
                if (button != null) button.gameObject.SetActive(false);
            }

            if (nextQuizButton != null)
            {
                nextQuizButton.gameObject.SetActive(false);
                nextQuizButton.interactable = false;
            }

            if (closeQuizButton != null)
            {
                closeQuizButton.gameObject.SetActive(true);
                closeQuizButton.interactable = true;
            }
        }

        // Godot: update_quiz_progress()
        private void UpdateQuizProgress()
        {
            if (quizProgressLabel != null)
            {
                quizProgressLabel.text = "Question " + (currentQuizIndex + 1) + " / " + quizQuestions.Count;
            }

            if (quizProgressBar != null)
            {
                quizProgressBar.maxValue = quizQuestions.Count;
                quizProgressBar.value = currentQuizIndex + 1;
            }
        }

        // Godot: reset_option_button_styles() — see porting note 4 above.
        private void ResetOptionButtonStyles()
        {
            Button[] optionButtons = GetOptionButtons();

            for (int i = 0; i < optionButtons.Length; i++)
            {
                if (optionButtons[i] != null && optionButtons[i].image != null)
                {
                    optionButtons[i].image.color = defaultOptionButtonColors[i];
                }
            }
        }

        // Godot: set_button_color(button, color)
        private void SetButtonColor(Button button, Color color)
        {
            if (button == null || button.image == null) return;
            button.image.color = color;
        }

        /// Not a Godot port — Unity-specific, and DEFENSIVE ONLY. Read this before assuming
        /// it fixes anything on its own.
        ///
        /// This method was originally added while chasing a bug where QuizFeedbackScroll
        /// never scrolled: its Content's height stayed 0 at runtime even with real,
        /// wrapped, visibly-rendered text inside it, so ScrollRect believed there was
        /// nothing to scroll to and the feedback/review text past the visible box was
        /// unreachable. The diagnosis behind this method (that Unity's deferred layout /
        /// TMP geometry rebuilds were landing too late) was WRONG, and neither call below
        /// fixed that bug.
        ///
        /// The actual root cause was an Editor setup mistake, fixed in the scene, not here:
        /// ContentSizeFitter does not measure a GameObject's CHILDREN. It queries
        /// ILayoutElement components on its OWN GameObject. Content had a ContentSizeFitter
        /// but no LayoutGroup, so that query had nothing to ask and correctly returned 0.
        /// The fix was adding a VerticalLayoutGroup to Content (see
        /// Stage22_Quiz_Walkthrough.md 3.5) — that group is what reads QuizFeedbackLabel's
        /// preferred height and reports it as Content's own, which the fitter then applies.
        ///
        /// These two calls are kept because they're still genuinely useful once the layout
        /// is correct: ShowQuizResult() and DisplayQuizQuestion() both set
        /// verticalNormalizedPosition = 1f immediately after changing this text, and that
        /// scroll-to-top only lands correctly if Content has already been re-measured for
        /// the new text rather than on the next frame. They are not load-bearing for the
        /// scroll working at all — the scene setup is.
        private void RefreshFeedbackLayout()
        {
            if (quizFeedbackLabel != null) quizFeedbackLabel.ForceMeshUpdate();
            if (quizFeedbackScroll == null || quizFeedbackScroll.content == null) return;
            LayoutRebuilder.ForceRebuildLayoutImmediate(quizFeedbackScroll.content);
        }

        private Button[] GetOptionButtons()
        {
            return new[] { optionAButton, optionBButton, optionCButton, optionDButton };
        }

        private TMP_Text[] GetOptionTexts()
        {
            return new[] { optionAText, optionBText, optionCText, optionDText };
        }

        private void RebuildDashboardLayout()
        {
            // Matches every other controller's post-panel-text-change call —
            // see e.g. LinkedListController.RebuildDashboardLayout(). Quiz
            // has no dashboardContent RectTransform reference of its own
            // (DashboardController's SetOperation() already triggers its
            // own panel's content sizing); left as a no-op seam here in case
            // a future pass wants one, matching the established naming
            // convention.
        }

        private static void LogEvent(string action, string value, string result, string details)
        {
            ResearchLogger.Instance?.LogEvent(action, value, result, details);
        }
    }
}
