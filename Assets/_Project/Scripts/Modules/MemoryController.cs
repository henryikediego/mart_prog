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

        /// Built in this phase. Everything else selects cleanly and reports
        /// that it is not available yet.
        private static readonly HashSet<string> BuiltActivities = new HashSet<string>
        {
            ActivityOverview,
            ActivityDataTypes,
            ActivityAddressLayout
        };

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

        /// Godot: _handle_memory_insert_button(). Its if/elif chain routes by
        /// activity, with handle_memory_set_variable() as the catch-all.
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

            ReportNotBuilt("Set/Inspect");
        }

        private void OnDeleteClicked()
        {
            if (isBusy) return;
            ReportNotBuilt("Predict Step");
        }

        private void OnSearchClicked()
        {
            if (isBusy) return;
            ReportNotBuilt("Set Array");
        }

        private void OnTraverseClicked()
        {
            if (isBusy) return;
            ReportNotBuilt(CurrentLayout.TraverseLabel);
        }

        /// Phase-26 scope gate, matching how Phase 25 reported Else-if and
        /// Nested If before they existed. Removed activity by activity as
        /// 26b-26d land.
        private void ReportNotBuilt(string actionName)
        {
            string action = actionName == "" ? "This action" : actionName;

            SetOperation(
                action + " is not available yet. " + currentActivity +
                " is built in a later phase of the Memory module.");

            RefreshDashboard();
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
            currentFeedbackText = "";

            // Source also resets the Trace / Debug / Final Assessment
            // counters here, per activity. Those state machines arrive in
            // Phase 26d; this is where their reset will hook in.

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

        /// Godot: func get_next_memory_activity() — porting note 8.
        private string GetNextActivity()
        {
            int currentIndex = System.Array.IndexOf(ActivityOrder, currentActivity);

            if (currentIndex != -1 && currentIndex + 1 < ActivityOrder.Length)
            {
                return ActivityOrder[currentIndex + 1];
            }

            foreach (string activityName in ActivityOrder)
            {
                if (memoryProgress.TryGetValue(activityName, out bool done) && !done)
                {
                    return activityName;
                }
            }

            return "";
        }

        // Godot: func get_memory_progress_text() -> String
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
                               "All activities completed.";
                    }

                    return $"Progress: {completed} of {total} activities completed. " +
                           $"{currentActivity} completed. Next: {next}.";
                }

                return $"Progress: {completed} of {total} activities completed. " +
                       $"Complete current: {currentActivity}.";
            }

            if (next == "")
            {
                return $"Progress: {completed} of {total} activities completed. " +
                       "All activities completed.";
            }

            return $"Progress: {completed} of {total} activities completed. Next: {next}.";
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

            string operation = overview ? "" : GetModeDetailsBlock().Trim();

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
            string explanationText = currentExplanationText.Trim();
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

            if (!blocked.Contains(explanationText) && !modeSummary.Contains(explanationText))
            {
                detailLines.Add(explanationText);
            }

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
