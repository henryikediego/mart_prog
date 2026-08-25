using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MartProg.Modules
{
    /// <summary>
    /// Phase 19: Queue interactivity.
    ///
    /// Wires the on-screen control strip (Enqueue / Dequeue / Search /
    /// Traverse / Reset buttons + a numeric input field) to QueueModule's
    /// operations, mirrors the resulting state onto DashboardController's
    /// six panels, and logs every action through ResearchLogger — matching
    /// the original Godot behaviour implemented by ar_learning_screen.gd's
    /// _handle_queue_insert_button and its siblings (_delete_button,
    /// _search_button, _traverse_button, _reset_button), plus their helper
    /// functions (handle_queue_enqueue, handle_queue_dequeue,
    /// handle_queue_search, handle_queue_traverse,
    /// set_queue_dashboard_message, update_queue_status_label).
    ///
    /// Built directly against StackController.cs's already-validated shape
    /// (deferred init, responsive button grid, dashboard rebuild-on-change,
    /// short dynamic text). Porting notes specific to Queue — confirmed by
    /// reading ar_learning_screen.gd's actual queue handlers directly, not
    /// assumed from Array/Stack by structural similarity:
    ///
    /// 1. Max capacity — 5, matching QUEUE_MAX_VALUES in
    ///    ar_learning_screen.gd (the same dual-cap pattern as Array/Stack:
    ///    the controller enforces 5, QueueModule.IsQueueFull() (cap 6) is a
    ///    redundant backstop only).
    ///
    /// 2. Enqueue has no isAnimating check in this controller and no
    ///    coroutine at all — matches _handle_queue_insert_button() exactly,
    ///    which never checks is_animating itself (only the module-level
    ///    enqueue_value() does, as a backstop). Enqueue never animates in
    ///    the source, so QueueModule.EnqueueValue() is a plain synchronous
    ///    method, not a coroutine — see QueueModule.cs note 1.
    ///
    /// 3. Real per-message differences from Stack, ported distinctly rather
    ///    than normalized to match Stack's phrasing:
    ///    - Dequeue's success message DOES include the removed value
    ///      ("Dequeued value 10 from the front of the queue.") — the
    ///      opposite of Stack's Pop, which deliberately omits it.
    ///    - Enqueue's and Reset's Explanation text both include a running
    ///      "Current size: X/5." — Stack's Push/Reset explanations never
    ///      mention a count at all.
    ///    - Search's found message always reports the raw index directly
    ///      ("found at index N") with no special-casing — unlike Stack's
    ///      search, which only shows an index when the match is at the very
    ///      top and otherwise says "N positions from the top."
    ///    - Dequeue's research-log action name is asymmetric the same way
    ///      Stack's Pop is: the controller-level empty-queue guard logs
    ///      "Queue Dequeue" on failure, but a successful dequeue is logged
    ///      from inside QueueModule-adjacent code as plain "Dequeue" — a
    ///      real asymmetry confirmed in source (the outer function's own
    ///      empty check logs "Queue Dequeue"; the inner handle_queue_dequeue()
    ///      logs "Dequeue" on its success path), ported as-is. Enqueue has
    ///      no such asymmetry — every Enqueue log uses "Queue Enqueue"
    ///      regardless of outcome.
    ///    - Search has no dedicated "the queue is empty" message, the same
    ///      omission Stack's search has — handle_queue_search() has no
    ///      empty-queue pre-check, so an empty-queue search just falls
    ///      through to the same "not found" message. Traverse, unlike
    ///      Search, DOES have a dedicated empty check in source, and keeps
    ///      it here.
    ///
    /// 4. Traverse formats front-to-rear, which is simply forward order
    ///    (index 0 to Count-1) — the opposite of Stack, which has to
    ///    reverse-iterate because its top is the last index.
    ///
    /// 5. Phase 20: implements IModuleController.EnterModule(), same as
    ///    ArrayListController/StackController — see Migration Spec §8f.
    ///    Lets ChangeModuleController.cs reset + refresh Queue on every
    ///    switch into it, not just the scene's first frame.
    ///
    /// 8. **Phase 28: Peek is wired here, and this is a DEVIATION from
    ///    source's reachability — deliberately.** Same situation as Stack
    ///    (see StackController porting note 9, which carries the full
    ///    reasoning). Godot defines `handle_queue_peek()` at
    ///    ar_learning_screen.gd:6433 — complete, with a `queue[0]` code
    ///    sample, explanation, empty-queue branch and logging under the name
    ///    "Peek" — and it has **no callers anywhere**.
    ///    `configure_buttons_for_current_module()`'s Queue branch wires only
    ///    Enqueue, Dequeue, Search, Traverse, Reset and Quiz.
    ///
    ///    Queue's version is deader still: `queue_module.gd`'s own
    ///    `peek_front()` is **commented out entirely**, so source's live
    ///    handler calls `highlight_front()` instead. Unity's
    ///    QueueModule.PeekValue()/HighlightPeek() were ported anyway and
    ///    marked "parity only" (see that class's note 7); this phase gives
    ///    HighlightPeek() its first caller.
    ///
    ///    Forced by the Phase 28 quiz audit: Queue Q8 ("What does peek do in
    ///    a queue?") tested an operation no student could perform. The
    ///    project owner chose to wire the operation rather than drop the
    ///    question.
    /// </summary>
    public class QueueController : MonoBehaviour, IModuleController
    {
        [Header("Module Reference")]
        [Tooltip("Auto-found via FindFirstObjectByType if left empty.")]
        [SerializeField] private QueueModule queueModule;

        [Header("UI References")]
        [SerializeField] private TMP_InputField valueInput;
        [SerializeField] private Button enqueueButton;
        [SerializeField] private Button dequeueButton;
        [Tooltip("Phase 28: NEW. Godot defines a complete handle_queue_peek() " +
                 "but never calls it, so Peek was unreachable in both builds " +
                 "while a quiz question tested it. Wired here rather than " +
                 "dropping the question — see porting note 8.")]
        [SerializeField] private Button peekButton;
        [SerializeField] private Button searchButton;
        [SerializeField] private Button traverseButton;
        [SerializeField] private Button resetButton;
        [Tooltip("The Quiz trigger button living in THIS module's own control strip, " +
                 "alongside Search/Traverse (not the admin row). Assign the same Button " +
                 "GameObject here as on QuizController.queueQuizButton — that script wires " +
                 "the click behavior; this field only makes sure this module's button-grid " +
                 "column/row/cell-size math (CountAssignedButtons() below) accounts for it.")]
        [SerializeField] private Button quizButton;

        [Header("Layout (optional)")]
        [Tooltip("The Grid Layout Group on this module's button grid. If assigned, its column count " +
                 "and cell size are set at Start() from ResponsiveManager.GetButtonColumns(), " +
                 "matching the same profile-driven layout the rest of the dashboard uses.")]
        [SerializeField] private GridLayoutGroup buttonGrid;

        [Tooltip("The dashboard's scrollable 'Content' RectTransform (Canvas > " +
                 "DashboardRoot > Scroll View > Viewport > Content). If assigned, this " +
                 "controller forces a layout rebuild every time it changes a panel's text.")]
        [SerializeField] private RectTransform dashboardContent;

        // Controller-enforced cap — matches ar_learning_screen.gd's
        // QUEUE_MAX_VALUES. QueueModule.IsQueueFull() (cap 6) is checked
        // too, purely as a defensive backstop; it should never be the one
        // that trips first. See porting note 1 above.
        private const int QueueMaxValues = 5;

        private bool isBusy;

        private void Start()
        {
            if (queueModule == null)
            {
                queueModule = FindFirstObjectByType<QueueModule>();
            }

            if (queueModule == null)
            {
                Debug.LogError("QueueController: no QueueModule found in the scene.");
            }

            if (ResearchLogger.Instance != null)
            {
                ResearchLogger.Instance.ModuleName = "Queue";
            }

            WireButtons();
            StartCoroutine(InitializeDeferred());
        }

        /// <summary>
        /// Same one-frame defer as StackController.InitializeDeferred() —
        /// see Migration Spec §5.
        /// </summary>
        private IEnumerator InitializeDeferred()
        {
            yield return null;

            EnterModule();

            ApplyResponsiveButtonLayout();
        }

        /// <summary>
        /// IModuleController.EnterModule() — see porting note 5 above and
        /// Migration Spec §8f. Resets Queue back to its starting values and
        /// refreshes every dashboard panel, matching Godot's
        /// reset_dashboard_state_for_module_start("Queue") +
        /// update_queue_dashboard() pair.
        /// </summary>
        public void EnterModule()
        {
            // Phase 29 (F1) backstop: if this controller's GameObject was
            // disabled mid-animation but the module's was not, the module's
            // own OnDisable() never fired and isAnimating is still true —
            // which would make the Reset*() call below silently no-op and
            // leave the module permanently unusable. Clear it first.
            if (queueModule != null) queueModule.ForceStopAnimation();

            SetBusy(false);

            if (queueModule != null) queueModule.ResetQueue();
            if (valueInput != null) valueInput.text = "";

            // Phase 26b dashboard audit — see DashboardController.cs.
            DashboardController.Instance?.SetSummary(
                "A queue stores values using the First-In, First-Out principle. " +
                "The first value added is the first value removed.");
            DashboardController.Instance?.SetInstruction(
                "Choose Enqueue, Dequeue, Peek, Search, Traverse, Reset, or Quiz to continue.");
            DashboardController.Instance?.SetOperation("No operation selected yet.");
            DashboardController.Instance?.SetExplanation(
                "A queue works like a line of people. New values are added at the rear, " +
                "and values are removed from the front.");
            RebuildDashboardLayout();

            RefreshStatusAndProgress();
        
            // Phase 27 regression fix — the responsive button grid is sized
            // HERE as well as in InitializeDeferred().
            //
            // Phase 27 made ChangeModuleController disable every module at
            // Start() so the app can boot to the selector. Unity's script
            // execution order between two components' Start() methods is
            // undefined, so this controller's Start() sometimes runs FIRST,
            // launching InitializeDeferred() — and then the GameObject is
            // disabled a moment later, which halts the coroutine before its
            // `yield return null` resumes. ApplyResponsiveButtonLayout()
            // never runs. When the student later picks this module the object
            // is re-enabled, but Start() does not run a second time, so the
            // coroutine never restarts and the grid keeps its prefab cell
            // size — narrow buttons, reported on Array/List while Stack and
            // Memory looked correct purely because their Start() happened to
            // run after they were already disabled.
            //
            // EnterModule() is called on every entry by ChangeModuleController,
            // so applying it here is reliable regardless of that ordering.
            // The call is idempotent, so the deferred one doing it too is
            // harmless.
            ApplyResponsiveButtonLayout();
        }

        private void WireButtons()
        {

            // Phase 29 (F11): nothing in the project was wired to onSubmit, so

            // pressing Enter/Done on the on-screen keyboard dismissed it and

            // did nothing — the student had to notice the value was still in

            // the field and find the button. onSubmit (not onEndEdit) is the

            // right event: onEndEdit also fires on focus loss, which would

            // trigger the action when the student merely tapped elsewhere.

            if (valueInput != null) valueInput.onSubmit.AddListener(_ => OnEnqueueClicked());

            if (enqueueButton != null) enqueueButton.onClick.AddListener(OnEnqueueClicked);
            if (dequeueButton != null) dequeueButton.onClick.AddListener(OnDequeueClicked);
            if (peekButton != null) peekButton.onClick.AddListener(OnPeekClicked);
            if (searchButton != null) searchButton.onClick.AddListener(OnSearchClicked);
            if (traverseButton != null) traverseButton.onClick.AddListener(OnTraverseClicked);
            if (resetButton != null) resetButton.onClick.AddListener(OnResetClicked);
        }

        /// <summary>
        /// Same responsive grid pattern as StackController/ArrayListController
        /// — see Migration Spec §5.
        /// </summary>
        private void ApplyResponsiveButtonLayout()
        {
            if (buttonGrid == null) return;

            int columns = 2; // sensible mobile-portrait default if ResponsiveManager isn't available at all

            if (ResponsiveManager.Instance != null)
            {
                columns = Mathf.Clamp(ResponsiveManager.Instance.GetButtonColumns(), 1, 5);
            }

            int buttonCount = CountAssignedButtons();
            int rows = columns > 0 ? Mathf.CeilToInt(buttonCount / (float)columns) : 1;

            Debug.Log($"QueueController: applying a {columns}-column / {rows}-row button layout " +
                      $"(profile: {ResponsiveManager.Instance?.GetLayoutProfileName() ?? "unavailable"}).");

            buttonGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            buttonGrid.constraintCount = columns;

            Canvas.ForceUpdateCanvases();

            RectTransform gridRect = buttonGrid.GetComponent<RectTransform>();

            if (gridRect != null)
            {
                float availableWidth = gridRect.rect.width - buttonGrid.padding.left - buttonGrid.padding.right;
                float availableHeight = gridRect.rect.height - buttonGrid.padding.top - buttonGrid.padding.bottom;

                float cellWidth = (availableWidth - buttonGrid.spacing.x * (columns - 1)) / columns;
                float cellHeight = rows > 0
                    ? (availableHeight - buttonGrid.spacing.y * (rows - 1)) / rows
                    : buttonGrid.cellSize.y;

                if (cellWidth > 0f && cellHeight > 0f)
                {
                    buttonGrid.cellSize = new Vector2(cellWidth, cellHeight);
                }
            }
        }

        private int CountAssignedButtons()
        {
            int count = 0;
            if (enqueueButton != null) count++;
            if (dequeueButton != null) count++;
            if (peekButton != null) count++;
            if (searchButton != null) count++;
            if (traverseButton != null) count++;
            if (resetButton != null) count++;
            if (quizButton != null) count++;
            return count;
        }

        // ---------------------------------------------------------------
        // Enqueue — synchronous, no animation (see class comment note 2)
        // ---------------------------------------------------------------

        public void OnEnqueueClicked()
        {
            if (isBusy || queueModule == null) return;
            HandleEnqueue();
        }

        private void HandleEnqueue()
        {
            string typedValue = valueInput != null ? valueInput.text.Trim() : "";

            if (string.IsNullOrEmpty(typedValue))
            {
                SetOperationExplanation(
                    "Enqueue failed. No value was entered.",
                    "Type a numeric value in the input box before clicking Enqueue. The value will be added to the rear of the queue.");
                LogEvent("Queue Enqueue", "", "Failed", "No value entered for queue enqueue");
                return;
            }

            if (!int.TryParse(typedValue, out int newValue))
            {
                SetOperationExplanation(
                    $"Enqueue failed. {typedValue} is not a valid number.",
                    "This Queue activity currently accepts numeric values only. Enter a number such as 40 before clicking Enqueue.");
                LogEvent("Queue Enqueue", typedValue, "Failed", "Invalid queue enqueue input");
                return;
            }

            if (queueModule.values.Count >= QueueMaxValues)
            {
                SetOperationExplanation(
                    "Enqueue failed. The queue is full.",
                    $"This activity allows a maximum of {QueueMaxValues} values.");
                LogEvent("Queue Enqueue", typedValue, "Failed", "Queue maximum value count reached");
                return;
            }

            bool success = queueModule.EnqueueValue(newValue);

            if (!success)
            {
                SetOperationExplanation(
                    $"Enqueue failed. {queueModule.GetLastActionMessage()}",
                    "The value was not added.");
                LogEvent("Queue Enqueue", typedValue, "Failed", queueModule.GetLastActionMessage());
                return;
            }

            SetOperationExplanation(
                $"Enqueued value {newValue} at the rear of the queue.",
                $"Enqueue adds a new value to the rear of the queue. Current size: {queueModule.values.Count}/{QueueMaxValues}.");
            LogEvent("Queue Enqueue", typedValue, "Success", queueModule.GetLastActionMessage());

            if (valueInput != null) valueInput.text = "";

            RefreshStatusAndProgress();
        }

        // ---------------------------------------------------------------
        // Dequeue — animates (see class comment note 2)
        // ---------------------------------------------------------------

        public void OnDequeueClicked()
        {
            if (isBusy || queueModule == null) return;
            StartCoroutine(HandleDequeue());
        }

        private IEnumerator HandleDequeue()
        {
            if (queueModule.values.Count == 0)
            {
                SetOperationExplanation(
                    "Dequeue failed. The queue is empty.",
                    "Dequeue cannot remove a value because there are no values currently stored in the queue.");
                LogEvent("Queue Dequeue", "", "Failed", "Queue is empty");
                yield break;
            }

            SetBusy(true);

            int? removed = null;
            yield return StartCoroutine(queueModule.DequeueValue(result => removed = result));

            SetBusy(false);

            if (removed.HasValue)
            {
                // Deliberately DOES include the removed value — see
                // porting note 3 above (opposite of Stack's Pop).
                SetOperationExplanation(
                    $"Dequeued value {removed.Value} from the front of the queue.",
                    $"Dequeue removes the front value from the queue. A queue follows First-In, First-Out. Current size: {queueModule.values.Count}/{QueueMaxValues}.");
                // Action name "Dequeue" (not "Queue Dequeue") on success —
                // see porting note 3 above.
                LogEvent("Dequeue", removed.Value.ToString(), "Success", "Front value removed from queue");
            }
            else
            {
                SetOperationExplanation(
                    "Dequeue failed. The queue is empty.",
                    "Dequeue cannot remove a value because there are no values currently stored in the queue.");
                // Phase 27b: port of handle_queue_dequeue()'s own failure
                // path, not the button handler's — delegated handler's name
                // and details. Was "Queue Dequeue" / "Queue is empty".
                LogEvent("Dequeue", "", "Failed", "Queue was empty");
            }

            RefreshStatusAndProgress();
        }

        // ---------------------------------------------------------------
        // Peek — Phase 28. See porting note 8.
        // ---------------------------------------------------------------

        public void OnPeekClicked()
        {
            if (isBusy || queueModule == null) return;
            StartCoroutine(HandlePeek());
        }

        /// <summary>
        /// Godot: handle_queue_peek() (ar_learning_screen.gd:6433), ported
        /// line for line. Complete in source and never called — see porting
        /// note 8. Phase 28 wires it.
        ///
        /// Event name is a bare "Peek" — the same name Stack's Peek uses,
        /// because source logs both from their delegated handlers with no
        /// module prefix. That is source's own inconsistency (contrast
        /// "Queue Dequeue" vs "Dequeue" above) and is preserved. Analysis of
        /// the research CSV therefore needs the Module column, not the Action
        /// column, to tell a Stack peek from a Queue peek.
        /// </summary>
        private IEnumerator HandlePeek()
        {
            SetBusy(true);

            int? frontValue = null;
            yield return StartCoroutine(queueModule.HighlightPeek(result => frontValue = result));

            SetBusy(false);

            if (frontValue.HasValue)
            {
                // Explanation text is source's verbatim; the Operation line
                // drops source's "Peek: " prefix per the Phase 26b dashboard
                // contract and follows the house phrasing used by Dequeue.
                SetOperationExplanation(
                    $"Peeked the front of the queue: {frontValue.Value}. Peek does not remove it.",
                    "queue[0] reads the first value in the Python list. It views the front value without removing it.");
                LogEvent("Peek", frontValue.Value.ToString(), "Success", "Viewed front queue value");
            }
            else
            {
                SetOperationExplanation(
                    "Peek failed. The queue is empty.",
                    "queue[0] cannot be used when the queue is empty because there is no front value.");
                LogEvent("Peek", "", "Failed", "Queue was empty");
            }

            RefreshStatusAndProgress();
        }

        // ---------------------------------------------------------------
        // Search
        // ---------------------------------------------------------------

        public void OnSearchClicked()
        {
            if (isBusy || queueModule == null) return;
            StartCoroutine(HandleSearch());
        }

        private IEnumerator HandleSearch()
        {
            if (queueModule.isAnimating)
            {
                SetOperationExplanation(
                    "Please wait.",
                    "An animation is still running.");
                yield break;
            }

            string typedValue = valueInput != null ? valueInput.text.Trim() : "";

            if (string.IsNullOrEmpty(typedValue))
            {
                SetOperationExplanation(
                    "Search failed. No value was entered.",
                    "Type the value you want to find before clicking Search. For example, type 20 and then click Search.");
                LogEvent("Queue Search", "", "Failed", "No value entered for queue search");
                yield break;
            }

            if (!int.TryParse(typedValue, out int target))
            {
                SetOperationExplanation(
                    $"Search failed. {typedValue} is not a valid number.",
                    "This Queue activity currently stores numeric values, so the search input must be a number such as 10, 20, or 30.");
                LogEvent("Queue Search", typedValue, "Failed", "Invalid queue search input");
                yield break;
            }

            // No dedicated "the queue is empty" branch here — see porting
            // note 3 above. An empty queue falls straight through to
            // QueueModule's own currentCells.Count == 0 guard and reports
            // as "not found", exactly matching handle_queue_search().

            SetBusy(true);

            int foundIndex = -1;
            yield return StartCoroutine(queueModule.SearchValue(target, result => foundIndex = result));

            SetBusy(false);

            if (foundIndex >= 0)
            {
                SetOperationExplanation(
                    $"Value {target} was found at index {foundIndex}.",
                    "Search compares the entered value with the stored queue values from front to rear and reports the index where the value is found.");
                LogEvent("Queue Search", typedValue, "Success", $"Value found at queue index {foundIndex}");
            }
            else
            {
                SetOperationExplanation(
                    $"Value {target} was not found in the queue.",
                    "Search checks the stored queue values one by one from front to rear. If no stored value matched the input, the search result is not found.");
                LogEvent("Queue Search", typedValue, "Failed", "Value not found in queue");
            }

            RefreshStatusAndProgress();
        }

        // ---------------------------------------------------------------
        // Traverse
        // ---------------------------------------------------------------

        public void OnTraverseClicked()
        {
            if (isBusy || queueModule == null) return;
            StartCoroutine(HandleTraverse());
        }

        private IEnumerator HandleTraverse()
        {
            if (queueModule.isAnimating)
            {
                SetOperationExplanation(
                    "Please wait.",
                    "An animation is still running.");
                yield break;
            }

            if (queueModule.values.Count == 0)
            {
                SetOperationExplanation(
                    "Traverse failed. The queue is empty.",
                    "Traversal cannot be completed because there are no values currently stored in the queue.");
                LogEvent("Queue Traverse", "", "Failed", "Queue is empty");
                yield break;
            }

            SetBusy(true);

            yield return StartCoroutine(queueModule.TraverseQueue());

            SetBusy(false);

            string traverseText = FormatValuesFrontToRear(queueModule.values);

            SetOperationExplanation(
                $"Traversed values: {traverseText}",
                "Traverse visits the queue values from front to rear without removing them. The queue order remains unchanged.");
            LogEvent("Queue Traverse", traverseText, "Success", "Queue values traversed from front to rear");

            RefreshStatusAndProgress();
        }

        // ---------------------------------------------------------------
        // Reset
        // ---------------------------------------------------------------

        public void OnResetClicked()
        {
            if (isBusy || queueModule == null) return;

            queueModule.ResetQueue();

            if (valueInput != null) valueInput.text = "";

            // Explanation DOES include a running count here — see porting
            // note 3 above (Stack's Reset explanation never mentions one).
            SetOperationExplanation(
                "Queue reset successful.",
                $"The queue returns its default values. Current size: {queueModule.values.Count}/{QueueMaxValues}.");
            LogEvent("Reset", "", "Success", "Queue reset to default values");

            RefreshStatusAndProgress();
        }

        // ---------------------------------------------------------------
        // Shared helpers
        // ---------------------------------------------------------------

        private void SetBusy(bool busy)
        {
            isBusy = busy;

            if (enqueueButton != null) enqueueButton.interactable = !busy;
            if (dequeueButton != null) dequeueButton.interactable = !busy;
            if (searchButton != null) searchButton.interactable = !busy;
            if (traverseButton != null) traverseButton.interactable = !busy;
            if (resetButton != null) resetButton.interactable = !busy;
        }

        private void SetOperationExplanation(string operation, string explanation)
        {
            DashboardController.Instance?.SetOperation(operation);
            DashboardController.Instance?.SetExplanation(explanation);
            RebuildDashboardLayout();
        }

        /// <summary>
        /// Direct port of update_queue_status_label(). Note the source's
        /// own asymmetry, preserved as-is: the single-value case ends with
        /// a period, the multi-value case does not — same pattern as
        /// Stack's Top/Bottom status.
        /// </summary>
        private void RefreshStatusAndProgress()
        {
            if (queueModule == null) return;

            List<int> values = queueModule.values;

            if (values.Count == 0)
            {
                DashboardController.Instance?.SetStatus("The queue is empty.");
            }
            else if (values.Count == 1)
            {
                DashboardController.Instance?.SetStatus($"Front and Rear are both {values[0]}.");
            }
            else
            {
                int frontValue = values[0];
                int rearValue = values[values.Count - 1];
                DashboardController.Instance?.SetStatus($"Front = {frontValue} | Rear = {rearValue}");
            }

            DashboardController.Instance?.SetProgress(
                $"{values.Count}/{QueueMaxValues} values stored.");

            RebuildDashboardLayout();
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

        /// <summary>
        /// Traverse reports front-to-rear, which is simply forward order
        /// through `values` (index 0 is the front) — see class comment
        /// note 4. Contrast StackController.FormatValuesTopToBottom, which
        /// has to iterate backward.
        /// </summary>
        private static string FormatValuesFrontToRear(List<int> values)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < values.Count; i++)
            {
                if (sb.Length > 0) sb.Append(" -> ");
                sb.Append(values[i]);
            }
            return sb.ToString();
        }
    }
}
