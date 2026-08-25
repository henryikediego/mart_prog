using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MartProg.Modules
{
    /// <summary>
    /// Port of ar_learning_screen.gd's Memory Management half — the activity
    /// dropdown, the per-activity button matrix, the progress tracker and the
    /// dashboard text. MemoryModule.cs owns the 3D table; this owns which
    /// activity is running and what the student is told about it.
    ///
    /// **Phase 26 scope.** Memory is by far the largest module in the app —
    /// roughly 1,500 lines in memory_module.gd plus ~55 memory functions in
    /// ar_learning_screen.gd, more than Functions, Loops and Selection
    /// combined — so it is built across five phases grouped by shared
    /// implementation pattern. This phase builds the foundation every later
    /// one needs, plus the two activities that need nothing beyond it:
    ///
    ///   26  (this)  Table visual, dropdown, button matrix, progress tracker,
    ///               dashboard, Overview, Data Types, Address Layout
    ///   26b         The six one-button step traces (Functions, Selection,
    ///               Scope, Memory Access, Loop Memory, Remove Variable)
    ///   26c         Variables and Arrays — text input, type inference,
    ///               validation, inspection, prediction
    ///   26d         Trace / Debug / Final Assessment state machines
    ///   26e         The 65-question quiz bank, after an audit
    ///
    /// The **full 14-activity dropdown and the full button matrix ship now**,
    /// exactly as ChangeModuleController lists all 8 modules before they are
    /// built. Unbuilt activities select cleanly and report that they are not
    /// available yet, so no later phase has to revisit this file's structure —
    /// only add handlers.
    ///
    /// ---- Porting notes ----
    ///
    /// 1. **Buttons are HIDDEN per activity, not merely relabelled.** Every
    ///    module before this one kept a fixed control strip and renamed its
    ///    buttons; Loops was the first to hide one, and it hid the same one
    ///    permanently. Memory calls set_memory_button_visibility() with seven
    ///    booleans and every activity passes a different combination —
    ///    Overview shows nothing but Quiz, Functions shows only Traverse,
    ///    Reset and Quiz. ActivityLayout below is that call site, turned into
    ///    a table so the fourteen combinations can be read at a glance
    ///    instead of scattered through a 250-line if/elif chain.
    ///
    /// 2. **The button grid is sized for all six buttons, even when an
    ///    activity shows one.** The layout is recomputed on every activity
    ///    change rather than only at startup, because the visible set keeps
    ///    changing — but the CELL SIZE is derived from every button wired in
    ///    the Inspector, not from the visible subset.
    ///
    ///    The first draft sized cells by the visible count, reasoning that
    ///    reserving six cells for one button would leave the strip full of
    ///    gaps. On screen that turned out backwards: Overview shows only
    ///    Quiz, so the grid collapsed to one 1x1 cell and the Quiz button
    ///    stretched across the entire strip at several times the size it has
    ///    in every other activity. Empty cells are the correct trade — a
    ///    button should not change size depending on how many of its
    ///    neighbours happen to be hidden.
    ///
    /// 3. **No stale button labels — checked, not assumed.** Source sets
    ///    button text inside the same branch that makes the button visible,
    ///    and never clears it when moving to an activity that hides it. That
    ///    is the shape of a real bug (switch away, switch back, read the
    ///    previous activity's label), so every branch was checked: each of
    ///    the four relabelled buttons has its text set by every activity that
    ///    shows it. Insert is set by all eight activities that show it,
    ///    Traverse by all six, and Search and Delete by the single activity
    ///    that shows each. The bug does not exist. Recorded because the check
    ///    is worth having on file, and because Phase 26d adds three more
    ///    Insert labels that must keep the property true.
    ///
    /// 4. **The Quiz button is hidden in Final Assessment, and that is
    ///    deliberate in source.** get_memory_activity_quiz_questions() holds
    ///    an explicitly empty list for "Final Assessment" while every other
    ///    activity has five questions, and configure_memory_buttons() passes
    ///    false for the quiz flag in that one branch. The two facts agree, so
    ///    this is a decision rather than an oversight, and it is ported. The
    ///    Unity Quiz button lives in this module's own control strip (Phase
    ///    22's per-module correction) rather than in a shared admin row, but
    ///    hiding it per activity works the same way.
    ///
    /// 5. **Memory's quiz bank is not wired until Phase 26e.**
    ///    QuizQuestionBank.GetQuestionsForModule("Memory") still returns an
    ///    empty list, so the Quiz button reports "No quiz questions are
    ///    available for this module" until then. That is source's own
    ///    behaviour for an empty bank, so it fails gracefully rather than
    ///    looking broken. The bank is keyed by activity name and will thread
    ///    through the same selectionMode-style parameter Phase 25d added.
    ///
    /// 6. **Godot hides two dashboard labels in Overview; Unity populates
    ///    them anyway.** update_memory_status_label() sets
    ///    index_explanation_label.visible = false and
    ///    operation_feedback_label.visible = false in Overview, so Godot
    ///    shows no memory status and no progress until an activity is picked.
    ///    That works because Godot's dashboard is a container that shrinks
    ///    around whatever is visible. Unity's six panels are permanent and
    ///    headed, so a hidden label is not absent — it is a labelled box with
    ///    nothing in it, which reads as broken.
    ///
    ///    The first draft blanked them faithfully and it was reported from
    ///    device as exactly that. Status and Progress are now populated in
    ///    every activity including Overview. Both statements are true at that
    ///    point anyway — "no variables allocated" and "0 of 14 completed" —
    ///    so nothing is invented, it is only shown earlier than Godot shows
    ///    it. Same reasoning applies to Operation, which falls back to
    ///    "No operation selected yet." exactly as every other module does.
    ///
    /// 7. **A Godot indentation accident, not reproduced.** In
    ///    update_memory_status_label(), the blocks that populate the memory
    ///    status and the progress line — and the function's own `return` —
    ///    sit INSIDE `if code_explanation_label != null:` rather than at the
    ///    branch's top level beside it. If that label were ever unassigned,
    ///    status and progress would silently stop updating. It is always
    ///    assigned in practice, so this never manifests, and it is the same
    ///    class of accident as QuizController's porting note 9. Unity updates
    ///    all three independently.
    ///
    /// 8. **"Next:" can point at an activity already completed.**
    ///    get_next_memory_activity() returns the entry AFTER the current one
    ///    in the fixed order, and only falls back to "first incomplete" when
    ///    the current activity is not in the list at all — which happens only
    ///    in Overview, since Overview is deliberately absent from
    ///    get_memory_activity_order(). So a student who completes activities
    ///    out of order is told to go somewhere they have already been. The
    ///    source comment says this is intentional ("This keeps the guided
    ///    sequence pedagogically correct"), so it is preserved rather than
    ///    corrected to "next incomplete".
    ///
    /// 9. **Progress counts 14 entries, and "Quiz" is one of them.**
    ///    memory_progress has 13 activity keys plus "Quiz", and Overview is
    ///    not tracked. So the counter reads "of 14" and the Quiz entry can
    ///    only be completed by taking the quiz — which is unreachable until
    ///    Phase 26e. Until then the maximum honest score is 13 of 14.
    ///
    /// 10. **Changing activity clears the table but NOT progress.**
    ///     set_memory_activity() calls memory_module.clear_activity_memory()
    ///     only when the activity actually changed, and the source comment is
    ///     explicit that memory_progress must not be reset here. Reset, by
    ///     contrast, DOES clear the current activity's progress
    ///     (reset_memory_activity_progress) while leaving every other
    ///     activity's intact. Both behaviours are ported exactly; they are
    ///     easy to conflate and the difference is visible in the progress
    ///     line.
    ///
    /// 11. **Reset writes an Operation message; Godot writes none.** A
    ///     DEVIATION, added in Phase 28b after an on-device report that the
    ///     Operation panel read "No operation selected yet." after every
    ///     Reset, in every activity.
    ///
    ///     Source is deliberate about clearing it —
    ///     `_handle_memory_reset_button()` sets all three of
    ///     current_memory_details_text / _explanation_text / _feedback_text to
    ///     "" under the comment *"Clear action/result details so reset looks
    ///     like the starting interface."* That is right for Godot, whose
    ///     dashboard is a single label stack: clearing the text removes the
    ///     line and the interface genuinely does look freshly started.
    ///
    ///     Unity's six panels are permanent and headed, so the same clear
    ///     produces a panel captioned "Operation" reading "No operation
    ///     selected yet." immediately after the student performed one — the
    ///     table, the input box and the activity's progress were all just
    ///     cleared. The panel contract's rule 3 (Migration Spec §8n) covers
    ///     this case by name: where Godot hides a label, Unity supplies the
    ///     TRUTHFUL default, and after a reset the truthful default is that a
    ///     reset happened.
    ///
    ///     Two consequences worth knowing: the message is worded per activity
    ///     (Memory resets one at a time, unlike the other seven modules), and
    ///     the Overview branch deliberately does NOT claim progress was
    ///     cleared, because ResetActivityProgress() skips Overview. It is also
    ///     why RefreshDashboard() no longer forces Operation to "" in
    ///     Overview — that would have swallowed this message.
    /// </summary>
    public class MemoryController : MonoBehaviour, IModuleController
    {
        // -----------------------------------------------------------------
        // Activity names — exact strings from setup_memory_mode_option()
        // -----------------------------------------------------------------

        public const string ActivityOverview = "Overview";
        public const string ActivityVariables = "Variables";
        public const string ActivityDataTypes = "Data Types";
        public const string ActivityAddressLayout = "Address Layout";
        public const string ActivityMemoryAccess = "Memory Access";
        public const string ActivityLoopMemory = "Loop Memory";
        public const string ActivityArrays = "Arrays";
        public const string ActivityFunctions = "Functions";
        public const string ActivitySelection = "Selection";
        public const string ActivityScope = "Scope";
        public const string ActivityRemoveVariable = "Remove Variable";
        public const string ActivityTraceExercise = "Trace Exercise";
        public const string ActivityDebugExercise = "Debug Exercise";
        public const string ActivityFinalAssessment = "Final Assessment";

        /// Godot: setup_memory_mode_option()'s add_item() order. Overview is
        /// first and is the default selection.
        private static readonly string[] DropdownActivities =
        {
            ActivityOverview,
            ActivityVariables,
            ActivityDataTypes,
            ActivityAddressLayout,
            ActivityMemoryAccess,
            ActivityLoopMemory,
            ActivityArrays,
            ActivityFunctions,
            ActivitySelection,
            ActivityScope,
            ActivityRemoveVariable,
            ActivityTraceExercise,
            ActivityDebugExercise,
            ActivityFinalAssessment
        };

        /// Godot: get_memory_activity_order(). Note it does NOT contain
        /// Overview, and it DOES contain "Quiz" — see porting notes 8 and 9.
        private static readonly string[] ActivityOrder =
        {
            ActivityVariables,
            ActivityDataTypes,
            ActivityAddressLayout,
            ActivityMemoryAccess,
            ActivityLoopMemory,
            ActivityArrays,
            ActivityFunctions,
            ActivitySelection,
            ActivityScope,
            ActivityRemoveVariable,
            ActivityTraceExercise,
            ActivityDebugExercise,
            ActivityFinalAssessment,
            "Quiz"
        };

        // A BuiltActivities set and a ReportNotBuilt() scope gate lived here
        // through Phases 26 to 26d, so an unbuilt activity would select
        // cleanly and say so rather than doing nothing. Both are gone as of
        // Phase 26e: all fourteen are implemented and there is nothing left
        // to gate. Recorded rather than silently deleted, because their
        // absence is what marks the module feature-complete.

        // -----------------------------------------------------------------
        // The per-activity button matrix — porting note 1
        // -----------------------------------------------------------------

        /// One row of configure_memory_buttons(). The seven booleans are
        /// set_memory_button_visibility()'s parameters in source order.
        private sealed class ActivityLayout
        {
            public readonly bool ShowInput;
            public readonly bool ShowInsert;
            public readonly bool ShowDelete;
            public readonly bool ShowSearch;
            public readonly bool ShowTraverse;
            public readonly bool ShowReset;
            public readonly bool ShowQuiz;

            // Empty string means "this activity does not set this label",
            // which per porting note 3 only ever happens for a button it
            // also hides.
            public readonly string InsertLabel;
            public readonly string DeleteLabel;
            public readonly string SearchLabel;
            public readonly string TraverseLabel;

            public readonly string InputPlaceholder;

            public ActivityLayout(
                bool showInput, bool showInsert, bool showDelete, bool showSearch,
                bool showTraverse, bool showReset, bool showQuiz,
                string insertLabel = "", string deleteLabel = "",
                string searchLabel = "", string traverseLabel = "",
                string inputPlaceholder = "")
            {
                ShowInput = showInput;
                ShowInsert = showInsert;
                ShowDelete = showDelete;
                ShowSearch = showSearch;
                ShowTraverse = showTraverse;
                ShowReset = showReset;
                ShowQuiz = showQuiz;
                InsertLabel = insertLabel;
                DeleteLabel = deleteLabel;
                SearchLabel = searchLabel;
                TraverseLabel = traverseLabel;
                InputPlaceholder = inputPlaceholder;
            }
        }

        /// configure_memory_buttons(), transcribed branch by branch.
        /// Parameter order: input, insert, delete, search, traverse, reset, quiz.
        private static readonly Dictionary<string, ActivityLayout> Layouts =
            new Dictionary<string, ActivityLayout>
            {
                // Reset is hidden in Overview — source comments it as
                // "reset hidden in overview", the only activity that does so.
                { ActivityOverview,
                    new ActivityLayout(false, false, false, false, false, false, true) },

                { ActivityVariables,
                    new ActivityLayout(true, true, true, false, false, true, true,
                        insertLabel: "Set/Inspect", deleteLabel: "Predict Step") },

                { ActivityArrays,
                    new ActivityLayout(true, true, false, true, false, true, true,
                        insertLabel: "Inspect", searchLabel: "Set Array") },

                { ActivityFunctions,
                    new ActivityLayout(false, false, false, false, true, true, true,
                        traverseLabel: "Function Call") },

                { ActivitySelection,
                    new ActivityLayout(false, false, false, false, true, true, true,
                        traverseLabel: "Run If-Else") },

                // Insert's label in the three assessment activities is
                // dynamic (Start / Submit Answer / Next Question / Completed)
                // and is applied in ApplyDynamicInsertLabel(). The value here
                // is the starting state.
                { ActivityTraceExercise,
                    new ActivityLayout(true, true, false, false, false, true, true,
                        insertLabel: "Start Trace", inputPlaceholder: "Enter answer") },

                { ActivityDebugExercise,
                    new ActivityLayout(true, true, false, false, false, true, true,
                        insertLabel: "Start Debug", inputPlaceholder: "Enter answer") },

                // The only activity that hides Quiz — porting note 4.
                { ActivityFinalAssessment,
                    new ActivityLayout(true, true, false, false, false, true, false,
                        insertLabel: "Start Assessment", inputPlaceholder: "Enter answer") },

                { ActivityDataTypes,
                    new ActivityLayout(false, true, false, false, false, true, true,
                        insertLabel: "Show Types") },

                { ActivityScope,
                    new ActivityLayout(false, false, false, false, true, true, true,
                        traverseLabel: "Scope Step") },

                { ActivityAddressLayout,
                    new ActivityLayout(false, true, false, false, false, true, true,
                        insertLabel: "Show Layout") },

                { ActivityMemoryAccess,
                    new ActivityLayout(false, false, false, false, true, true, true,
                        traverseLabel: "Access Step") },

                { ActivityLoopMemory,
                    new ActivityLayout(false, false, false, false, true, true, true,
                        traverseLabel: "Loop Step") },

                { ActivityRemoveVariable,
                    new ActivityLayout(false, false, false, false, true, true, true,
                        traverseLabel: "Remove Step") }
            };

        /// Godot's final `else` in configure_memory_buttons(). Unreachable
        /// today — every dropdown entry has its own branch — but ported so an
        /// activity added later without a layout degrades the same way.
        private static readonly ActivityLayout FallbackLayout =
            new ActivityLayout(true, true, false, false, false, true, true,
                insertLabel: "Set/Inspect");

        // -----------------------------------------------------------------
        // Inspector
        // -----------------------------------------------------------------

        [Header("Module Reference")]
        [Tooltip("Auto-found via FindFirstObjectByType if left empty.")]
        [SerializeField] private MemoryModule memoryModule;

        [Header("UI References")]
        [Tooltip("Godot's value_input. Hidden in most activities — see porting note 1.")]
        [SerializeField] private TMP_InputField valueInput;

        [Tooltip("Godot's memory_mode_option. Item TEXT is the activity identity.")]
        [SerializeField] private TMP_Dropdown activityDropdown;

        [Tooltip("Godot's insert_button. Relabelled per activity.")]
        [SerializeField] private Button insertButton;
        [Tooltip("Godot's delete_button. Only shown in Variables (\"Predict Step\").")]
        [SerializeField] private Button deleteButton;
        [Tooltip("Godot's search_button. Only shown in Arrays (\"Set Array\").")]
        [SerializeField] private Button searchButton;
        [Tooltip("Godot's traverse_button. The step button for six activities.")]
        [SerializeField] private Button traverseButton;
        [SerializeField] private Button resetButton;
        [Tooltip("The Quiz trigger button in THIS module's control strip. Assign the same Button " +
                 "GameObject here as on QuizController's Memory quiz-button field.")]
        [SerializeField] private Button quizButton;

        [Header("Button labels (assign the TMP text INSIDE each button)")]
        [SerializeField] private TMP_Text insertButtonLabel;
        [SerializeField] private TMP_Text deleteButtonLabel;
        [SerializeField] private TMP_Text searchButtonLabel;
        [SerializeField] private TMP_Text traverseButtonLabel;
        [SerializeField] private TMP_Text resetButtonLabel;
        [SerializeField] private TMP_Text quizButtonLabel;

        [Header("Layout (optional)")]
        [SerializeField] private GridLayoutGroup buttonGrid;
        [SerializeField] private RectTransform dashboardContent;

        // -----------------------------------------------------------------
        // State
        // -----------------------------------------------------------------

        private string currentActivity = ActivityOverview;

        // Godot: var memory_progress / var memory_progress_notes
        private readonly Dictionary<string, bool> memoryProgress = new Dictionary<string, bool>();
        private readonly Dictionary<string, string> memoryProgressNotes = new Dictionary<string, string>();

        // Godot: current_memory_details_text / _explanation_text / _feedback_text
        private string currentDetailsText = "";
        private string currentExplanationText = "";
        private string currentFeedbackText = "";

        private bool isBusy;

        public string CurrentActivity => currentActivity;

        // -----------------------------------------------------------------
        // Lifecycle
        // -----------------------------------------------------------------

        private void Start()
        {
            if (memoryModule == null)
            {
                memoryModule = FindFirstObjectByType<MemoryModule>();
            }

            if (memoryModule == null)
            {
                Debug.LogError("MemoryController: no MemoryModule found in the scene.");
            }

            if (ResearchLogger.Instance != null)
            {
                ResearchLogger.Instance.ModuleName = "Memory";
            }

            InitializeProgress();
            WireButtons();
            SetUpActivityDropdown();

            StartCoroutine(InitializeDeferred());
        }

        private IEnumerator InitializeDeferred()
        {
            yield return null;

            EnterModule();
        }

        public void EnterModule()
        {
            SetBusy(false);

            currentActivity = ActivityOverview;
            if (activityDropdown != null) activityDropdown.SetValueWithoutNotify(0);

            if (memoryModule != null)
            {
                memoryModule.ResetMemory();
            }

            currentDetailsText = "";
            currentExplanationText = "";
            currentFeedbackText = "";

            ApplyActivityLayout();
            RefreshDashboard();
        }

        /// Godot: the memory_progress dictionary literal. Built at runtime
        /// from ActivityOrder so the two can never drift apart — source keeps
        /// them as two separate literals that happen to agree.
        private void InitializeProgress()
        {
            memoryProgress.Clear();
            memoryProgressNotes.Clear();

            foreach (string key in ActivityOrder)
            {
                memoryProgress[key] = false;
            }
        }

        private void WireButtons()
        {
            if (insertButton != null) insertButton.onClick.AddListener(OnInsertClicked);
            if (deleteButton != null) deleteButton.onClick.AddListener(OnDeleteClicked);
            if (searchButton != null) searchButton.onClick.AddListener(OnSearchClicked);
            if (traverseButton != null) traverseButton.onClick.AddListener(OnTraverseClicked);
            if (resetButton != null) resetButton.onClick.AddListener(OnResetClicked);
        }

        // Godot: setup_memory_mode_option()
        private void SetUpActivityDropdown()
        {
            if (activityDropdown == null) return;

            activityDropdown.ClearOptions();
            activityDropdown.AddOptions(new List<string>(DropdownActivities));
            activityDropdown.SetValueWithoutNotify(0);
            activityDropdown.onValueChanged.AddListener(OnActivityDropdownChanged);
        }

        // Godot: _on_memory_mode_option_item_selected(index)
        public void OnActivityDropdownChanged(int index)
        {
            if (activityDropdown == null || isBusy) return;
            if (index < 0 || index >= activityDropdown.options.Count) return;

            SetMemoryActivity(activityDropdown.options[index].text);

            // Godot clears the input on every activity change.
            if (valueInput != null) valueInput.text = "";
        }

        // -----------------------------------------------------------------
        // Godot: func set_memory_activity(activity_name)
        // -----------------------------------------------------------------

        private void SetMemoryActivity(string activityName)
        {
            string previousActivity = currentActivity;
            currentActivity = activityName;

            if (activityDropdown != null)
            {
                for (int i = 0; i < activityDropdown.options.Count; i++)
                {
                    if (activityDropdown.options[i].text == activityName)
                    {
                        activityDropdown.SetValueWithoutNotify(i);
                        break;
                    }
                }
            }

            // Porting note 10: clears the VISUAL table only, and only on an
            // actual change. Progress is deliberately untouched.
            if (memoryModule != null && previousActivity != activityName)
            {
                memoryModule.ClearActivityMemory();
            }

            // Godot: "Clean starting interface for every Memory mode."
            currentDetailsText = "";
            currentExplanationText = "";
            currentFeedbackText = "";

            ApplyActivityLayout();
            RefreshDashboard();

            LogEvent("Memory Activity", activityName, "Changed",
                "Memory activity changed to " + activityName);
        }

        // -----------------------------------------------------------------
        // Godot: func configure_memory_buttons()
        // -----------------------------------------------------------------

        private ActivityLayout CurrentLayout =>
            Layouts.TryGetValue(currentActivity, out ActivityLayout layout) ? layout : FallbackLayout;

        private void ApplyActivityLayout()
        {
            ActivityLayout layout = CurrentLayout;

            SetActive(valueInput != null ? valueInput.gameObject : null, layout.ShowInput);
            SetActive(insertButton != null ? insertButton.gameObject : null, layout.ShowInsert);
            SetActive(deleteButton != null ? deleteButton.gameObject : null, layout.ShowDelete);
            SetActive(searchButton != null ? searchButton.gameObject : null, layout.ShowSearch);
            SetActive(traverseButton != null ? traverseButton.gameObject : null, layout.ShowTraverse);
            SetActive(resetButton != null ? resetButton.gameObject : null, layout.ShowReset);
            SetActive(quizButton != null ? quizButton.gameObject : null, layout.ShowQuiz);

            SetLabel(insertButtonLabel, layout.InsertLabel);
            SetLabel(deleteButtonLabel, layout.DeleteLabel);
            SetLabel(searchButtonLabel, layout.SearchLabel);
            SetLabel(traverseButtonLabel, layout.TraverseLabel);

            // Godot sets these two unconditionally at the end of
            // configure_memory_buttons(), after the if/elif chain.
            SetLabel(resetButtonLabel, "Reset");
            SetLabel(quizButtonLabel, "Quiz");

            // The three assessments cycle Insert's label through four states,
            // which the layout table cannot express as a constant.
            ApplyDynamicInsertLabel();

            if (valueInput != null && layout.InputPlaceholder != "")
            {
                TMP_Text placeholder = valueInput.placeholder as TMP_Text;
                if (placeholder != null) placeholder.text = layout.InputPlaceholder;
            }

            // Godot re-enables every button at the end of the same function.
            SetBusy(isBusy);

            // Porting note 2: the visible set just changed, so the grid has to
            // be recomputed rather than only sized once at startup.
            ApplyResponsiveButtonLayout();
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active) target.SetActive(active);
        }

        private static void SetLabel(TMP_Text label, string text)
        {
            if (label != null && text != "") label.text = text;
        }

        // -----------------------------------------------------------------
        // Button handlers
        // -----------------------------------------------------------------

        /// Godot: _handle_memory_insert_button(). Data Types and Address
        /// Layout have their own demos; everything else falls through to
        /// handle_memory_set_variable(), which is itself a dispatcher.
        private void OnInsertClicked()
        {
            if (isBusy) return;

            if (currentActivity == ActivityDataTypes)
            {
                HandleDataTypesDemo();
                return;
            }

            if (currentActivity == ActivityAddressLayout)
            {
                HandleAddressLayoutDemo();
                return;
            }

            if (currentActivity == ActivityFinalAssessment)
            {
                HandleFinalAssessmentButton();
                return;
            }

            AssessmentSpec spec = SpecFor(currentActivity);

            if (spec != null)
            {
                HandleAssessmentInput(spec);
                return;
            }

            HandleSetVariable();
        }

        /// Godot: _handle_memory_delete_button().
        ///
        /// *** DELIBERATE DEVIATION — the Predict Step button is wired to the
        /// *** prediction flow, which is unreachable in the Godot build.
        ///
        /// Source reads:
        ///
        ///     func _handle_memory_delete_button():
        ///         if current_memory_activity == "Remove Variable":
        ///             handle_memory_remove_step()
        ///         else:
        ///             handle_memory_set_variable()
        ///
        /// So Predict Step calls the same function Set/Inspect calls, and the
        /// two buttons behave identically. handle_memory_step() — the entire
        /// two-phase prediction flow — has NO callers anywhere in
        /// ar_learning_screen.gd, which makes awaiting_prediction permanently
        /// false and everything downstream of it dead: the 450px dashboard
        /// height, get_memory_mode_details_block()'s "Prediction task:" and
        /// "Next statement:" filters, and all six text markers in
        /// is_memory_variable_prediction_active().
        ///
        /// Source's intent is not ambiguous, though. The button is labelled
        /// "Predict Step", the Variables instruction reads "Use Predict Step
        /// for guided prediction", the module holds five prompts matching
        /// step_memory()'s five statements, normalize_prediction_value()
        /// exists to judge answers leniently, and a correct answer is written
        /// to mark_memory_activity_completed("Variables", ...). Everything is
        /// built except one call.
        ///
        /// Ported as intended rather than as written, at the user's decision.
        /// Set it back by calling HandleSetVariable() here instead.
        ///
        /// Source's own "Remove Variable" branch is dead for a second,
        /// unrelated reason: the layout matrix never shows the Delete button
        /// in that activity. Kept anyway, at no cost.
        private void OnDeleteClicked()
        {
            if (isBusy) return;

            if (currentActivity == ActivityRemoveVariable)
            {
                OnTraverseClicked();
                return;
            }

            HandleMemoryStep();
        }

        /// Godot: _handle_memory_search_button()
        private void OnSearchClicked()
        {
            if (isBusy) return;

            if (currentActivity != ActivityArrays)
            {
                SetOperation("Set Array is available in Arrays mode.");
                RefreshDashboard();
                return;
            }

            HandleSetArray();
        }

        // -----------------------------------------------------------------
        // Godot: func handle_memory_set_variable()
        // A dispatcher: typed commands, then the three assessment activities,
        // then empty input, then inspect-vs-assign, then array-vs-variable.
        // -----------------------------------------------------------------

        private void HandleSetVariable()
        {
            if (memoryModule == null)
            {
                SetOperation("Set Variable: Memory module is not assigned.");
                RefreshDashboard();
                return;
            }

            string typedValue = valueInput != null ? valueInput.text.Trim() : "";

            // Typed commands are checked FIRST, before empty-input handling —
            // so "arrays" switches activity rather than being read as a
            // variable name.
            if (HandleMemoryActivityCommand(typedValue)) return;

            if (typedValue == "")
            {
                // Source uses ONE fixed message here regardless of activity,
                // so pressing Inspect in Arrays with an empty box asks for
                // "int age=20" — Variables wording, in the wrong activity.
                //
                // Corrected, and source's own sibling branch is the precedent:
                // the inspect-FAILURE message a few lines below already words
                // itself differently in Arrays ("No array element named X was
                // found" vs "No memory record named X was found"). Source
                // clearly intended Arrays to speak about elements; it just
                // never applied that to the empty case.
                SetOperation(currentActivity == ActivityArrays
                    ? "Enter an array such as int marks[3]=70,80,65, update an element such " +
                      "as marks[2]=85, or inspect by typing an element name such as marks[2]."
                    : "Enter a variable such as int age=20, update such as age=25, " +
                      "or inspect by typing a memory record name such as age.");

                RefreshDashboard();

                LogEvent("Memory Set Variable", "", "Failed", "Empty input");
                return;
            }

            bool looksLikeAssignment = typedValue.Contains("=");
            bool looksLikeArrayAssignment =
                typedValue.Contains("[") && typedValue.Contains("]") && typedValue.Contains("=");

            // ----- Inspection: a bare name, e.g. "age" or "marks[2]" -----
            if (!looksLikeAssignment)
            {
                bool inspectSuccess = memoryModule.InspectMemoryRecord(typedValue);

                if (inspectSuccess)
                {
                    SetOperation(memoryModule.GetInspectionMessage());

                    LogEvent("Memory Inspect", typedValue, "Success",
                        memoryModule.GetInspectionMessage());
                }
                else
                {
                    // Source words the failure differently in Arrays — the
                    // only place this distinction appears.
                    SetOperation(currentActivity == ActivityArrays
                        ? "Inspect: No array element named " + typedValue + " was found."
                        : "Inspect: No memory record named " + typedValue + " was found.");

                    LogEvent("Memory Inspect", typedValue, "Failed",
                        memoryModule.GetLastActionMessage());
                }

                RefreshDashboard();
                return;
            }

            // ----- Array syntax typed into the variable handler -----
            // Source calls this "ARRAY INPUT SAFETY": marks[2]=85 works from
            // the Set/Inspect button too, not only from Set Array.
            if (looksLikeArrayAssignment)
            {
                if (!memoryModule.SetArrayFromText(typedValue))
                {
                    // A TYPE rejection explains itself; an unparseable input
                    // falls back to source's generic message. See
                    // MemoryModule.UpdateArrayElement()'s type-gate note.
                    string typeError = memoryModule.LastArrayTypeError;

                    SetOperation(typeError != ""
                        ? "Set Array: " + typeError
                        : "Set Array: Invalid input. Use examples like int marks[3]=70,80,65 " +
                          "or marks[2]=85.");

                    RefreshDashboard();

                    LogEvent("Memory Set Array", typedValue, "Failed",
                        typeError != "" ? typeError : "Invalid array input");
                    return;
                }

                SetOperation("Set Array: " + memoryModule.GetLastActionMessage());

                // Note this credits ARRAYS, even when typed while Variables is
                // the active activity. Source does the same.
                MarkActivityCompleted(ActivityArrays, memoryModule.GetLastActionMessage());

                RefreshDashboard();

                LogEvent("Memory Set Array", typedValue, "Success",
                    memoryModule.GetLastActionMessage());

                ClearInput();
                return;
            }

            // ----- Normal variable input -----
            if (!memoryModule.SetVariableFromText(typedValue))
            {
                SetOperation("Set Variable: " + memoryModule.GetLastActionMessage());

                RefreshDashboard();

                LogEvent("Memory Set Variable", typedValue, "Failed",
                    memoryModule.GetLastActionMessage());
                return;
            }

            SetOperation("Set Variable: " + memoryModule.GetCurrentCodeLine() +
                         " " + memoryModule.GetLastActionMessage());

            MarkActivityCompleted(ActivityVariables, memoryModule.GetLastActionMessage());

            RefreshDashboard();

            LogEvent("Memory Set Variable", typedValue, "Success",
                memoryModule.GetLastActionMessage());

            ClearInput();
        }

        // Godot: func handle_memory_set_array()
        private void HandleSetArray()
        {
            if (memoryModule == null)
            {
                SetOperation("Set Array: Memory module is not assigned.");
                RefreshDashboard();
                return;
            }

            string typedValue = valueInput != null ? valueInput.text.Trim() : "";

            if (typedValue == "")
            {
                currentExplanationText =
                    "Enter an array declaration such as int marks[5]=70,80,65,90,75 " +
                    "or update an element such as marks[2]=85.";
                currentDetailsText = "";
                currentFeedbackText =
                    "Set Array: Please enter an array, for example int marks[3]=70,80,65.";

                RefreshDashboard();
                return;
            }

            if (!memoryModule.SetArrayFromText(typedValue))
            {
                string typeError = memoryModule.LastArrayTypeError;

                if (typeError != "")
                {
                    // The value did not match the element type. Explain that
                    // rather than the input format, which was fine.
                    currentExplanationText =
                        "An array element can only hold values of the array's own type.";
                    currentDetailsText = "";
                    currentFeedbackText = "Set Array: " + typeError;
                }
                else
                {
                    currentExplanationText =
                        "Use formats like int marks[3]=70,80,65 or marks[2]=85.";
                    currentDetailsText = "";
                    currentFeedbackText =
                        "Set Array: Invalid input. Use examples like int marks[3]=70,80,65 " +
                        "or marks[2]=85.";
                }

                RefreshDashboard();

                LogEvent("Memory Set Array", typedValue, "Failed",
                    typeError != "" ? typeError : "Invalid array input");
                return;
            }

            // On success source sets the explanation to the Arrays activity's
            // own sentence — which get_memory_mode_details_block() then
            // filters out of Operation, because it is already showing in the
            // Explanation panel. See the dashboard contract note.
            currentExplanationText =
                "Array elements are stored in consecutive simulated memory locations.";
            currentDetailsText = "";
            currentFeedbackText = "Set Array: " + memoryModule.GetLastActionMessage();

            MarkActivityCompleted(ActivityArrays, memoryModule.GetLastActionMessage());

            RefreshDashboard();

            LogEvent("Memory Set Array", typedValue, "Success",
                memoryModule.GetLastActionMessage());
        }

        // -----------------------------------------------------------------
        // Godot: func handle_memory_step() — the prediction flow.
        // Unreachable in source; wired to Predict Step here. See
        // OnDeleteClicked()'s note.
        // -----------------------------------------------------------------

        private void HandleMemoryStep()
        {
            if (memoryModule == null)
            {
                SetOperation("Prediction: Memory module is not assigned.");
                RefreshDashboard();
                return;
            }

            // PHASE 1 — first press prepares and shows the prediction.
            if (!memoryModule.IsWaitingForPrediction())
            {
                if (!memoryModule.PrepareNextMemoryPrediction())
                {
                    SetOperation("Program finished.\n" +
                                 "Step: " + memoryModule.GetLastActionMessage());

                    RefreshDashboard();

                    LogEvent("Memory Prediction", "", "Finished", "Memory trace completed");
                    return;
                }

                SetOperation(GetPredictionDisplayText(false));

                RefreshDashboard();

                LogEvent("Memory Prediction Prompt", memoryModule.GetPendingCodeLine(),
                    "Shown", memoryModule.GetPredictionPrompt());
                return;
            }

            // PHASE 2 — second press checks the typed answer.
            string userPrediction = valueInput != null ? valueInput.text.Trim() : "";

            if (userPrediction == "")
            {
                SetOperation(GetPredictionDisplayText(true));
                RefreshDashboard();
                return;
            }

            StartCoroutine(RunPredictionCheck(userPrediction));
        }

        private IEnumerator RunPredictionCheck(string userPrediction)
        {
            SetBusy(true);

            MemoryModule.PredictionResult result = null;

            yield return memoryModule.ExecutePendingMemoryStep(userPrediction, r => result = r);

            if (result != null)
            {
                if (currentActivity == ActivityVariables && result.Correct)
                {
                    MarkActivityCompleted(ActivityVariables,
                        "Prediction completed correctly. " + result.Message);
                }

                string executedText = result.ExecutedLine ?? "Program finished.";

                string predictionFeedback = result.Correct
                    ? "Prediction: Correct. Memory was updated as expected."
                    : "Prediction: Not correct. Expected value: " + result.Expected + ".";

                SetOperation(executedText + "\n" + result.Message + "\n" + predictionFeedback);

                RefreshDashboard();

                LogEvent("Memory Prediction Check", userPrediction,
                    result.Correct ? "Correct" : "Incorrect",
                    "Expected: " + result.Expected);
            }

            ClearInput();

            SetBusy(false);

            ApplyActivityLayout();
        }

        // Godot: func get_memory_prediction_display_text(empty_answer := false)
        private string GetPredictionDisplayText(bool emptyAnswer)
        {
            if (memoryModule == null) return "Prediction: Memory module is not assigned.";

            string pendingLine = memoryModule.GetPendingCodeLine().Trim();
            string prompt = memoryModule.GetPredictionPrompt().Trim();

            string displayText = "";

            if (pendingLine != "") displayText += "Next statement: " + pendingLine + "\n";

            displayText += prompt != ""
                ? "Question: " + prompt + "\n"
                : "Question: Predict the value that will be stored after this statement runs.\n";

            displayText += emptyAnswer
                ? "Please enter your predicted value before pressing Predict Step again."
                : "Type your predicted value in the input box, then press Predict Step again.";

            return displayText;
        }

        // -----------------------------------------------------------------
        // Godot: func handle_memory_activity_command(typed_value) -> bool
        // Typed shortcuts that switch activity or run a command. Checked
        // before any other input handling, so "arrays" switches rather than
        // being read as a variable name to inspect.
        // -----------------------------------------------------------------

        private static readonly Dictionary<string, string> ActivityCommands =
            new Dictionary<string, string>
            {
                { "mode overview", ActivityOverview }, { "overview", ActivityOverview },
                { "home", ActivityOverview },

                { "mode variables", ActivityVariables }, { "variables", ActivityVariables },
                { "mode variable", ActivityVariables },

                { "mode arrays", ActivityArrays }, { "arrays", ActivityArrays },
                { "mode array", ActivityArrays },

                { "mode functions", ActivityFunctions }, { "functions", ActivityFunctions },
                { "mode function", ActivityFunctions },

                { "mode selection", ActivitySelection }, { "selection", ActivitySelection },
                { "mode if", ActivitySelection }, { "mode if-else", ActivitySelection },

                { "mode trace", ActivityTraceExercise }, { "trace mode", ActivityTraceExercise },
                { "mode trace exercise", ActivityTraceExercise }, { "trace", ActivityTraceExercise },

                { "mode debug", ActivityDebugExercise }, { "debug mode", ActivityDebugExercise },
                { "mode debug exercise", ActivityDebugExercise }, { "debug", ActivityDebugExercise },

                { "mode assessment", ActivityFinalAssessment },
                { "mode final", ActivityFinalAssessment },
                { "mode final assessment", ActivityFinalAssessment },
                { "assessment", ActivityFinalAssessment },

                { "mode data types", ActivityDataTypes }, { "data types", ActivityDataTypes },
                { "datatypes", ActivityDataTypes }, { "mode datatype", ActivityDataTypes },

                { "mode scope", ActivityScope }, { "scope", ActivityScope },

                { "mode address", ActivityAddressLayout },
                { "mode address layout", ActivityAddressLayout },
                { "address layout", ActivityAddressLayout },

                { "mode access", ActivityMemoryAccess },
                { "mode memory access", ActivityMemoryAccess },
                { "memory access", ActivityMemoryAccess }, { "access", ActivityMemoryAccess },

                { "mode loop", ActivityLoopMemory }, { "mode loop memory", ActivityLoopMemory },
                { "loop memory", ActivityLoopMemory }, { "loop", ActivityLoopMemory },

                { "mode remove", ActivityRemoveVariable },
                { "mode remove variable", ActivityRemoveVariable },
                { "remove variable", ActivityRemoveVariable }, { "remove", ActivityRemoveVariable }
            };

        private bool HandleMemoryActivityCommand(string typedValue)
        {
            string command = (typedValue ?? "").Trim().ToLowerInvariant();

            if (command == "") return false;

            if (command == "help" || command == "modes" || command == "mode")
            {
                currentExplanationText = "Type a mode command to switch activities.";
                currentDetailsText =
                    "Modes: variables | arrays | functions | selection | trace | debug | assessment";
                currentFeedbackText = "Example: type mode arrays, then click Set/Inspect.";

                RefreshDashboard();
                ClearInput();
                return true;
            }

            if (ActivityCommands.TryGetValue(command, out string activity))
            {
                SetMemoryActivity(activity);
                ClearInput();
                return true;
            }

            if (command == "start")
            {
                AssessmentSpec startSpec = SpecFor(currentActivity);

                if (startSpec != null)
                {
                    ClearInput();
                    StartAssessment(startSpec);
                    return true;
                }

                SetOperation(
                    "Start is used in Trace Exercise, Debug Exercise, or Final Assessment mode.");

                RefreshDashboard();
                ClearInput();
                return true;
            }

            if (command == "reset progress" || command == "progress reset")
            {
                // Godot: reset_memory_progress() — clears EVERY activity, not
                // just the current one, and logs it as its own event.
                foreach (string key in ActivityOrder) memoryProgress[key] = false;
                memoryProgressNotes.Clear();

                LogEvent("Memory Progress", "", "Reset", "Memory progress tracker reset");

                currentExplanationText = GetActivityExplanation();
                currentDetailsText = "";
                currentFeedbackText = "Memory progress has been reset.";

                RefreshDashboard();
                ClearInput();
                return true;
            }

            return false;
        }

        private void ClearInput()
        {
            if (valueInput != null) valueInput.text = "";
        }

        // -----------------------------------------------------------------
        // Phase 26e — Trace Exercise, Debug Exercise, Final Assessment.
        //
        // Godot writes these three out separately, three times over, with the
        // identifiers renamed — three sets of counters, three display
        // builders, three completion displays, three question-key helpers.
        // They are one shape here, parameterised by AssessmentSpec.
        //
        // The button cycles Start -> Submit Answer -> Next Question ->
        // Completed, which is why the Insert label is computed at layout time
        // rather than being a constant in the layout table.
        // -----------------------------------------------------------------

        private sealed class AssessmentSpec
        {
            public readonly string Activity;
            public readonly MemoryModule.AssessmentBank Bank;
            public readonly int Total;
            public readonly string LogAction;
            public readonly string DisplayName;
            public readonly string StartLabel;
            public readonly string StartPrompt;
            public readonly string UnavailableText;
            public readonly string StartedDetail;
            public readonly string RepeatedDetail;
            public readonly string ResultLabel;
            public readonly bool InlineCode;
            public readonly bool VaryingResultLabel;

            public AssessmentSpec(
                string activity, MemoryModule.AssessmentBank bank, int total, string logAction,
                string displayName, string startLabel, string startPrompt, string unavailableText,
                string startedDetail, string repeatedDetail, string resultLabel,
                bool inlineCode, bool varyingResultLabel)
            {
                Activity = activity; Bank = bank; Total = total; LogAction = logAction;
                DisplayName = displayName; StartLabel = startLabel; StartPrompt = startPrompt;
                UnavailableText = unavailableText; StartedDetail = startedDetail;
                RepeatedDetail = repeatedDetail; ResultLabel = resultLabel;
                InlineCode = inlineCode; VaryingResultLabel = varyingResultLabel;
            }
        }

        /// Godot: MEMORY_TRACE_TOTAL_QUESTIONS / MEMORY_DEBUG_TOTAL_QUESTIONS /
        /// MEMORY_FINAL_ASSESSMENT_TOTAL_QUESTIONS, all 5.
        ///
        /// **Debug's bank holds only FOUR exercises against a total of five.**
        /// That is not a porting slip — source really is inconsistent. The
        /// count is never reached by answering distinct questions; instead
        /// start_debug_exercise() wraps its index back to 0 and re-serves
        /// question 1, and the repeated-question guard below catches it and
        /// ends the exercise at 4. Source's own comment on that guard calls
        /// it a "Critical fix", which reads like it was added after exactly
        /// this was noticed. Preserved: the guard makes the behaviour correct,
        /// and lowering the constant to 4 would change the score denominator
        /// students see.
        private static readonly AssessmentSpec TraceSpec = new AssessmentSpec(
            ActivityTraceExercise, MemoryModule.AssessmentBank.Trace, 5,
            "Memory Trace Exercise", "Trace Exercise", "Start Trace",
            "Click Start Trace to display a tracing question. Then enter your answer.",
            "Trace exercise unavailable.", "Trace question displayed",
            "Repeated first trace question detected. Trace exercise stopped.",
            "Trace Result: ", false, false);

        private static readonly AssessmentSpec DebugSpec = new AssessmentSpec(
            ActivityDebugExercise, MemoryModule.AssessmentBank.Debug, 5,
            "Memory Debug Exercise", "Debug Exercise", "Start Debug",
            "Click Start Debug to display a debugging question. Then enter your answer.",
            "Debug exercise unavailable.", "Debug question displayed",
            "Repeated first debug question detected. Debug exercise stopped.",
            "", true, true);

        private static readonly AssessmentSpec FinalSpec = new AssessmentSpec(
            ActivityFinalAssessment, MemoryModule.AssessmentBank.Final, 5,
            "Memory Final Assessment", "Final Assessment", "Start Assessment",
            "Click Start Assessment first to display an assessment question. Then enter your answer.",
            "Final Assessment unavailable.", "Assessment question displayed",
            "Repeated first assessment item detected. Final Assessment stopped.",
            "Final Result: ", true, false);

        private sealed class AssessmentState
        {
            public bool Completed;
            public int AnswerCount;
            public int CorrectCount;
            public bool ReadyForNext;
            public readonly HashSet<string> SeenKeys = new HashSet<string>();

            public void Reset()
            {
                Completed = false;
                AnswerCount = 0;
                CorrectCount = 0;
                ReadyForNext = false;
                SeenKeys.Clear();
            }
        }

        private readonly Dictionary<string, AssessmentState> assessmentStates =
            new Dictionary<string, AssessmentState>
            {
                { ActivityTraceExercise, new AssessmentState() },
                { ActivityDebugExercise, new AssessmentState() },
                { ActivityFinalAssessment, new AssessmentState() }
            };

        private static AssessmentSpec SpecFor(string activity)
        {
            if (activity == ActivityTraceExercise) return TraceSpec;
            if (activity == ActivityDebugExercise) return DebugSpec;
            if (activity == ActivityFinalAssessment) return FinalSpec;
            return null;
        }

        /// Godot: get_trace_question_key() / get_debug_question_key() /
        /// get_final_assessment_question_key() — three identical functions.
        private static string QuestionKey(string code, string question)
        {
            return question.Trim().ToLowerInvariant() + " || " + code.Trim().ToLowerInvariant();
        }

        /// Godot: configure_memory_buttons()'s four-state label for these
        /// three activities. Applied after the layout table, because the
        /// table can only hold a constant.
        private void ApplyDynamicInsertLabel()
        {
            AssessmentSpec spec = SpecFor(currentActivity);
            if (spec == null || insertButtonLabel == null) return;

            AssessmentState state = assessmentStates[spec.Activity];

            string label;

            if (state.Completed) label = "Completed";
            else if (memoryModule != null && memoryModule.IsWaitingForAnswer(spec.Bank))
                label = "Submit Answer";
            else if (state.ReadyForNext) label = "Next Question";
            else label = spec.StartLabel;

            insertButtonLabel.text = label;
        }

        /// Godot: start_memory_trace_activity() / start_memory_debug_activity()
        /// / the second half of handle_memory_final_assessment_button().
        private void StartAssessment(AssessmentSpec spec)
        {
            if (memoryModule == null)
            {
                SetOperation(spec.DisplayName + ": Memory module is not assigned.");
                RefreshDashboard();
                return;
            }

            AssessmentState state = assessmentStates[spec.Activity];

            if (state.Completed)
            {
                ShowCompletionDisplay(spec);
                return;
            }

            state.ReadyForNext = false;

            if (!memoryModule.StartExercise(spec.Bank))
            {
                if (state.AnswerCount > 0)
                {
                    CompleteAssessment(spec, state, state.AnswerCount);
                }
                else
                {
                    SetOperation(spec.UnavailableText);
                    RefreshDashboard();
                }

                ApplyActivityLayout();
                return;
            }

            string code = memoryModule.GetExerciseCode(spec.Bank).Trim();
            string question = memoryModule.GetExerciseQuestion(spec.Bank).Trim();
            string key = QuestionKey(code, question);

            // Source calls this a "Critical fix": the module wraps its index
            // and re-serves question 1 rather than reporting that it ran out,
            // so a repeat is the only signal that the bank is exhausted.
            if (state.SeenKeys.Contains(key) && state.AnswerCount > 0)
            {
                CompleteAssessment(spec, state, state.AnswerCount);

                LogEvent(spec.LogAction, "", "Completed", spec.RepeatedDetail);

                ApplyActivityLayout();
                return;
            }

            state.SeenKeys.Add(key);

            ShowAssessmentDisplay(spec, "", code, question);

            LogEvent(spec.LogAction, code, "Started", spec.StartedDetail);

            ClearInput();
            ApplyActivityLayout();
        }

        /// Godot: the Trace / Debug / Final branches inside
        /// handle_memory_set_variable(). One function; source has three.
        private void HandleAssessmentInput(AssessmentSpec spec)
        {
            if (memoryModule == null)
            {
                SetOperation(spec.DisplayName + ": Memory module is not assigned.");
                RefreshDashboard();
                return;
            }

            AssessmentState state = assessmentStates[spec.Activity];

            if (state.Completed)
            {
                ShowCompletionDisplay(spec);
                return;
            }

            string typedValue = valueInput != null ? valueInput.text.Trim() : "";

            if (!memoryModule.IsWaitingForAnswer(spec.Bank))
            {
                if (typedValue == "")
                {
                    StartAssessment(spec);
                    return;
                }

                // Typing before starting does NOT start the exercise — it
                // explains that the button comes first.
                ShowAssessmentDisplay(spec, spec.StartPrompt);
                return;
            }

            // Captured BEFORE checking, because CheckExerciseAnswer() clears
            // the waiting flag and the display would otherwise lose the
            // question it is reporting on.
            string codeBefore = memoryModule.GetExerciseCode(spec.Bank).Trim();
            string questionBefore = memoryModule.GetExerciseQuestion(spec.Bank).Trim();

            if (typedValue == "")
            {
                ShowAssessmentDisplay(spec,
                    "Enter your answer in the input box, then click Submit Answer.",
                    codeBefore, questionBefore);
                return;
            }

            MemoryModule.AnswerResult result =
                memoryModule.CheckExerciseAnswer(spec.Bank, typedValue);

            state.AnswerCount++;
            if (result.Correct) state.CorrectCount++;

            string resultText = (result.Correct ? "Correct. " : "Not correct. ") + result.Explanation;

            if (state.AnswerCount >= spec.Total)
            {
                // Note the denominator here is the CONSTANT, while the
                // completion display uses the actual answer count. They differ
                // for Debug, whose bank holds four. Source's own asymmetry.
                state.Completed = true;
                state.ReadyForNext = false;

                MarkActivityCompleted(spec.Activity,
                    spec.DisplayName + " completed. Score: " +
                    state.CorrectCount + "/" + spec.Total);
            }
            else
            {
                state.ReadyForNext = true;
            }

            ShowAssessmentDisplay(spec, resultText, codeBefore, questionBefore);

            LogEvent(spec.LogAction, typedValue, result.Correct ? "Correct" : "Incorrect",
                "Question " + state.AnswerCount + "/" + spec.Total +
                " | Expected: " + result.Expected + " | " + result.Explanation);

            ClearInput();
            ApplyActivityLayout();
        }

        /// Godot: handle_memory_final_assessment_button()
        /// Final Assessment splits start and submit across one button, where
        /// Trace and Debug let one function decide. Same visible behaviour.
        private void HandleFinalAssessmentButton()
        {
            if (memoryModule == null)
            {
                SetOperation("Final Assessment: Memory module is not assigned.");
                RefreshDashboard();
                return;
            }

            AssessmentState state = assessmentStates[ActivityFinalAssessment];

            if (state.Completed)
            {
                ShowCompletionDisplay(FinalSpec);
                return;
            }

            if (memoryModule.IsWaitingForFinalAssessmentAnswer())
            {
                HandleAssessmentInput(FinalSpec);
                return;
            }

            StartAssessment(FinalSpec);
        }

        private void CompleteAssessment(AssessmentSpec spec, AssessmentState state, int denominator)
        {
            state.Completed = true;

            MarkActivityCompleted(spec.Activity,
                spec.DisplayName + " completed. Score: " +
                state.CorrectCount + "/" + denominator);

            ShowCompletionDisplay(spec);
        }

        /// Godot: set_trace_completion_display() / set_debug_completion_display()
        /// / set_final_assessment_completion_display() — three identical
        /// functions. Note the denominator is the ANSWER COUNT, not the
        /// constant, so an exercise stopped early reports honestly.
        private void ShowCompletionDisplay(AssessmentSpec spec)
        {
            AssessmentState state = assessmentStates[spec.Activity];

            SetOperation(
                "Completed: " + spec.DisplayName + " completed. Score: " +
                state.CorrectCount + "/" + state.AnswerCount +
                ".\nClick the Reset button to start again.");

            RefreshDashboard();
        }

        /// Godot: set_trace_exercise_display() / set_debug_exercise_display()
        /// / set_final_assessment_display().
        ///
        /// Three builders with three different code formats, all preserved:
        ///
        /// - Trace shows its code RAW, across multiple lines, with no "Code: "
        ///   prefix — source has that prefix commented out in place.
        /// - Debug and Final flatten the code onto ONE line, joining lines
        ///   with five spaces, under a "Code: " prefix. Source's comment says
        ///   why: "keep short debug code on one line to avoid vertical
        ///   overflow" — a dashboard-height concern.
        /// - Debug varies its result label ("Answer:" / "Completed:" /
        ///   "Result:") by inspecting the message text; the other two use a
        ///   fixed label.
        private void ShowAssessmentDisplay(
            AssessmentSpec spec, string resultText,
            string codeOverride = "", string questionOverride = "")
        {
            if (memoryModule == null)
            {
                SetOperation(spec.DisplayName + ": Memory module is not assigned.");
                RefreshDashboard();
                return;
            }

            string code = codeOverride.Trim();
            string question = questionOverride.Trim();

            if (code == "") code = memoryModule.GetExerciseCode(spec.Bank).Trim();
            if (question == "") question = memoryModule.GetExerciseQuestion(spec.Bank).Trim();

            string resultClean = resultText.Trim();
            string display = "";

            if (question != "") display += "Question: " + question;

            if (code != "")
            {
                if (spec.InlineCode)
                {
                    string inlineCode = "";

                    foreach (string line in code.Split('\n'))
                    {
                        string cleanedLine = line.Trim();
                        if (cleanedLine == "") continue;

                        if (inlineCode != "") inlineCode += "     ";
                        inlineCode += cleanedLine;
                    }

                    if (inlineCode != "")
                    {
                        if (display != "") display += "\n";
                        display += "Code: " + inlineCode;
                    }
                }
                else
                {
                    if (display != "") display += "\n";
                    display += code;
                }
            }

            if (resultClean != "")
            {
                if (display != "") display += "\n\n";

                if (spec.VaryingResultLabel)
                {
                    if (resultClean.StartsWith("Enter your answer") ||
                        resultClean.StartsWith("Please enter"))
                    {
                        display += "Answer: " + resultClean;
                    }
                    else if (resultClean.StartsWith(spec.DisplayName + " completed"))
                    {
                        display += "Completed: " +
                                   resultClean.Replace(spec.DisplayName + " completed. ", "");
                    }
                    else
                    {
                        display += "Result: " + resultClean;
                    }
                }
                else
                {
                    display += spec.ResultLabel + resultClean;
                }
            }

            SetOperation(display);
            RefreshDashboard();
        }

        /// Godot: _handle_memory_traverse_button(). Six activities, one shape.
        ///
        /// Each is `set_buttons_disabled(true)` -> `await
        /// memory_module.step_*()` -> read back `get_last_action_message()`
        /// -> write the Operation line -> log -> re-enable. The ONLY things
        /// that differ are four strings and which step coroutine runs, so
        /// they are a table here rather than six near-identical handlers.
        ///
        /// Two source inconsistencies preserved in that table:
        ///
        /// - Memory Access logs under "Memory Access", which is also its
        ///   activity name, while every other activity logs under a
        ///   "Memory <Verb>" name.
        /// - Loop Memory's activity is "Loop Memory" but it logs under
        ///   "Memory Loop". Every other pair matches.
        ///
        /// Neither affects behaviour, but both affect research-log grouping,
        /// so they are ported rather than normalised — the same reasoning as
        /// FunctionsController's mixed log names.
        private sealed class StepTrace
        {
            public readonly string Activity;
            public readonly string Prefix;
            public readonly string LogAction;
            public readonly string FinishedDetail;

            public StepTrace(string activity, string prefix, string logAction, string finishedDetail)
            {
                Activity = activity;
                Prefix = prefix;
                LogAction = logAction;
                FinishedDetail = finishedDetail;
            }
        }

        private static readonly Dictionary<string, StepTrace> StepTraces =
            new Dictionary<string, StepTrace>
            {
                { ActivityFunctions, new StepTrace(
                    ActivityFunctions, "Function Call: ", "Memory Function Call",
                    "Function call memory trace completed") },

                { ActivitySelection, new StepTrace(
                    ActivitySelection, "Run If-Else: ", "Memory Selection",
                    "Selection memory trace completed") },

                { ActivityScope, new StepTrace(
                    ActivityScope, "Scope Step: ", "Memory Scope",
                    "Scope memory trace completed") },

                { ActivityMemoryAccess, new StepTrace(
                    ActivityMemoryAccess, "Access Step: ", "Memory Access",
                    "Memory access trace completed") },

                { ActivityLoopMemory, new StepTrace(
                    ActivityLoopMemory, "Loop Step: ", "Memory Loop",
                    "Loop memory trace completed") },

                { ActivityRemoveVariable, new StepTrace(
                    ActivityRemoveVariable, "Remove Step: ", "Memory Remove Variable",
                    "Remove Variable trace completed") }
            };

        private void OnTraverseClicked()
        {
            if (isBusy) return;

            if (!StepTraces.TryGetValue(currentActivity, out StepTrace trace))
            {
                // Godot's else branch, verbatim. Reachable only if the button
                // is somehow shown outside these six — the layout table hides
                // it everywhere else.
                SetOperation(
                    "This action is available in Functions, Selection, Scope, " +
                    "Memory Access, Loop Memory, or Remove Variable mode.");
                RefreshDashboard();
                return;
            }

            if (memoryModule == null)
            {
                SetOperation(trace.Prefix + "Memory module is not assigned.");
                RefreshDashboard();
                return;
            }

            StartCoroutine(RunStepTrace(trace));
        }

        private IEnumerator RunStepTrace(StepTrace trace)
        {
            // Godot re-selects the activity if it somehow does not match.
            // Dead in practice — the button is only visible in its own
            // activity — but ported, and harmless because SetMemoryActivity()
            // only clears the table when the activity actually changes.
            if (currentActivity != trace.Activity)
            {
                SetMemoryActivity(trace.Activity);
            }

            SetBusy(true);

            string executedLine = null;
            bool finished = false;

            IEnumerator step = StepCoroutineFor(trace.Activity, line =>
            {
                executedLine = line;
                finished = line == null;
            });

            yield return step;

            string actionMessage = memoryModule.GetLastActionMessage();

            // Loop Memory only: source rewrites the setup step's message so it
            // reads as Loop Memory rather than Arrays, because step 1 calls
            // allocate_array() and inherits its wording. Two sequential ifs,
            // not if/else — but the first rewrite produces text containing
            // neither "array" nor "allocated", so the second can never fire
            // after it. Ported in the same order for that reason.
            if (trace.Activity == ActivityLoopMemory)
            {
                string lowered = actionMessage.ToLowerInvariant();

                if (lowered.Contains("array") && lowered.Contains("stored"))
                {
                    actionMessage =
                        "Loop setup completed. The loop values were placed in consecutive " +
                        "memory locations so the loop can read them one by one.";
                    lowered = actionMessage.ToLowerInvariant();
                }

                if (lowered.Contains("array") && lowered.Contains("allocated"))
                {
                    actionMessage =
                        "Loop setup completed. The loop values were prepared in memory for iteration.";
                }
            }

            if (finished)
            {
                currentDetailsText = trace.Prefix + actionMessage;
                currentExplanationText = "";
                currentFeedbackText = "";

                MarkActivityCompleted(trace.Activity, actionMessage);

                RefreshDashboard();

                LogEvent(trace.LogAction, "", "Finished", trace.FinishedDetail);
            }
            else
            {
                // Source puts the executed code line on its own first line,
                // then the prefixed action message beneath it.
                currentDetailsText = executedLine + "\n" + trace.Prefix + actionMessage;
                currentExplanationText = "";
                currentFeedbackText = "";

                RefreshDashboard();

                LogEvent(trace.LogAction, executedLine, "Success", actionMessage);
            }

            SetBusy(false);

            ApplyActivityLayout();
        }

        private IEnumerator StepCoroutineFor(string activity, System.Action<string> onComplete)
        {
            switch (activity)
            {
                case ActivityFunctions: return memoryModule.StepFunctionCallMemory(onComplete);
                case ActivitySelection: return memoryModule.StepSelectionMemory(onComplete);
                case ActivityScope: return memoryModule.StepScopeMemory(onComplete);
                case ActivityMemoryAccess: return memoryModule.StepMemoryAccess(onComplete);
                case ActivityLoopMemory: return memoryModule.StepLoopMemory(onComplete);
                case ActivityRemoveVariable: return memoryModule.StepRemoveMemory(onComplete);
                default: return null;
            }
        }

        // Godot: func handle_memory_data_types_demo()
        private void HandleDataTypesDemo()
        {
            if (memoryModule == null)
            {
                SetOperation("Data Types: Memory module is not assigned.");
                RefreshDashboard();
                return;
            }

            memoryModule.ShowDataTypeMemoryDemo();

            currentDetailsText =
                "Displayed common data types in memory: int, float, char, bool, and string.\n" +
                "Each row shows the variable name, data type, stored value, simulated address, and memory size.";

            currentExplanationText = "";
            currentFeedbackText = "";

            MarkActivityCompleted(ActivityDataTypes, "Common data types displayed.");

            RefreshDashboard();

            LogEvent("Memory Data Types", "Show Types", "Success",
                "Displayed common data types with values, addresses, and memory sizes.");
        }

        // Godot: func handle_memory_address_layout_demo()
        private void HandleAddressLayoutDemo()
        {
            if (memoryModule == null)
            {
                SetOperation("Address Layout: Memory module is not assigned.");
                RefreshDashboard();
                return;
            }

            memoryModule.ShowAddressLayoutDemo();

            currentDetailsText =
                "The memory table displayed how memory addresses increase according to data type size.\n" +
                "For example, an int uses 4 bytes, so the next address moves forward by 4. " +
                "A char or bool uses 1 byte, so the next address moves forward by 1.";

            currentExplanationText = "";
            currentFeedbackText = "";

            MarkActivityCompleted(ActivityAddressLayout, "Address layout displayed.");

            RefreshDashboard();

            LogEvent("Memory Address Layout", "Show Layout", "Success",
                "The memory table displayed how memory addresses increase based on data type size.");
        }

        /// Godot: func _handle_memory_reset_button()
        /// Porting note 10: unlike an activity change, this DOES clear the
        /// current activity's progress — and only that one.
        private void OnResetClicked()
        {
            string activityBeforeReset = currentActivity;

            if (memoryModule != null) memoryModule.ResetMemory();

            currentActivity = activityBeforeReset;

            if (valueInput != null) valueInput.text = "";

            currentDetailsText = "";
            currentExplanationText = "";

            // DEVIATION from source, reported from device in Phase 28b: Godot
            // clears all three here ("Clear action/result details so reset
            // looks like the starting interface") and that is correct for its
            // ONE-label dashboard — clearing simply removes the line.
            //
            // Unity has six permanent headed panels, so an empty Operation
            // falls back to "No operation selected yet." — which after
            // pressing Reset reads as though nothing happened, when in fact
            // the table, the input and this activity's progress were all just
            // cleared. That is the Phase 26b panel contract's rule 3 exactly:
            // where Godot hides a label, Unity supplies the TRUTHFUL default,
            // and the truthful statement after a reset is that a reset
            // happened.
            //
            // Wording follows the house style set by the other seven modules
            // ("Stack reset successful." / "Queue reset successful."), named
            // per activity because Memory resets one activity at a time. The
            // Overview branch is worded differently on purpose: Overview is
            // skipped by ResetActivityProgress() below, so claiming its
            // progress was cleared would be false.
            currentFeedbackText = currentActivity == ActivityOverview
                ? "Memory Management reset successful. The memory table returned to its starting state."
                : $"{currentActivity} reset successful. The memory table and this activity's progress returned to their starting state.";

            // Godot resets ONLY the current activity's assessment counters,
            // leaving the other two intact — so resetting Debug does not wipe
            // a Trace score. Preserved.
            if (assessmentStates.TryGetValue(currentActivity, out AssessmentState resetState))
            {
                resetState.Reset();
            }

            if (currentActivity != ActivityOverview)
            {
                ResetActivityProgress(currentActivity);
            }

            ApplyActivityLayout();
            RefreshDashboard();

            LogEvent("Reset", currentActivity, "Success",
                "Memory activity reset: " + currentActivity);
        }

        // -----------------------------------------------------------------
        // Progress — Godot: mark_memory_activity_completed / etc.
        // -----------------------------------------------------------------

        /// <summary>
        /// Godot: show_quiz_result()'s Memory branch —
        ///
        ///     if current_module == "Memory":
        ///         mark_memory_activity_completed(
        ///             "Quiz", "Quiz score: " + str(quiz_score) + "/" + ...)
        ///
        /// "Quiz" is the FOURTEENTH entry in get_memory_activity_order() and
        /// the last step of the guided sequence, which is why the progress
        /// line reads "Next: Quiz" once Final Assessment is done. Without
        /// this hook the tracker could never reach 14 of 14 — every other
        /// entry is marked by its own activity, and nothing else can mark
        /// this one.
        ///
        /// Called by QuizController when a quiz finishes while Memory is the
        /// active module. Public for that reason only.
        /// </summary>
        public void MarkQuizCompleted(int score, int total)
        {
            MarkActivityCompleted("Quiz", "Quiz score: " + score + "/" + total);

            // Godot follows the mark with update_memory_status_label(), so
            // the Progress line reflects it immediately rather than at the
            // next activity change.
            RefreshDashboard();
        }

        private void MarkActivityCompleted(string activityName, string note = "")
        {
            if (!memoryProgress.ContainsKey(activityName)) return;

            memoryProgress[activityName] = true;

            if (note != "") memoryProgressNotes[activityName] = note;

            LogEvent("Memory Progress", activityName, "Completed", note);
        }

        private void ResetActivityProgress(string activityName)
        {
            if (!memoryProgress.ContainsKey(activityName)) return;

            memoryProgress[activityName] = false;
            memoryProgressNotes.Remove(activityName);
        }

        private int GetCompletedCount()
        {
            int completed = 0;

            foreach (KeyValuePair<string, bool> entry in memoryProgress)
            {
                if (entry.Value) completed++;
            }

            return completed;
        }

        /// Godot: func get_next_memory_activity() — see porting note 8, now
        /// amended.
        ///
        /// Source returns the entry AFTER the current one in the fixed order,
        /// and only falls back to "first incomplete" when the current
        /// activity is not in the list at all. Its comment defends this:
        /// "This keeps the guided sequence pedagogically correct."
        ///
        /// That holds only if the student works in order. Jump straight to
        /// Final Assessment as your first activity and source announces
        /// "Next: Quiz" at 1 of 14 — pointing at the last step of the module
        /// while twelve activities are untouched. Reported from device
        /// showing exactly that.
        ///
        /// **Corrected: always the FIRST INCOMPLETE entry, in order.**
        ///
        /// A first attempt kept source's next-in-order preference and merely
        /// skipped it when that entry was already done. That still failed the
        /// reported case: finishing Final Assessment first makes the
        /// following entry "Quiz", which IS incomplete, so it was still
        /// announced at 1 of 14 while twelve activities sat untouched. The
        /// check has to consider everything before it, not just the one
        /// entry after.
        ///
        /// First-incomplete does that, and collapses the two rules into one.
        /// For a student working in order the two are identical — everything
        /// before the current entry is done, so the first incomplete IS the
        /// next one. Source's guided sequence is preserved exactly where it
        /// applies, and out-of-order work is sent back to the earliest thing
        /// still owed rather than past it.
        private string GetNextActivity()
        {
            foreach (string activityName in ActivityOrder)
            {
                if (memoryProgress.TryGetValue(activityName, out bool done) && !done)
                {
                    return activityName;
                }
            }

            return "";
        }

        /// Godot: func get_memory_progress_text() -> String
        ///
        /// Two wording changes on top of source, both raised on review, both
        /// clarifications rather than behaviour changes — the counts, the
        /// order and the "of 14" denominator are untouched.
        ///
        /// 1. **The Quiz line is only shown when it is true.** "Quiz" is
        ///    the fourteenth entry of get_memory_activity_order() and the
        ///    last step of the guided sequence, but it is not one of the
        ///    thirteen dropdown activities — it is a button. Combined with
        ///    the GetNextActivity() correction above, it is now named only
        ///    once every activity is genuinely complete.
        ///
        /// 2. **Finishing everything says so.** Source's terminal string is
        ///    "All activities completed.", which does not read as the end of
        ///    anything in particular.
        ///
        /// EndOfModule and the Quiz branch of DescribeNext() are the only two
        /// places to change if source's exact strings are ever wanted back.
        private const string EndOfModule = "Memory Management module complete.";

        private static string DescribeNext(string next)
        {
            // "Quiz" is now only ever returned when every activity is done —
            // GetNextActivity() skips completed entries — so this line can
            // state that plainly instead of hedging.
            if (next == "Quiz")
            {
                return "All activities done — take the Quiz to finish the module.";
            }

            return "Next: " + next + ".";
        }

        private string GetProgressText()
        {
            int completed = GetCompletedCount();
            int total = memoryProgress.Count;
            string next = GetNextActivity();

            if (memoryProgress.TryGetValue(currentActivity, out bool currentDone))
            {
                if (currentDone)
                {
                    if (next == "")
                    {
                        return $"Progress: {completed} of {total} activities completed. " +
                               EndOfModule;
                    }

                    return $"Progress: {completed} of {total} activities completed. " +
                           $"{currentActivity} completed. {DescribeNext(next)}";
                }

                return $"Progress: {completed} of {total} activities completed. " +
                       $"Complete current: {currentActivity}.";
            }

            if (next == "")
            {
                return $"Progress: {completed} of {total} activities completed. " + EndOfModule;
            }

            return $"Progress: {completed} of {total} activities completed. {DescribeNext(next)}";
        }

        // -----------------------------------------------------------------
        // Dashboard — Godot: update_memory_status_label()
        // -----------------------------------------------------------------

        /// Godot's six labels map one-to-one onto Unity's six panels:
        /// module_title + module_description -> Summary, code_label ->
        /// Instruction, code_explanation -> Operation, index_explanation ->
        /// Status, operation_feedback -> Progress, with the activity's own
        /// concept sentence in Explanation. See porting note 6 for the two
        /// panels Godot hides in Overview.
        private void RefreshDashboard()
        {
            if (memoryModule != null)
            {
                memoryModule.SetOverviewStyle(currentActivity == ActivityOverview);
            }

            bool overview = currentActivity == ActivityOverview;

            // Summary is the MODULE-level sentence and does not change with
            // the activity — Godot: show_module_description() in
            // show_selected_module()'s Memory branch, which passes exactly
            // this string. The module/activity name that used to lead this
            // panel was dropped in the Phase 26c audit; the dropdown already
            // names the activity.
            DashboardController.Instance?.SetSummary(GetModuleSummary());

            // No "Instruction: " prefix — the panel heading already says it.
            DashboardController.Instance?.SetInstruction(GetActivityInstruction());

            // Overview has no per-activity details block, so it normally shows
            // the default. It CAN still have feedback though — Reset works in
            // Overview and now says so (see OnResetClicked) — so surface the
            // feedback line rather than forcing "" and losing the message.
            // Phase 28b: was `overview ? ""`, which swallowed it.
            string operation = overview
                ? currentFeedbackText.Trim()
                : GetModeDetailsBlock().Trim();

            DashboardController.Instance?.SetOperation(
                operation == "" ? "No operation selected yet." : operation);

            // Explanation is the ACTIVITY's own concept sentence, replaced by
            // an action's explanation once a handler sets one. This is the
            // panel that must track both the activity AND what was just done.
            //
            // Corrected in Phase 26c: the first draft had this and Summary
            // the wrong way round, so Explanation held a constant and nothing
            // on the dashboard changed as activities changed. Godot is
            // unambiguous about it — show_selected_module()'s Memory branch
            // reads:
            //
            //     show_module_description("This module shows how variables...")
            //     show_code_explanation(get_memory_activity_explanation())
            //
            // module_description (-> Summary) is the constant; code_explanation
            // (-> Explanation) is the per-activity one.
            //
            // Note this is also why get_memory_mode_details_block() blocks
            // mode_summary from its own output: the activity explanation is
            // already on screen in this panel, so repeating it in Operation
            // would be redundant. That filter only makes sense once the two
            // panels are the right way round.
            string explanation = currentExplanationText.Trim();

            if (explanation == "")
            {
                explanation = overview
                    ? "Select an activity: Variables, Data Types, Address Layout, Memory Access, " +
                      "Loop Memory, Arrays, Functions, Selection, Scope, Remove, Trace, Debug, " +
                      "or Assessment."
                    : GetActivityExplanation();
            }

            DashboardController.Instance?.SetExplanation(explanation);

            // Populated in EVERY activity including Overview — see the panel
            // contract note. Godot hides both labels in Overview because its
            // dashboard is a container that shrinks; Unity's six panels are
            // permanent, so hiding the text leaves two visibly empty boxes.
            DashboardController.Instance?.SetStatus(
                memoryModule != null ? memoryModule.GetStatusText() : "");

            DashboardController.Instance?.SetProgress(GetProgressText());

            RebuildDashboardLayout();
        }

        private void SetOperation(string text)
        {
            currentDetailsText = text;
            currentExplanationText = "";
            currentFeedbackText = "";
        }

        /// Godot: func get_memory_mode_details_block() -> String
        /// Ported in full even though Phase 26 only exercises its simple
        /// path, because the Variables-specific branches are what Phase 26c
        /// needs and porting it once avoids rewriting it there.
        private string GetModeDetailsBlock()
        {
            string detailsText = currentDetailsText.Trim();
            string feedbackText = currentFeedbackText.Trim();

            // Prediction text must never be filtered out by the rules below.
            if (currentActivity == ActivityVariables && detailsText.StartsWith("Next statement:"))
            {
                return detailsText;
            }

            if (detailsText.StartsWith("Prediction task:"))
            {
                return detailsText;
            }

            string modeSummary = GetActivityExplanation().Trim();
            string instructionText = GetActivityInstruction().Trim();

            List<string> detailLines = new List<string>();

            // Godot's blocked_texts list — anything already shown elsewhere
            // on the dashboard is suppressed here to avoid repeating it.
            HashSet<string> blocked = new HashSet<string>
            {
                "",
                modeSummary,
                instructionText,
                "Instruction: " + instructionText
            };

            const string VariableTableSentence =
                "The memory table shows whether the variable was newly allocated, updated, or inspected.";

            // Godot adds current_memory_explanation_text as this block's first
            // line, guarded against repeating the mode summary:
            //
            //     if not blocked_texts.has(explanation_text):
            //         if not mode_summary.contains(explanation_text):
            //             detail_lines.append(explanation_text)
            //
            // That is NOT ported, and the omission is the point. Godot has one
            // label for all of this, so the explanation has nowhere else to
            // appear. Unity gives it its own headed Explanation panel, so
            // including it here printed it twice — once under "Explanation",
            // again under "Operation". Reported from device on the Set Array
            // empty-input message, which is where both are set at once.
            //
            // The guard above is the tell: source only bothers suppressing the
            // duplicate when the text already matches the mode summary,
            // because that is the ONLY other place it could already be
            // showing. In Unity there is always another place, so the line is
            // always redundant here.
            //
            // Panel contract: Explanation owns the concept text, Operation
            // owns what just happened. explanationText is deliberately unused
            // in this method and read straight from RefreshDashboard().

            if (currentActivity == ActivityVariables)
            {
                if (feedbackText.StartsWith("Set Variable:") && detailsText != "")
                {
                    string cleanedFeedback = feedbackText.Replace("Set Variable:", "").Trim();
                    detailLines.Add("Set Variable: " + detailsText + " " + cleanedFeedback);
                }
                else if (feedbackText != "")
                {
                    if (!blocked.Contains(feedbackText) && feedbackText != VariableTableSentence)
                    {
                        detailLines.Add(feedbackText);
                    }
                }
                else if (detailsText != "")
                {
                    if (!blocked.Contains(detailsText) && detailsText != VariableTableSentence)
                    {
                        detailLines.Add(detailsText);
                    }
                }
            }
            else
            {
                if (detailsText != "" && !blocked.Contains(detailsText))
                {
                    detailLines.Add(detailsText);
                }

                if (feedbackText != "" && !blocked.Contains(feedbackText))
                {
                    detailLines.Add(feedbackText);
                }
            }

            return string.Join("\n", detailLines);
        }

        // Godot: func get_memory_module_summary() -> String
        private static string GetModuleSummary()
        {
            return "This module shows how variables, arrays, functions, selection statements, " +
                   "and program traces behave in memory.";
        }

        // Godot: func get_memory_activity_explanation() -> String
        private string GetActivityExplanation()
        {
            switch (currentActivity)
            {
                case ActivityOverview:
                    return "Choose a mode to explore memory behavior through visualization, " +
                           "tracing, debugging, or assessment.";
                case ActivityVariables:
                    return "Variables are stored with a name, type, value, address, and size. " +
                           "The memory table shows whether the variable was newly allocated, " +
                           "updated, or inspected.";
                case ActivityArrays:
                    return "Array elements are stored in consecutive simulated memory locations.";
                case ActivityFunctions:
                    return "Function calls create temporary memory for parameters, local " +
                           "variables, and return values.";
                case ActivitySelection:
                    return "Selection statements evaluate a condition and update memory based " +
                           "on the selected branch.";
                case ActivityScope:
                    return "Scope shows how local variables are created inside a block and " +
                           "removed when the block ends.";
                case ActivityTraceExercise:
                    return "Trace exercises ask you to predict the final value stored in memory.";
                case ActivityDebugExercise:
                    return "Debug exercises ask you to identify memory-related programming mistakes.";
                case ActivityFinalAssessment:
                    return "The final assessment checks your understanding of memory updates.";
                case ActivityDataTypes:
                    return "Data types define the kind of value stored and the simulated memory " +
                           "size: int/float = 4 bytes, char/bool = 1 byte, string = variable size.";
                case ActivityAddressLayout:
                    return "Address layout shows how the next memory address depends on the size " +
                           "of the previous data type.";
                case ActivityMemoryAccess:
                    return "Memory access shows how stored values are read from memory before " +
                           "they are used in an expression.";
                case ActivityLoopMemory:
                    return "Loop memory shows how repeated steps read array values, update total, " +
                           "and remove the loop counter when the loop ends.";
                case ActivityRemoveVariable:
                    return "Remove Variable shows how one stored item is removed while other " +
                           "memory records remain.";
                default:
                    return "Choose a memory activity mode.";
            }
        }

        // Godot: func get_memory_activity_instruction() -> String
        private string GetActivityInstruction()
        {
            switch (currentActivity)
            {
                case ActivityOverview:
                    return "Choose an activity from the dropdown.";
                case ActivityVariables:
                    return "Enter int age=20, age=25, or type age to inspect. Use Predict Step " +
                           "for guided prediction.";
                case ActivityArrays:
                    return "Enter int marks[3]=70,80,65 or marks[2]=85, then click Set Array. " +
                           "Type marks[2] and click Inspect.";
                case ActivityFunctions:
                    return "Click Function Call repeatedly to trace parameters, local variables, " +
                           "return values, and frame removal.";
                case ActivitySelection:
                    return "Click Run If-Else repeatedly to trace condition evaluation and branch " +
                           "memory updates.";
                case ActivityScope:
                    return "Click Scope Step to trace x allocation, y local scope, x update, and " +
                           "y removal.";
                case ActivityTraceExercise:
                    return "Click Start Trace to begin. Enter your answer, click Submit Answer, " +
                           "then click Next Question to continue.";
                case ActivityDebugExercise:
                    return "Click Start Debug to begin. Enter the error line, click Submit Answer, " +
                           "then click Next Question to continue.";
                case ActivityFinalAssessment:
                    // Note the mismatch with the button, preserved: the button
                    // reads "Start Assessment", not "Final Assessment", and
                    // the "type start" step does not match the Next Question
                    // button the other two assessments use. Flagged for
                    // Phase 26d rather than silently reworded here.
                    return "Click Final Assessment, answer each assessment item, then type start " +
                           "for the next item.";
                case ActivityDataTypes:
                    return "Click Show Types to display common data types, values, addresses, and " +
                           "memory sizes.";
                case ActivityAddressLayout:
                    return "Click Show Layout to display how addresses increase based on data type size.";
                case ActivityMemoryAccess:
                    return "Click Access Step repeatedly to allocate x and y, read them from " +
                           "memory, and store their sum.";
                case ActivityLoopMemory:
                    return "Click Loop Step one step at a time to create the array, read each " +
                           "value, add it to total, update the loop index, and remove the loop " +
                           "counter when the loop ends.";
                case ActivityRemoveVariable:
                    return "Click Remove Step repeatedly to create sample variables, remove them " +
                           "from memory, and observe which records remain.";
                default:
                    return "Choose a memory activity mode.";
            }
        }

        // -----------------------------------------------------------------
        // Shared plumbing (same shape as every other module controller)
        // -----------------------------------------------------------------

        private void SetBusy(bool busy)
        {
            isBusy = busy;

            ActivityLayout layout = CurrentLayout;

            if (insertButton != null) insertButton.interactable = !busy && layout.ShowInsert;
            if (deleteButton != null) deleteButton.interactable = !busy && layout.ShowDelete;
            if (searchButton != null) searchButton.interactable = !busy && layout.ShowSearch;
            if (traverseButton != null) traverseButton.interactable = !busy && layout.ShowTraverse;
            if (resetButton != null) resetButton.interactable = !busy && layout.ShowReset;
            if (valueInput != null) valueInput.interactable = !busy && layout.ShowInput;
        }

        /// Every button wired in the Inspector, regardless of whether the
        /// current activity shows it — porting note 2.
        private int CountAssignedButtons()
        {
            int count = 0;
            if (insertButton != null) count++;
            if (deleteButton != null) count++;
            if (searchButton != null) count++;
            if (traverseButton != null) count++;
            if (resetButton != null) count++;
            if (quizButton != null) count++;

            return count;
        }

        private void ApplyResponsiveButtonLayout()
        {
            if (buttonGrid == null) return;

            int columns = 2;

            if (ResponsiveManager.Instance != null)
            {
                columns = Mathf.Clamp(ResponsiveManager.Instance.GetButtonColumns(), 1, 5);
            }

            // Cells are sized for the module's FULL button complement, not for
            // the subset the current activity happens to show — porting note 2.
            //
            // The first draft sized them for the visible count instead, which
            // is wrong in a way that only becomes obvious on screen: in
            // Overview one button is visible, so the grid collapsed to a
            // single 1x1 cell and the Quiz button stretched across the whole
            // strip at several times its normal size. Sizing for the full
            // complement keeps every button identical in every activity, and
            // an activity showing fewer simply leaves cells empty — which is
            // what a control strip should do.
            int sizingCount = Mathf.Max(1, CountAssignedButtons());
            int rows = Mathf.CeilToInt(sizingCount / (float)columns);

            buttonGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            buttonGrid.constraintCount = columns;

            Canvas.ForceUpdateCanvases();

            RectTransform gridRect = buttonGrid.GetComponent<RectTransform>();

            if (gridRect != null && columns > 0 && rows > 0)
            {
                float availableWidth = gridRect.rect.width - buttonGrid.padding.left - buttonGrid.padding.right;
                float availableHeight = gridRect.rect.height - buttonGrid.padding.top - buttonGrid.padding.bottom;

                float cellWidth = (availableWidth - buttonGrid.spacing.x * (columns - 1)) / columns;
                float cellHeight = (availableHeight - buttonGrid.spacing.y * (rows - 1)) / rows;

                if (cellWidth > 0f && cellHeight > 0f)
                {
                    buttonGrid.cellSize = new Vector2(cellWidth, cellHeight);
                }
            }
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
