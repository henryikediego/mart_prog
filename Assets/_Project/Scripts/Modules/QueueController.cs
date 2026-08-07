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
    /// </summary>
    public class QueueController : MonoBehaviour
    {
        [Header("Module Reference")]
        [Tooltip("Auto-found via FindFirstObjectByType if left empty.")]
        [SerializeField] private QueueModule queueModule;

        [Header("UI References")]
        [SerializeField] private TMP_InputField valueInput;
        [SerializeField] private Button enqueueButton;
        [SerializeField] private Button dequeueButton;
        [SerializeField] private Button searchButton;
        [SerializeField] private Button traverseButton;
        [SerializeField] private Button resetButton;

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

            DashboardController.Instance?.SetSummary("Queue Module");
            DashboardController.Instance?.SetInstruction(
                "Choose Enqueue, Dequeue, Search, Traverse, or Reset to continue.");
            DashboardController.Instance?.SetOperation("No operation selected yet.");
            DashboardController.Instance?.SetExplanation(
                "A queue works like a line of people. New values are added at the rear, " +
                "and values are removed from the front.");
            RebuildDashboardLayout();

            RefreshStatusAndProgress();

            ApplyResponsiveButtonLayout();
        }

        private void WireButtons()
        {
            if (enqueueButton != null) enqueueButton.onClick.AddListener(OnEnqueueClicked);
            if (dequeueButton != null) dequeueButton.onClick.AddListener(OnDequeueClicked);
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
            if (searchButton != null) count++;
            if (traverseButton != null) count++;
            if (resetButton != null) count++;
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
                LogEvent("Queue Dequeue", "", "Failed", "Queue is empty");
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
