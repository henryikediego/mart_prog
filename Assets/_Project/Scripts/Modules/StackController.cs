using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MartProg.Modules
{
    /// <summary>
    /// Phase 18: Stack interactivity.
    ///
    /// Wires the on-screen control strip (Push / Pop / Search / Traverse / Reset
    /// buttons + a numeric input field) to StackModule's coroutine-based
    /// operations, mirrors the resulting state onto DashboardController's six
    /// panels, and logs every action through ResearchLogger — matching the
    /// original Godot behaviour implemented by ar_learning_screen.gd's
    /// _handle_stack_insert_button and its siblings (_delete_button,
    /// _search_button, _traverse_button, _reset_button), plus their helper
    /// functions (handle_stack_push, handle_stack_search, handle_stack_traverse,
    /// handle_stack_pop, set_stack_dashboard_message, update_stack_status_label).
    ///
    /// Built directly against ArrayListController.cs's already-validated shape
    /// (deferred init, responsive button grid, dashboard rebuild-on-change,
    /// short dynamic text) rather than rediscovering those lessons — see
    /// Migration Spec §5. Porting notes specific to Stack:
    ///
    /// 1. Max capacity — 5, matching STACK_MAX_VALUES in ar_learning_screen.gd,
    ///    the same dual-cap pattern as Array/List (controller enforces 5,
    ///    StackModule.IsStackFull() (cap 6) is a redundant backstop only).
    ///
    /// 2. State tracking: reads StackModule.values directly, same reasoning
    ///    as ArrayListController — no shadow list needed in Unity.
    ///
    /// 3. Two source quirks preserved deliberately (not fixed):
    ///    - Pop's success message does NOT include the popped value
    ///      ("Popped the top value from the stack.") even though Array's
    ///      Delete message does include it — this exactly matches
    ///      _handle_stack_delete_button()'s actual text in the Godot source.
    ///    - Pop's research-log action name differs between its failure path
    ///      ("Stack Pop", logged by the controller-level empty check) and its
    ///      success path ("Pop", logged inside handle_stack_pop()) — a real
    ///      asymmetry in the source, ported as-is rather than normalized.
    ///    - Search has no dedicated "stack is empty" message the way Array's
    ///      search does; an empty-stack search falls through to the same
    ///      "not found" message an unsuccessful non-empty search would show.
    ///      This matches handle_stack_search() exactly, which has no empty-
    ///      stack pre-check unlike its Array/List counterpart.
    ///
    /// 4. Same InitializeDeferred() pattern as ArrayListController from the
    ///    start — both the responsive button grid and every
    ///    DashboardController call are deferred by one frame together, per
    ///    the Phase 17 lesson (Migration Spec §5), rather than needing a
    ///    second bugfix pass later.
    ///
    /// 5. Phase 20: implements IModuleController.EnterModule(), same as
    ///    ArrayListController — see its porting note 7 and Migration Spec
    ///    §8f. Lets ChangeModuleController.cs reset + refresh Stack on every
    ///    switch into it, not just the scene's first frame.
    ///
    /// 9. **Phase 28: Peek is wired here, and this is a DEVIATION from
    ///    source's reachability — deliberately.** Godot defines
    ///    `handle_stack_peek()` at ar_learning_screen.gd:6068. It is
    ///    complete: highlight animation, `stack[-1]` code sample, explanation
    ///    text, dashboard feedback, an empty-stack branch, and research
    ///    logging under the name "Peek". It also has **no callers anywhere in
    ///    the 11,925-line script**, and
    ///    `configure_buttons_for_current_module()`'s Stack branch wires only
    ///    Push, Pop, Search, Traverse, Reset and Quiz. So Peek was dead in
    ///    Godot and, ported faithfully, stayed dead in Unity —
    ///    StackModule.PeekValue() and HighlightPeek() were both carried over
    ///    and marked "ported for parity only".
    ///
    ///    The Phase 28 quiz audit is what forced the decision: **two of the
    ///    Stack bank's ten questions key on Peek** ("What does peek do in a
    ///    stack?" and "Which operation should be used to check the top value
    ///    without deleting it?"). Whoever wrote that bank assumed Peek was
    ///    reachable. The choice was to delete the questions or to wire the
    ///    operation, and the project owner chose to wire it: Peek is the
    ///    third canonical stack operation, taught beside push and pop
    ///    everywhere, so its absence was a gap in what the app teaches rather
    ///    than a scoping decision anyone had made on purpose.
    ///
    ///    Precedent: Phase 26e revived `handle_memory_step()` on the same
    ///    reasoning — a complete exercise whose only defect was that nothing
    ///    called it. This closes the last of the three dead handlers found in
    ///    the Phase 27b log audit (the other two, `handle_function_call()`
    ///    and `handle_function_set_inputs()`, stay dead because live buttons
    ///    already cover what they do).
    ///
    ///    Consequences to know about: the button grid goes from 6 to 7, and
    ///    the "Peek" research-log event name — Godot-only and never actually
    ///    emitted by either build until now — becomes live in Unity. A Godot
    ///    export will still contain no Peek rows.
    /// </summary>
    public class StackController : MonoBehaviour, IModuleController
    {
        [Header("Module Reference")]
        [Tooltip("Auto-found via FindFirstObjectByType if left empty.")]
        [SerializeField] private StackModule stackModule;

        [Header("UI References")]
        [SerializeField] private TMP_InputField valueInput;
        [SerializeField] private Button pushButton;
        [SerializeField] private Button popButton;
        [Tooltip("Phase 28: NEW. Godot defines a complete handle_stack_peek() " +
                 "but never calls it, so Peek was unreachable in both builds " +
                 "while two quiz questions tested it. Wired here rather than " +
                 "dropping the questions — see porting note 9.")]
        [SerializeField] private Button peekButton;
        [SerializeField] private Button searchButton;
        [SerializeField] private Button traverseButton;
        [SerializeField] private Button resetButton;
        [Tooltip("The Quiz trigger button living in THIS module's own control strip, " +
                 "alongside Search/Traverse (not the admin row). Assign the same Button " +
                 "GameObject here as on QuizController.stackQuizButton — that script wires " +
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
        // STACK_MAX_VALUES. StackModule.IsStackFull() (cap 6) is checked too,
        // purely as a defensive backstop; it should never be the one that
        // trips first. See porting note 1 above.
        private const int StackMaxValues = 5;

        private bool isBusy;

        private void Start()
        {
            if (stackModule == null)
            {
                stackModule = FindFirstObjectByType<StackModule>();
            }

            if (stackModule == null)
            {
                Debug.LogError("StackController: no StackModule found in the scene.");
            }

            if (ResearchLogger.Instance != null)
            {
                ResearchLogger.Instance.ModuleName = "Stack";
            }

            WireButtons();
            StartCoroutine(InitializeDeferred());
        }

        /// <summary>
        /// Same one-frame defer as ArrayListController.InitializeDeferred() —
        /// see Migration Spec §5 and porting note 4 above. Every
        /// DashboardController call and the ResponsiveManager-driven button
        /// layout are deferred together from the start, rather than being
        /// split (and needing a second fix later) the way Array/List's did.
        /// </summary>
        private IEnumerator InitializeDeferred()
        {
            yield return null;

            EnterModule();

            ApplyResponsiveButtonLayout();
        }

        /// <summary>
        /// IModuleController.EnterModule() — see porting note 5 above and
        /// Migration Spec §8f. Resets Stack back to its starting values and
        /// refreshes every dashboard panel, matching Godot's
        /// reset_dashboard_state_for_module_start("Stack") +
        /// update_stack_dashboard() pair.
        /// </summary>
        public void EnterModule()
        {
            SetBusy(false);

            if (stackModule != null) stackModule.ResetStack();
            if (valueInput != null) valueInput.text = "";

            // Phase 26b dashboard audit — see DashboardController.cs.
            DashboardController.Instance?.SetSummary(
                "A stack stores values using the Last-In, First-Out principle. " +
                "The last value pushed is the first value popped.");
            DashboardController.Instance?.SetInstruction(
                "Choose Push, Pop, Search, Traverse, Reset, or Quiz to continue.");
            DashboardController.Instance?.SetOperation("No operation selected yet.");
            DashboardController.Instance?.SetExplanation(
                "A stack works like a pile of items. New values are added to the top, " +
                "and removal also happens from the top.");
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
            if (pushButton != null) pushButton.onClick.AddListener(OnPushClicked);
            if (popButton != null) popButton.onClick.AddListener(OnPopClicked);
            if (peekButton != null) peekButton.onClick.AddListener(OnPeekClicked);
            if (searchButton != null) searchButton.onClick.AddListener(OnSearchClicked);
            if (traverseButton != null) traverseButton.onClick.AddListener(OnTraverseClicked);
            if (resetButton != null) resetButton.onClick.AddListener(OnResetClicked);
        }

        /// <summary>
        /// Same responsive grid pattern as ArrayListController — see
        /// Migration Spec §5. Computes both cell width and height from the
        /// grid's own RectTransform so it can never overflow into whatever
        /// sits below it, matching the Phase 16 fix from day one instead of
        /// rediscovering it.
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

            Debug.Log($"StackController: applying a {columns}-column / {rows}-row button layout " +
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
            if (pushButton != null) count++;
            if (popButton != null) count++;
            if (peekButton != null) count++;
            if (searchButton != null) count++;
            if (traverseButton != null) count++;
            if (resetButton != null) count++;
            if (quizButton != null) count++;
            return count;
        }

        // ---------------------------------------------------------------
        // Push
        // ---------------------------------------------------------------

        public void OnPushClicked()
        {
            if (isBusy || stackModule == null) return;
            StartCoroutine(HandlePush());
        }

        private IEnumerator HandlePush()
        {
            if (stackModule.isAnimating)
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
                    "Push failed. No value was entered.",
                    "Type a value, then click Push.");
                LogEvent("Stack Push", "", "Failed", "No value entered for stack push");
                yield break;
            }

            if (!int.TryParse(typedValue, out int newValue))
            {
                SetOperationExplanation(
                    $"Push failed. {typedValue} is not a valid number.",
                    "Only numbers are accepted.");
                LogEvent("Stack Push", typedValue, "Failed", "Invalid stack push input");
                yield break;
            }

            if (stackModule.values.Count >= StackMaxValues || stackModule.IsStackFull())
            {
                SetOperationExplanation(
                    "Push failed. The stack is full.",
                    $"The stack can hold at most {StackMaxValues} values. Pop one first.");
                LogEvent("Stack Push", typedValue, "Failed", "Stack maximum value count reached");
                yield break;
            }

            SetBusy(true);

            bool success = false;
            yield return StartCoroutine(stackModule.PushValue(newValue, result => success = result));

            SetBusy(false);

            if (!success)
            {
                SetOperationExplanation(
                    $"Push failed. {stackModule.GetLastActionMessage()}",
                    "The value was not pushed.");
                LogEvent("Stack Push", typedValue, "Failed", stackModule.GetLastActionMessage());
                yield break;
            }

            SetOperationExplanation(
                $"Pushed value {newValue} onto the stack.",
                "push() adds a new value to the top of the stack.");
            LogEvent("Stack Push", typedValue, "Success", stackModule.GetLastActionMessage());

            if (valueInput != null) valueInput.text = "";

            RefreshStatusAndProgress();
        }

        // ---------------------------------------------------------------
        // Pop
        // ---------------------------------------------------------------

        public void OnPopClicked()
        {
            if (isBusy || stackModule == null) return;
            StartCoroutine(HandlePop());
        }

        private IEnumerator HandlePop()
        {
            if (stackModule.values.Count == 0)
            {
                SetOperationExplanation(
                    "Pop failed. The stack is empty.",
                    "There is nothing to pop.");
                LogEvent("Stack Pop", "", "Failed", "Stack is empty");
                yield break;
            }

            SetBusy(true);

            int? removed = null;
            yield return StartCoroutine(stackModule.PopValue(result => removed = result));

            SetBusy(false);

            if (removed.HasValue)
            {
                // Deliberately does not include the popped value — see
                // porting note 3 above (matches the Godot source exactly).
                SetOperationExplanation(
                    "Popped the top value from the stack.",
                    "pop() removes the top value from the stack.");
                // Action name "Pop" (not "Stack Pop") on success — see
                // porting note 3 above.
                LogEvent("Pop", removed.Value.ToString(), "Success", "Top value removed from stack");
            }
            else
            {
                SetOperationExplanation(
                    "Pop failed. The stack is empty.",
                    "There is nothing to pop.");
                // Phase 27b: this branch is the port of handle_stack_pop()'s
                // own failure path, NOT the button handler's — so it takes
                // the delegated handler's name and details, exactly as the
                // success row above does. Was "Stack Pop" / "Stack is empty".
                LogEvent("Pop", "", "Failed", "Stack was empty or unavailable");
            }

            RefreshStatusAndProgress();
        }

        // ---------------------------------------------------------------
        // Peek — Phase 28. See porting note 9.
        // ---------------------------------------------------------------

        public void OnPeekClicked()
        {
            if (isBusy || stackModule == null) return;
            StartCoroutine(HandlePeek());
        }

        /// <summary>
        /// Godot: handle_stack_peek() (ar_learning_screen.gd:6068), ported
        /// line for line. That function is complete in source — animation,
        /// code sample, explanation, empty-case handling and research logging
        /// — and has NO callers anywhere in the 11,925-line script, so Peek
        /// was unreachable in Godot and stayed unreachable through the port.
        /// Phase 28 wires it. Porting note 9.
        ///
        /// Note the event name is a bare "Peek", not "Stack Peek": source
        /// logs it from the delegated handler, and there is no button-handler
        /// wrapper to carry a prefixed name. Consistent with the Phase 27b
        /// contract (Migration Spec §8o) — a log call takes the name of the
        /// function it lives in.
        /// </summary>
        private IEnumerator HandlePeek()
        {
            SetBusy(true);

            int? topValue = null;
            yield return StartCoroutine(stackModule.HighlightPeek(result => topValue = result));

            SetBusy(false);

            if (topValue.HasValue)
            {
                // Explanation text is source's verbatim; the Operation line
                // drops source's "Peek: " prefix per the Phase 26b dashboard
                // contract (the panel heading already says Operation) and
                // follows the house phrasing used by Pop and Push above.
                SetOperationExplanation(
                    $"Peeked the top of the stack: {topValue.Value}. Peek does not remove the value.",
                    "stack[-1] reads the last value in the Python list. It views the top value without removing it.");
                LogEvent("Peek", topValue.Value.ToString(), "Success", "Viewed top stack value");
            }
            else
            {
                SetOperationExplanation(
                    "Peek failed. The stack is empty.",
                    "stack[-1] cannot be used when the stack is empty because there is no top value.");
                LogEvent("Peek", "", "Failed", "Stack was empty");
            }

            RefreshStatusAndProgress();
        }

        // ---------------------------------------------------------------
        // Search
        // ---------------------------------------------------------------

        public void OnSearchClicked()
        {
            if (isBusy || stackModule == null) return;
            StartCoroutine(HandleSearch());
        }

        private IEnumerator HandleSearch()
        {
            if (stackModule.isAnimating)
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
                    "Type a value, then click Search.");
                LogEvent("Stack Search", "", "Failed", "No value entered for stack search");
                yield break;
            }

            if (!int.TryParse(typedValue, out int target))
            {
                SetOperationExplanation(
                    $"Search failed. {typedValue} is not a valid number.",
                    "Only numbers are accepted.");
                LogEvent("Stack Search", typedValue, "Failed", "Invalid stack search input");
                yield break;
            }

            // No dedicated "stack is empty" branch here — see porting note 3
            // above. An empty stack falls straight through to StackModule's
            // own currentCells.Count == 0 guard and reports as "not found",
            // exactly matching handle_stack_search() in the Godot source.

            SetBusy(true);

            int foundIndex = -1;
            yield return StartCoroutine(stackModule.SearchValue(target, result => foundIndex = result));

            SetBusy(false);

            if (foundIndex >= 0)
            {
                int positionFromTop = (stackModule.values.Count - 1) - foundIndex;

                string explanation = positionFromTop == 0
                    ? $"Search checks stack values from top to bottom. Found at the top (index {foundIndex})."
                    : $"Search checks stack values from top to bottom. Found {positionFromTop + 1} positions from the top.";

                SetOperationExplanation(
                    $"Value {target} was found in the stack.",
                    explanation);
                LogEvent("Stack Search", typedValue, "Success", $"Value found at stack index {foundIndex}");
            }
            else
            {
                SetOperationExplanation(
                    $"Value {target} was not found in the stack.",
                    "Search checks stack values from top to bottom. No stored value matched the input.");
                LogEvent("Stack Search", typedValue, "Failed", "Value not found in stack");
            }

            RefreshStatusAndProgress();
        }

        // ---------------------------------------------------------------
        // Traverse
        // ---------------------------------------------------------------

        public void OnTraverseClicked()
        {
            if (isBusy || stackModule == null) return;
            StartCoroutine(HandleTraverse());
        }

        private IEnumerator HandleTraverse()
        {
            if (stackModule.isAnimating)
            {
                SetOperationExplanation(
                    "Please wait.",
                    "An animation is still running.");
                yield break;
            }

            if (stackModule.values.Count == 0)
            {
                SetOperationExplanation(
                    "Traverse failed. The stack is empty.",
                    "There is nothing to traverse.");
                LogEvent("Stack Traverse", "", "Failed", "Stack is empty");
                yield break;
            }

            SetBusy(true);

            yield return StartCoroutine(stackModule.TraverseStack());

            SetBusy(false);

            string traverseText = FormatValuesTopToBottom(stackModule.values);

            SetOperationExplanation(
                $"Traversed values: {traverseText}",
                "Traverse visits each value from the top down to the bottom.");
            LogEvent("Stack Traverse", traverseText, "Success", "Stack values traversed from top to bottom");

            RefreshStatusAndProgress();
        }

        // ---------------------------------------------------------------
        // Reset
        // ---------------------------------------------------------------

        public void OnResetClicked()
        {
            if (isBusy || stackModule == null) return;

            stackModule.ResetStack();

            if (valueInput != null) valueInput.text = "";

            SetOperationExplanation(
                "Stack reset successful.",
                "The stack returned to its default values.");
            LogEvent("Reset", "", "Success", "Stack reset to default values");

            RefreshStatusAndProgress();
        }

        // ---------------------------------------------------------------
        // Shared helpers
        // ---------------------------------------------------------------

        private void SetBusy(bool busy)
        {
            isBusy = busy;

            if (pushButton != null) pushButton.interactable = !busy;
            if (popButton != null) popButton.interactable = !busy;
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
        /// Direct port of update_stack_status_label(). Note the source's own
        /// asymmetry, preserved as-is: the single-value case ends with a
        /// period, the multi-value case does not.
        /// </summary>
        private void RefreshStatusAndProgress()
        {
            if (stackModule == null) return;

            List<int> values = stackModule.values;

            if (values.Count == 0)
            {
                DashboardController.Instance?.SetStatus("The stack is empty.");
            }
            else if (values.Count == 1)
            {
                DashboardController.Instance?.SetStatus($"Top and Bottom are both {values[0]}.");
            }
            else
            {
                int topValue = values[values.Count - 1];
                int bottomValue = values[0];
                DashboardController.Instance?.SetStatus($"Top = {topValue} | Bottom = {bottomValue}");
            }

            DashboardController.Instance?.SetProgress(
                $"{values.Count}/{StackMaxValues} values stored.");

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
        /// Traverse reports top-to-bottom, the reverse of storage order
        /// (values[^1] is the top). Matches
        /// handle_stack_traverse()'s stack_dashboard_values.duplicate().reverse().
        /// </summary>
        private static string FormatValuesTopToBottom(List<int> values)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = values.Count - 1; i >= 0; i--)
            {
                if (sb.Length > 0) sb.Append(" -> ");
                sb.Append(values[i]);
            }
            return sb.ToString();
        }
    }
}
