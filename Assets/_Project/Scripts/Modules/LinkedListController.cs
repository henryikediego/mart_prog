using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MartProg.Modules
{
    /// <summary>
    /// Phase 21: Linked List interactivity.
    ///
    /// Wires the on-screen control strip (Insert / Delete Head / Search /
    /// Traverse / Reset buttons + a numeric input field) to
    /// LinkedListModule's operations, mirrors the resulting state onto
    /// DashboardController's six panels, and logs every action through
    /// ResearchLogger — matching the original Godot behaviour implemented
    /// by ar_learning_screen.gd's _handle_linked_list_insert_button and its
    /// siblings (_delete_button, _search_button, _traverse_button,
    /// _reset_button), plus their helper functions
    /// (handle_linked_list_insert, handle_linked_list_delete_head,
    /// handle_linked_list_search, handle_linked_list_traverse,
    /// set_linked_list_dashboard_message, update_linked_list_status_label).
    ///
    /// Built directly against QueueController.cs's already-validated shape.
    /// Porting notes specific to Linked List — confirmed by reading
    /// ar_learning_screen.gd's actual linked-list handlers directly, not
    /// assumed from Array/Stack/Queue by structural similarity:
    ///
    /// 1. Max capacity — 5, matching LINKED_LIST_MAX_VALUES in
    ///    ar_learning_screen.gd (the same dual-cap pattern as Array/Stack/
    ///    Queue: the controller enforces 5, LinkedListModule.IsLinkedListFull()
    ///    (cap 6) is a redundant backstop only).
    ///
    /// 2. CONFIRMED SOURCE BUG, corrected rather than ported — per this
    ///    project's established exception for genuine bugs that would break
    ///    already-built functionality (first applied to Phase 20's Change
    ///    Module availability-check bug): _handle_linked_list_reset_button()
    ///    in ar_learning_screen.gd opens with an UNCONDITIONAL call to
    ///    linked_list_module.reset_linked_list() — a method that does not
    ///    exist anywhere in linked_list_module.gd (only reset_list() is
    ///    defined; confirmed via full-text search of both files). In Godot
    ///    this would throw a runtime "Invalid call: Nonexistent function"
    ///    error the instant Reset is clicked in the Linked List module,
    ///    almost certainly aborting the rest of the handler — including its
    ///    own very next lines, which correctly guard reset_list() behind a
    ///    has_method() check and would otherwise have worked fine. This
    ///    controller's OnResetClicked() calls LinkedListModule.ResetList()
    ///    directly instead (the correct, working logic the source's own
    ///    guarded fallback already demonstrates), skipping the broken call
    ///    entirely rather than reproducing it.
    ///
    /// 3. CONFIRMED, genuinely preserved quirk — the Status panel uses TWO
    ///    DIFFERENT formats depending on which operation last touched it:
    ///    - Insert's success path uses LinkedListModule.GetStatusText()'s
    ///      format ("Head = X | Tail = Y | Size = Z"), matching
    ///      get_status_text() in source.
    ///    - Delete Head's and Reset's success paths use a different format
    ///      ("Head = X | Tail = Y", no Size, plus a dedicated single-node
    ///      phrasing "Head and Tail are both X."), matching
    ///      update_linked_list_status_label() in ar_learning_screen.gd.
    ///    - Search and Traverse touch neither — confirmed by direct
    ///      inspection of _handle_linked_list_search_button() and
    ///      _handle_linked_list_traverse_button(), neither of which
    ///      references the status label at all. The Status panel is simply
    ///      left showing whatever it last said.
    ///    - On entering the module (EnterModule()), the Status panel starts
    ///      BLANK, not populated — matching update_linked_list_dashboard(),
    ///      which explicitly hides and clears index_explanation_label on
    ///      every module-content refresh; reset_dashboard_state_for_module_start's
    ///      LinkedList branch resets values but does not call
    ///      update_linked_list_status_label(). This is a genuine difference
    ///      from Stack/Queue's EnterModule(), which populate Status
    ///      immediately — not a Unity-port inconsistency.
    ///    All four of the above are ported faithfully rather than
    ///    normalized to one consistent format/trigger.
    ///
    /// 4. The Progress panel (Unity-only — DashboardController's six-panel
    ///    layout has no literal Godot equivalent) is refreshed after every
    ///    operation regardless of whether Status is touched, matching the
    ///    established Unity-side convention from every prior module rather
    ///    than inventing a new one for a feature Godot never had.
    ///
    /// 5. Delete is split the same way Stack's/Queue's are: an outer
    ///    "already empty" guard (logged as "Linked List Delete", matching
    ///    _handle_linked_list_delete_button()'s own early-return check) and
    ///    an inner success/failure path (logged as "Linked List Delete
    ///    Head", matching handle_linked_list_delete_head()) — a real
    ///    asymmetry in the source, ported as-is. The inner failure branch
    ///    (module returns null despite the outer guard already having ruled
    ///    out an empty list — only reachable if an animation is mid-flight)
    ///    is defensive and effectively unreachable here: SetBusy(true)
    ///    disables the Delete button for the duration of any operation, the
    ///    same guarantee Stack/Queue already rely on. The source's own
    ///    analogous branch has its own copy-paste text bug (its message
    ///    reports the removed value even though it is null there); this
    ///    controller gives that unreachable branch a sensible message
    ///    instead of reproducing the bug, since nothing here can ever
    ///    surface it to a user.
    ///
    /// 6. Search has no dedicated "the list is empty" message — an empty
    ///    list search falls straight through to LinkedListModule's own
    ///    currentNodes.Count == 0 guard and reports as "not found", matching
    ///    handle_linked_list_search() exactly (no empty-list pre-check in
    ///    source). Traverse, unlike Search, DOES have a dedicated empty
    ///    check in source, and keeps it here — same asymmetry already
    ///    established for Stack/Queue.
    ///
    /// 7. Traverse's success message ends with "-> null", a Linked-List-
    ///    specific detail not present in Stack's/Queue's traverse messages
    ///    — matches _handle_linked_list_traverse_button() exactly.
    ///
    /// 8. Implements IModuleController.EnterModule() from the start (Phase
    ///    20 already established the pattern for every module built after
    ///    it) — see Migration Spec §8f / §10.
    /// </summary>
    public class LinkedListController : MonoBehaviour, IModuleController
    {
        [Header("Module Reference")]
        [Tooltip("Auto-found via FindFirstObjectByType if left empty.")]
        [SerializeField] private LinkedListModule linkedListModule;

        [Header("UI References")]
        [SerializeField] private TMP_InputField valueInput;
        [SerializeField] private Button insertButton;
        [SerializeField] private Button deleteButton;
        [SerializeField] private Button searchButton;
        [SerializeField] private Button traverseButton;
        [SerializeField] private Button resetButton;
        [Tooltip("The Quiz trigger button living in THIS module's own control strip, " +
                 "alongside Search/Traverse (not the admin row). Assign the same Button " +
                 "GameObject here as on QuizController.linkedListQuizButton — that script " +
                 "wires the click behavior; this field only makes sure this module's " +
                 "button-grid column/row/cell-size math (CountAssignedButtons() below) " +
                 "accounts for it.")]
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
        // LINKED_LIST_MAX_VALUES. LinkedListModule.IsLinkedListFull() (cap 6)
        // is checked too, purely as a defensive backstop; it should never be
        // the one that trips first. See porting note 1 above.
        private const int LinkedListMaxValues = 5;

        private bool isBusy;

        private void Start()
        {
            if (linkedListModule == null)
            {
                linkedListModule = FindFirstObjectByType<LinkedListModule>();
            }

            if (linkedListModule == null)
            {
                Debug.LogError("LinkedListController: no LinkedListModule found in the scene.");
            }

            if (ResearchLogger.Instance != null)
            {
                ResearchLogger.Instance.ModuleName = "LinkedList";
            }

            WireButtons();
            StartCoroutine(InitializeDeferred());
        }

        /// <summary>
        /// Same one-frame defer as every other module controller — see
        /// Migration Spec §5.
        /// </summary>
        private IEnumerator InitializeDeferred()
        {
            yield return null;

            EnterModule();

            ApplyResponsiveButtonLayout();
        }

        /// <summary>
        /// IModuleController.EnterModule() — see porting note 8 above and
        /// Migration Spec §8f. Resets Linked List back to its starting
        /// values and refreshes every dashboard panel, matching Godot's
        /// reset_dashboard_state_for_module_start("LinkedList") +
        /// update_linked_list_dashboard() pair. Status starts BLANK, not
        /// populated — see porting note 3 above.
        /// </summary>
        public void EnterModule()
        {
            SetBusy(false);

            if (linkedListModule != null) linkedListModule.ResetList();
            if (valueInput != null) valueInput.text = "";

            // Phase 26b dashboard audit — see DashboardController.cs.
            DashboardController.Instance?.SetSummary(
                "A linked list stores values as nodes. Each node contains data and a link " +
                "to the next node.");
            DashboardController.Instance?.SetInstruction(
                "Choose Insert, Delete Head, Search, Traverse, Reset, or Quiz to continue.");
            DashboardController.Instance?.SetOperation("No operation selected yet.");
            DashboardController.Instance?.SetExplanation(
                "A linked list begins with a head node. Each node points to the next node, " +
                "and the final node points to null.");
            // Populated on entry as of the Phase 26b/26c dashboard audit.
            // It used to be blanked here, faithfully reproducing Godot (which
            // hides index_explanation_label for every non-Memory module) — but
            // Unity's panels are permanent, so "faithful" rendered as a headed
            // box with nothing in it. The reset-format writer is the correct
            // one for entry, since EnterModule() has just reset the list.
            SetStatusFromDeleteOrReset();
            RebuildDashboardLayout();

            RefreshProgress();
        
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
            if (insertButton != null) insertButton.onClick.AddListener(OnInsertClicked);
            if (deleteButton != null) deleteButton.onClick.AddListener(OnDeleteClicked);
            if (searchButton != null) searchButton.onClick.AddListener(OnSearchClicked);
            if (traverseButton != null) traverseButton.onClick.AddListener(OnTraverseClicked);
            if (resetButton != null) resetButton.onClick.AddListener(OnResetClicked);
        }

        /// <summary>
        /// Same responsive grid pattern as every other module controller —
        /// see Migration Spec §5.
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

            Debug.Log($"LinkedListController: applying a {columns}-column / {rows}-row button layout " +
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
            if (insertButton != null) count++;
            if (deleteButton != null) count++;
            if (searchButton != null) count++;
            if (traverseButton != null) count++;
            if (resetButton != null) count++;
            if (quizButton != null) count++;
            return count;
        }

        // ---------------------------------------------------------------
        // Insert — synchronous, no animation (see class comment note 2 in
        // LinkedListModule.cs)
        // ---------------------------------------------------------------

        public void OnInsertClicked()
        {
            if (isBusy || linkedListModule == null) return;
            HandleInsert();
        }

        private void HandleInsert()
        {
            string typedValue = valueInput != null ? valueInput.text.Trim() : "";

            if (string.IsNullOrEmpty(typedValue))
            {
                SetOperationExplanation(
                    "Insert failed. No value was entered.",
                    "Type a numeric value in the input box before clicking Insert. A new node will be added to the linked list.");
                LogEvent("Linked List Insert", "", "Failed", "No value entered for linked list insert");
                return;
            }

            if (!int.TryParse(typedValue, out int newValue))
            {
                SetOperationExplanation(
                    $"Insert failed. {typedValue} is not a valid number.",
                    "This Linked List activity currently accepts numeric values only. Enter a number such as 40 before clicking Insert.");
                LogEvent("Linked List Insert", typedValue, "Failed", "Invalid linked list insert input");
                return;
            }

            if (linkedListModule.values.Count >= LinkedListMaxValues)
            {
                SetOperationExplanation(
                    "Insert failed. The linked list is full.",
                    $"This activity allows a maximum of {LinkedListMaxValues} nodes.");
                LogEvent("Linked List Insert", typedValue, "Failed", "Linked list maximum node count reached");
                return;
            }

            bool success = linkedListModule.InsertValue(newValue);

            if (!success)
            {
                SetOperationExplanation(
                    $"Insert failed. {linkedListModule.GetLastActionMessage()}",
                    "The node was not inserted.");
                LogEvent("Linked List Insert", typedValue, "Failed", linkedListModule.GetLastActionMessage());
                return;
            }

            SetOperationExplanation(
                $"Inserted value {newValue} as a new node.",
                $"Insert creates a new node and links it into the linked list. Current size: {linkedListModule.values.Count}/{LinkedListMaxValues}.");
            LogEvent("Linked List Insert", typedValue, "Success", linkedListModule.GetLastActionMessage());

            if (valueInput != null) valueInput.text = "";

            // Insert uses GetStatusText()'s format — see porting note 3.
            SetStatusFromInsert();
            RefreshProgress();
        }

        // ---------------------------------------------------------------
        // Delete Head — animates (see LinkedListModule.cs class comment
        // note 3)
        // ---------------------------------------------------------------

        public void OnDeleteClicked()
        {
            if (isBusy || linkedListModule == null) return;
            StartCoroutine(HandleDelete());
        }

        private IEnumerator HandleDelete()
        {
            if (linkedListModule.values.Count == 0)
            {
                SetOperationExplanation(
                    "Delete failed. The linked list is empty.",
                    "Delete Head cannot remove a node because there are no nodes currently stored in the linked list.");
                LogEvent("Linked List Delete", "", "Failed", "Linked list is empty");
                yield break;
            }

            SetBusy(true);

            int? removed = null;
            yield return StartCoroutine(linkedListModule.DeleteHead(result => removed = result));

            SetBusy(false);

            if (removed.HasValue)
            {
                SetOperationExplanation(
                    $"Deleted the head node with value {removed.Value}.",
                    $"Delete Head removes the first node in the linked list. The next node becomes the new head. Current size: {linkedListModule.values.Count}/{LinkedListMaxValues}.");
                LogEvent("Linked List Delete Head", removed.Value.ToString(), "Success", "Head node removed");
            }
            else
            {
                // Defensive/effectively unreachable — see porting note 5.
                SetOperationExplanation(
                    "Delete failed. Please wait for the current animation to finish.",
                    "Delete Head cannot run while another animation is still in progress.");
                LogEvent("Linked List Delete Head", "", "Failed", "Linked list was empty");
            }

            // Delete uses update_linked_list_status_label()'s format — see
            // porting note 3.
            SetStatusFromDeleteOrReset();
            RefreshProgress();
        }

        // ---------------------------------------------------------------
        // Search
        // ---------------------------------------------------------------

        public void OnSearchClicked()
        {
            if (isBusy || linkedListModule == null) return;
            StartCoroutine(HandleSearch());
        }

        private IEnumerator HandleSearch()
        {
            if (linkedListModule.isAnimating)
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
                LogEvent("Linked List Search", "", "Failed", "No value entered for linked list search");
                yield break;
            }

            if (!int.TryParse(typedValue, out int target))
            {
                SetOperationExplanation(
                    $"Search failed. {typedValue} is not a valid number.",
                    "This Linked List activity currently stores numeric node values, so the search input must be a number such as 10, 20, or 30.");
                LogEvent("Linked List Search", typedValue, "Failed", "Invalid linked list search input");
                yield break;
            }

            // No dedicated "the list is empty" branch here — see porting
            // note 6 above. An empty list falls straight through to
            // LinkedListModule's own currentNodes.Count == 0 guard and
            // reports as "not found", exactly matching
            // handle_linked_list_search().

            SetBusy(true);

            int foundIndex = -1;
            yield return StartCoroutine(linkedListModule.SearchValue(target, result => foundIndex = result));

            SetBusy(false);

            if (foundIndex >= 0)
            {
                SetOperationExplanation(
                    $"Value {target} was found at node index {foundIndex}.",
                    "Search starts at the head node and checks each node one by one until the value is found.");
                LogEvent("Linked List Search", typedValue, "Success", $"Value found at linked list node index {foundIndex}");
            }
            else
            {
                SetOperationExplanation(
                    $"Value {target} was not found in the linked list.",
                    "Search checks the linked list from the head node to the final node. If no node contains the entered value, the search result is not found.");
                LogEvent("Linked List Search", typedValue, "Failed", "Value not found in linked list");
            }

            // Search does not touch Status — see porting note 3. Progress
            // doesn't change either (Search never adds/removes a node), but
            // refreshed anyway for consistency with every other operation.
            RefreshProgress();
        }

        // ---------------------------------------------------------------
        // Traverse
        // ---------------------------------------------------------------

        public void OnTraverseClicked()
        {
            if (isBusy || linkedListModule == null) return;
            StartCoroutine(HandleTraverse());
        }

        private IEnumerator HandleTraverse()
        {
            if (linkedListModule.isAnimating)
            {
                SetOperationExplanation(
                    "Please wait.",
                    "An animation is still running.");
                yield break;
            }

            if (linkedListModule.values.Count == 0)
            {
                SetOperationExplanation(
                    "Traverse failed. The linked list is empty.",
                    "Traversal cannot be completed because there are no nodes currently stored in the linked list.");
                LogEvent("Linked List Traverse", "", "Failed", "Linked list is empty");
                yield break;
            }

            SetBusy(true);

            yield return StartCoroutine(linkedListModule.TraverseLinkedList());

            SetBusy(false);

            string traverseText = FormatValuesHeadToTail(linkedListModule.values);

            // "-> null" suffix is Linked-List-specific — see porting note 7.
            SetOperationExplanation(
                $"Traversed values: {traverseText} -> null",
                "Traverse visits each linked list node from the head node to the final node without changing the links between nodes.");
            LogEvent("Linked List Traverse", traverseText, "Success", "Linked list nodes traversed from head to tail");

            // Traverse does not touch Status — see porting note 3. Progress
            // refreshed anyway for consistency, same as Search.
            RefreshProgress();
        }

        // ---------------------------------------------------------------
        // Reset
        // ---------------------------------------------------------------

        public void OnResetClicked()
        {
            if (isBusy || linkedListModule == null) return;

            // Calls ResetList() directly — NOT a literal port of
            // _handle_linked_list_reset_button()'s first line. See porting
            // note 2 above.
            linkedListModule.ResetList();

            if (valueInput != null) valueInput.text = "";

            SetOperationExplanation(
                "Linked list reset successful.",
                $"The linked list returns to its default values. Current size: {linkedListModule.values.Count}/{LinkedListMaxValues}.");
            LogEvent("Reset", "", "Success", "Linked list reset to default nodes");

            // Reset uses the same format as Delete Head — see porting note 3.
            SetStatusFromDeleteOrReset();
            RefreshProgress();
        }

        // ---------------------------------------------------------------
        // Shared helpers
        // ---------------------------------------------------------------

        private void SetBusy(bool busy)
        {
            isBusy = busy;

            if (insertButton != null) insertButton.interactable = !busy;
            if (deleteButton != null) deleteButton.interactable = !busy;
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
        /// Direct port of get_status_text()'s format — used only after
        /// Insert. See porting note 3.
        /// </summary>
        private void SetStatusFromInsert()
        {
            if (linkedListModule == null) return;

            DashboardController.Instance?.SetStatus(linkedListModule.GetStatusText());
            RebuildDashboardLayout();
        }

        /// <summary>
        /// Direct port of update_linked_list_status_label()'s format — used
        /// after Delete Head and Reset. A DIFFERENT format from Insert's —
        /// see porting note 3. Note the source's own asymmetry, preserved
        /// as-is: the single-node case ends with a period, the multi-node
        /// case does not — the same pattern Stack's/Queue's status text has.
        /// </summary>
        private void SetStatusFromDeleteOrReset()
        {
            if (linkedListModule == null) return;

            List<int> values = linkedListModule.values;
            string text;

            if (values.Count == 0)
            {
                text = "The list is empty.";
            }
            else if (values.Count == 1)
            {
                text = $"Head and Tail are both {values[0]}.";
            }
            else
            {
                text = $"Head = {values[0]} | Tail = {values[values.Count - 1]}";
            }

            DashboardController.Instance?.SetStatus(text);
            RebuildDashboardLayout();
        }

        /// <summary>
        /// Progress panel only — see porting note 4 (Unity-only feature,
        /// refreshed after every operation regardless of whether Status
        /// changed).
        /// </summary>
        private void RefreshProgress()
        {
            if (linkedListModule == null) return;

            DashboardController.Instance?.SetProgress(
                $"{linkedListModule.values.Count}/{LinkedListMaxValues} nodes stored.");

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
        /// Traverse reports head-to-tail, which is simply forward order
        /// through `values` (index 0 is the head) — same as Queue's
        /// FormatValuesFrontToRear, contrast Stack's reverse-iterating
        /// FormatValuesTopToBottom.
        /// </summary>
        private static string FormatValuesHeadToTail(List<int> values)
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
