using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MartProg.Modules
{
    /// <summary>
    /// Stage 16 / Phase 16: Array/List interactivity.
    ///
    /// Wires the on-screen control strip (Insert / Delete / Search / Traverse / Reset
    /// buttons + a numeric input field) to ArrayModule's coroutine-based operations,
    /// mirrors the resulting state onto DashboardController's six panels, and logs
    /// every action through ResearchLogger — matching the original Godot behaviour
    /// implemented by ar_learning_screen.gd's _handle_array_list_insert_button and
    /// its siblings (_delete_button, _search_button, _traverse_button, _reset_button),
    /// plus their helper functions (handle_array_insert, handle_array_delete,
    /// handle_array_search, handle_array_traverse, set_array_list_dashboard_message,
    /// update_array_status_label).
    ///
    /// Porting notes (see accompanying dev-report addendum for the full write-up):
    ///
    /// 1. Max capacity — CONFIRMED 5, not 10. The Godot controller enforced its own
    ///    tighter ARRAY_LIST_MAX_VALUES = 5 cap in ar_learning_screen.gd, separate
    ///    from array_module.gd's own MAX_ARRAY_VALUES = 10. That 5-item cap was the
    ///    one actually experienced by students, so it's the one this controller
    ///    enforces (MaxArrayValues below). ArrayModule.IsArrayFull() (cap = 10) is
    ///    kept as a secondary, redundant safety check — it should never be the one
    ///    that actually fires, since the 5-cap always triggers first.
    ///
    /// 2. State tracking: Godot's controller kept its own shadow copies
    ///    (array_list_dashboard_values / array_list_current_value_count) because its
    ///    dashboard lived in a different script than array_module.gd. In Unity,
    ///    ArrayModule.values is already a public field on the same object graph, so
    ///    this controller reads it directly instead of maintaining a duplicate list.
    ///
    /// 3. Panel titles: each DashboardController panel (Operation / Explanation /
    ///    Status / Progress) already renders its own bold title from the
    ///    InformationPanel prefab — the strings this script sends via SetOperation /
    ///    SetExplanation / SetStatus / SetProgress are body text only and must NOT
    ///    repeat that title, or it appears twice on screen.
    ///
    /// 4. Responsive button grid: InitializeDeferred() waits one frame before
    ///    reading ResponsiveManager and touching DashboardController (script-
    ///    execution-order safety — ResponsiveManager determines its own profile in
    ///    Start(), and DashboardController's panel references are only wired by an
    ///    external Initialize() call from DashboardContainer.Start(); Unity doesn't
    ///    guarantee ordering between different scripts' Start() calls in the same
    ///    frame). ApplyResponsiveButtonLayout() then computes both cell WIDTH and
    ///    cell HEIGHT from the grid's own RectTransform, not just width — an
    ///    Editor-authored cell height left at Unity's GridLayoutGroup default
    ///    (100x100) is what let a 3-row grid overflow past its allotted space and
    ///    visually cover the Summary panel below it. Computing both dimensions from
    ///    the actual available RectTransform size means the grid always fits inside
    ///    its own bounds no matter what was left over from Editor setup.
    ///
    /// 5. Dashboard panel content changes need a forced layout rebuild, same as the
    ///    Phase 12 first-frame fix in DashboardContainer.Start(). That earlier fix
    ///    only covers the very first frame; it does not cover *later* text changes
    ///    that change a panel's required height (e.g. a short default message being
    ///    replaced by a longer Insert/Search/Traverse result). Without a rebuild
    ///    after those changes too, the Vertical Layout Group can leave stale panel
    ///    positions in place, producing a transient full-overlap of Operation /
    ///    Explanation / Status text — which is exactly what a Reset click "fixes",
    ///    since it triggers another content change that happens to coincide with a
    ///    rebuild. RebuildDashboardLayout() below applies the same
    ///    Canvas.ForceUpdateCanvases() + LayoutRebuilder.ForceRebuildLayoutImmediate()
    ///    pattern after every dashboard content change, not just the first one.
    ///
    /// 6. Phase 17 fix: Start() used to call DashboardController.Instance.SetSummary()
    ///    (and friends) directly and synchronously, which worked only as long as
    ///    DashboardContainer's Start() (which wires up DashboardController's six
    ///    panel references via Initialize()) happened to run first in the same
    ///    frame -- an unguaranteed ordering. Adding AdminController to the scene
    ///    shifted that ordering enough to flip it, producing a
    ///    NullReferenceException inside DashboardController.SetSummary(). All
    ///    Start()-time DashboardController calls now live in InitializeDeferred(),
    ///    deferred by one frame for the same reason ResponsiveManager reads are.
    ///
    /// 7. Phase 20: implements IModuleController.EnterModule() so
    ///    ChangeModuleController.cs can reset + refresh this module on every
    ///    switch into it, not just the first time the scene loads — see
    ///    Migration Spec §8f. InitializeDeferred() now just waits a frame and
    ///    calls EnterModule() itself, so first-activation and later switches
    ///    share exactly one code path instead of two that could drift apart.
    ///    EnterModule() also resets isBusy/re-enables buttons — a Unity-only
    ///    concern with no Godot equivalent, since deactivating a GameObject
    ///    silently kills any coroutine running on it, which could otherwise
    ///    leave this controller stuck "busy" forever if the student switched
    ///    away mid-animation.
    ///
    /// 8. **Insert logs under TWO different event names, split by outcome —
    ///    and that is source, not a defect here.** Phase 27b research-log
    ///    audit. Godot spreads one button press across two functions:
    ///    `_handle_array_list_insert_button()` validates and logs its own
    ///    three failures under `"Insert"`, returning each time; only on a
    ///    clean parse does it `await handle_array_insert()`, and THAT
    ///    function logs under `"Array Insert"`. So a Godot press emits
    ///    exactly one row either way, named `"Insert"` when it fails early
    ///    and `"Array Insert"` when it reaches the module:
    ///
    ///        empty input              -> "Insert"        Failed
    ///        non-integer input        -> "Insert"        Failed
    ///        screen counter at max    -> "Insert"        Failed
    ///        module refused the value -> "Array Insert"  Failed
    ///        inserted                 -> "Array Insert"  Success
    ///
    ///    Unity previously logged all five as `"Insert"`. Corrected to match
    ///    source exactly, on the same log-comparability grounds as Phase 23's
    ///    Reset naming — these CSVs are analysed against the Godot build's.
    ///    Note the two `"Array Insert"` rows also pass `str(new_value)`, the
    ///    PARSED integer, where the `"Insert"` rows pass the raw typed text;
    ///    so a student typing "007" produces `007` on a failure row and `7`
    ///    on a success row. Ported literally, including that.
    ///
    ///    Anyone filtering the research CSV for array insert activity must
    ///    therefore match BOTH names. Two of Godot's own `"Array Insert"`
    ///    failure branches (empty input, invalid integer) are unreachable —
    ///    the button handler validates the same field first — so they have
    ///    no Unity equivalent and never appear in either build's logs.
    /// </summary>
    public class ArrayListController : MonoBehaviour, IModuleController
    {
        [Header("Module Reference")]
        [Tooltip("Auto-found via FindFirstObjectByType if left empty.")]
        [SerializeField] private ArrayModule arrayModule;

        [Header("UI References")]
        [SerializeField] private TMP_InputField valueInput;
        [SerializeField] private Button insertButton;
        [SerializeField] private Button deleteButton;
        [SerializeField] private Button searchButton;
        [SerializeField] private Button traverseButton;
        [SerializeField] private Button resetButton;
        [Tooltip("The Quiz trigger button living in THIS module's own control strip, " +
                 "alongside Search/Traverse (not the admin row). Assign the same Button " +
                 "GameObject here as on QuizController.arrayQuizButton — that script wires " +
                 "the click behavior; this field only makes sure this module's button-grid " +
                 "column/row/cell-size math (CountAssignedButtons() below) accounts for it.")]
        [SerializeField] private Button quizButton;

        [Header("Layout (optional)")]
        [Tooltip("The Grid Layout Group on ButtonGrid. If assigned, its column count " +
                 "and cell size are set at Start() from ResponsiveManager.GetButtonColumns(), " +
                 "matching the same profile-driven layout the rest of the dashboard uses.")]
        [SerializeField] private GridLayoutGroup buttonGrid;

        [Tooltip("The dashboard's scrollable 'Content' RectTransform (Canvas > " +
                 "DashboardRoot > Scroll View > Viewport > Content) — the same object " +
                 "DashboardContainer.Start() rebuilds on scene load. If assigned, this " +
                 "controller forces the same rebuild every time it changes a panel's " +
                 "text, fixing a transient overlap between Operation/Explanation/Status " +
                 "that otherwise only clears up on the next unrelated content change.")]
        [SerializeField] private RectTransform dashboardContent;

        // The cap actually enforced. Matches ar_learning_screen.gd's
        // ARRAY_LIST_MAX_VALUES, confirmed as the intended value — see porting
        // note 1 above. ArrayModule.IsArrayFull() (cap = 10) is checked too, purely
        // as a defensive backstop; it should never be the one that trips first.
        private const int MaxArrayValues = 5;

        private bool isBusy;

        private void Start()
        {
            if (arrayModule == null)
            {
                arrayModule = FindFirstObjectByType<ArrayModule>();
            }

            if (arrayModule == null)
            {
                Debug.LogError("ArrayListController: no ArrayModule found in the scene.");
            }

            if (ResearchLogger.Instance != null)
            {
                // Defensive: make sure the CSV rows this controller writes are
                // attributed to the right module, even if another module set this
                // last (relevant once Stack/Queue/etc. controllers exist too).
                ResearchLogger.Instance.ModuleName = "ArrayList";
            }

            WireButtons();
            StartCoroutine(InitializeDeferred());
        }

        /// <summary>
        /// Fix (Phase 17): this used to set the dashboard's initial text and
        /// call RefreshStatusAndProgress() directly and synchronously inside
        /// Start(), before only ApplyResponsiveButtonLayout() was deferred by
        /// a frame. That worked by luck as long as DashboardContainer's own
        /// Start() (which calls DashboardController.Instance.Initialize(...),
        /// wiring up its six panel references) happened to run first in the
        /// same frame -- Unity does not guarantee that ordering between two
        /// different scripts' Start() methods. Adding AdminController to the
        /// scene shifted that ordering enough to flip the coin the other way,
        /// and DashboardController.SetSummary() threw a NullReferenceException
        /// because Initialize() hadn't run yet. Deferring everything that
        /// touches DashboardController (and ResponsiveManager, for the same
        /// reason as before) by one frame guarantees every other script's
        /// Start() has already completed first, regardless of which order
        /// Unity happened to pick.
        /// </summary>
        private IEnumerator InitializeDeferred()
        {
            yield return null;

            EnterModule();

            ApplyResponsiveButtonLayout();
        }

        /// <summary>
        /// IModuleController.EnterModule() — see porting note 7 above and
        /// Migration Spec §8f. Resets Array/List back to its starting values
        /// and refreshes every dashboard panel, matching Godot's
        /// reset_dashboard_state_for_module_start("ArrayList") +
        /// update_array_list_dashboard() pair, run together every time this
        /// module is displayed (first activation or a later switch back).
        /// </summary>
        public void EnterModule()
        {
            // Phase 29 (F1) backstop: if this controller's GameObject was
            // disabled mid-animation but the module's was not, the module's
            // own OnDisable() never fired and isAnimating is still true —
            // which would make the Reset*() call below silently no-op and
            // leave the module permanently unusable. Clear it first.
            if (arrayModule != null) arrayModule.ForceStopAnimation();

            SetBusy(false);

            if (arrayModule != null) arrayModule.ResetArray();
            if (valueInput != null) valueInput.text = "";

            // Phase 26b dashboard audit — see the DASHBOARD PANEL CONTRACT
            // note in DashboardController.cs. Summary now carries Godot's
            // module_description_label sentence, not just the title.
            DashboardController.Instance?.SetSummary(
                "Arrays/lists store values using indexes. The first value is usually at index 0.");
            DashboardController.Instance?.SetInstruction(
                "Choose Insert, Delete, Search, Traverse, Reset, or Quiz to continue.");
            DashboardController.Instance?.SetOperation("No operation selected yet.");
            // Godot's wording, with ONE deliberate correction: source reads
            // "The current array/list values IS stored", which is a verb-
            // agreement error in student-facing text. Corrected to "are" in
            // Phase 26b at the user's decision, and recorded here alongside
            // the migration's other deliberate deviations (LoopModule's /2.2
            // typo, LoopsController's success-on-rejection handler). The rest
            // of the sentence is verbatim — an earlier Unity draft had
            // paraphrased the whole line away, which is what the audit caught.
            DashboardController.Instance?.SetExplanation(
                "The current array/list values are stored in numbered positions called indexes.");
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

            if (valueInput != null) valueInput.onSubmit.AddListener(_ => OnInsertClicked());

            if (insertButton != null) insertButton.onClick.AddListener(OnInsertClicked);
            if (deleteButton != null) deleteButton.onClick.AddListener(OnDeleteClicked);
            if (searchButton != null) searchButton.onClick.AddListener(OnSearchClicked);
            if (traverseButton != null) traverseButton.onClick.AddListener(OnTraverseClicked);
            if (resetButton != null) resetButton.onClick.AddListener(OnResetClicked);
        }

        /// <summary>
        /// Mirrors ResponsiveManager's device-driven column count (2 on mobile
        /// portrait, 3 landscape, 4 tablet, 6 desktop — see responsive_manager.gd's
        /// get_button_columns()) onto the button grid, and sizes each cell to fill
        /// the strip's actual width and height so labels like "Traverse" get enough
        /// room instead of word-wrapping mid-letter, and the grid never overflows
        /// past its own RectTransform into whatever is underneath it. If the device
        /// rotates mid-session this won't re-flow (ResponsiveManager itself doesn't
        /// re-evaluate its profile automatically either, so this matches existing
        /// behaviour rather than regressing it).
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

            Debug.Log($"ArrayListController: applying a {columns}-column / {rows}-row button layout " +
                      $"(profile: {ResponsiveManager.Instance?.GetLayoutProfileName() ?? "unavailable"}).");

            buttonGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            buttonGrid.constraintCount = columns;

            // Standing Convention (Migration Spec §5): force a layout rebuild
            // before trusting a computed RectTransform size on the first frame.
            Canvas.ForceUpdateCanvases();

            RectTransform gridRect = buttonGrid.GetComponent<RectTransform>();

            if (gridRect != null)
            {
                // Both dimensions are computed from the grid's own RectTransform,
                // not just width — a fixed Editor-authored cell height (Unity's
                // GridLayoutGroup default is 100x100) is what let a 3-row grid
                // overflow past its allotted space and bleed into DashboardRoot's
                // Summary panel underneath it. Computing height the same way width
                // already was makes the grid always fit inside its own bounds,
                // regardless of what cell size was left over from the Editor.
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
        // Insert
        // ---------------------------------------------------------------

        public void OnInsertClicked()
        {
            if (isBusy || arrayModule == null) return;
            StartCoroutine(HandleInsert());
        }

        private IEnumerator HandleInsert()
        {
            if (arrayModule.isAnimating)
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
                    "Insert failed. No value was entered.",
                    "Type a number, then click Insert.");
                LogEvent("Insert", "", "Failed", "No value entered for array/list insert");
                yield break;
            }

            if (!int.TryParse(typedValue, out int newValue))
            {
                SetOperationExplanation(
                    $"Insert failed. {typedValue} is not a valid number.",
                    "Only numbers are accepted.");
                LogEvent("Insert", typedValue, "Failed", "Invalid array/list insert input");
                yield break;
            }

            if (arrayModule.values.Count >= MaxArrayValues || arrayModule.IsArrayFull())
            {
                SetOperationExplanation(
                    "Insert failed. The array/list is full.",
                    $"The array/list can hold at most {MaxArrayValues} values. Delete one first.");
                LogEvent("Insert", typedValue, "Failed", "Array/list maximum value count reached");
                yield break;
            }

            SetBusy(true);

            bool success = false;
            yield return StartCoroutine(arrayModule.InsertValue(newValue, result => success = result));

            SetBusy(false);

            if (!success)
            {
                SetOperationExplanation(
                    $"Insert failed. {arrayModule.GetLastActionMessage()}",
                    "The value was not inserted.");
                // Godot: this branch lives in handle_array_insert(), not in
                // _handle_array_list_insert_button() — so source logs it under
                // "Array Insert", and passes str(new_value) rather than the raw
                // typed text. Porting note 8.
                LogEvent("Array Insert", newValue.ToString(), "Failed", arrayModule.GetLastActionMessage());
                yield break;
            }

            int insertedIndex = arrayModule.values.Count - 1;

            SetOperationExplanation(
                $"Inserted value {newValue} at index {insertedIndex}.",
                $"numbers.append({newValue}) adds a new value to the end of the array/list.");
            // Godot logs the SUCCESS row under a different name from the
            // failure rows — "Array Insert", from handle_array_insert().
            // Porting note 8.
            LogEvent("Array Insert", newValue.ToString(), "Success", arrayModule.GetLastActionMessage());

            if (valueInput != null) valueInput.text = "";

            RefreshStatusAndProgress();
        }

        // ---------------------------------------------------------------
        // Delete
        // ---------------------------------------------------------------

        public void OnDeleteClicked()
        {
            if (isBusy || arrayModule == null) return;
            StartCoroutine(HandleDelete());
        }

        private IEnumerator HandleDelete()
        {
            if (arrayModule.isTraversing)
            {
                SetOperationExplanation(
                    "Please wait.",
                    "A traversal is still running.");
                yield break;
            }

            if (arrayModule.values.Count == 0)
            {
                SetOperationExplanation(
                    "Delete failed. The array/list is empty.",
                    "There is nothing to delete.");
                LogEvent("Delete", "", "Failed", "Array/list was empty");
                yield break;
            }

            SetBusy(true);

            int? removed = null;
            yield return StartCoroutine(arrayModule.DeleteValue(result => removed = result));

            SetBusy(false);

            if (removed.HasValue)
            {
                SetOperationExplanation(
                    $"Deleted the last value {removed.Value} from the array/list.",
                    "numbers.pop() removes the value at the end of the array/list.");
                LogEvent("Delete", removed.Value.ToString(), "Success", "Value removed from end of array/list");
            }
            else
            {
                SetOperationExplanation(
                    "Delete failed.",
                    "The delete operation could not be completed.");
                LogEvent("Delete", "", "Failed", "Array/list was empty or unavailable");
            }

            RefreshStatusAndProgress();
        }

        // ---------------------------------------------------------------
        // Search
        // ---------------------------------------------------------------

        public void OnSearchClicked()
        {
            if (isBusy || arrayModule == null) return;
            StartCoroutine(HandleSearch());
        }

        private IEnumerator HandleSearch()
        {
            if (arrayModule.isAnimating)
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
                    "Type a value to find, then click Search.");
                LogEvent("Search", "", "Failed", "No value entered for array/list search");
                yield break;
            }

            if (!int.TryParse(typedValue, out int target))
            {
                SetOperationExplanation(
                    $"Search failed. {typedValue} is not a valid number.",
                    "Only numbers are accepted.");
                LogEvent("Search", typedValue, "Failed", "Invalid array/list search input");
                yield break;
            }

            if (arrayModule.values.Count == 0)
            {
                SetOperationExplanation(
                    "Search failed. The array/list is empty.",
                    "There is nothing to search.");
                LogEvent("Search", typedValue, "Failed", "Array/list is empty");
                yield break;
            }

            SetBusy(true);

            int foundIndex = -1;
            yield return StartCoroutine(arrayModule.SearchValue(target, result => foundIndex = result));

            SetBusy(false);

            if (foundIndex >= 0)
            {
                SetOperationExplanation(
                    $"Value {target} was found at index {foundIndex}.",
                    "Search checks each value until a match is found.");
                LogEvent("Search", typedValue, "Success", $"Value found at index {foundIndex}");
            }
            else
            {
                SetOperationExplanation(
                    $"Value {target} was not found in the array/list.",
                    "No stored value matched the input.");
                LogEvent("Search", typedValue, "Failed", "Value not found in array/list");
            }

            RefreshStatusAndProgress();
        }

        // ---------------------------------------------------------------
        // Traverse
        // ---------------------------------------------------------------

        public void OnTraverseClicked()
        {
            if (isBusy || arrayModule == null) return;
            StartCoroutine(HandleTraverse());
        }

        private IEnumerator HandleTraverse()
        {
            if (arrayModule.isAnimating)
            {
                SetOperationExplanation(
                    "Please wait.",
                    "An animation is still running.");
                yield break;
            }

            if (arrayModule.values.Count == 0)
            {
                SetOperationExplanation(
                    "Traverse failed. The array/list is empty.",
                    "There is nothing to traverse.");
                LogEvent("Traverse", "", "Failed", "Array/list is empty");
                yield break;
            }

            SetBusy(true);

            yield return StartCoroutine(arrayModule.TraverseArray());

            SetBusy(false);

            string traverseText = FormatValuesWithArrows(arrayModule.values);

            SetOperationExplanation(
                $"Traversed values: {traverseText}",
                "Traverse visits each value from index 0 to the last index.");
            LogEvent("Traverse", traverseText, "Success", "Array/list values traversed from index 0 to the last index");

            RefreshStatusAndProgress();
        }

        // ---------------------------------------------------------------
        // Reset
        // ---------------------------------------------------------------

        public void OnResetClicked()
        {
            if (isBusy || arrayModule == null) return;

            if (arrayModule.isTraversing)
            {
                SetOperationExplanation(
                    "Please wait.",
                    "A traversal is still running.");
                return;
            }

            arrayModule.ResetArray();

            if (valueInput != null) valueInput.text = "";

            SetOperationExplanation(
                "Array/list reset successful.",
                "The array/list returned to its default values.");
            LogEvent("Reset", "", "Success", "Array/List reset to default values");

            RefreshStatusAndProgress();
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
        /// Status shows only the first and last value/index, not every element.
        /// The first entry is stable (index 0 never changes for an append-only
        /// array/list), so in practice only the "last value" half of the message
        /// updates as values are inserted or deleted — keeping the line a fixed,
        /// short length regardless of how many values are currently stored,
        /// instead of growing with every insert.
        /// </summary>
        private void RefreshStatusAndProgress()
        {
            if (arrayModule == null) return;

            List<int> values = arrayModule.values;

            if (values.Count == 0)
            {
                DashboardController.Instance?.SetStatus("The array/list is empty.");
            }
            else if (values.Count == 1)
            {
                DashboardController.Instance?.SetStatus($"Value {values[0]} is at index 0.");
            }
            else
            {
                int lastIndex = values.Count - 1;
                DashboardController.Instance?.SetStatus(
                    $"Value {values[0]} is at index 0 | Value {values[lastIndex]} is at index {lastIndex}.");
            }

            DashboardController.Instance?.SetProgress(
                $"{values.Count}/{MaxArrayValues} values stored.");

            RebuildDashboardLayout();
        }

        /// <summary>
        /// Forces the dashboard's Vertical Layout Group / Content Size Fitter to
        /// recompute immediately after a panel's text changes, rather than relying
        /// on it happening to line up with Unity's next automatic layout pass.
        /// Same pattern as DashboardContainer.Start()'s first-frame fix (Migration
        /// Spec §5) — just re-applied every time this controller changes dashboard
        /// content, not only once at scene load. See porting note 5 above.
        /// </summary>
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

        private static string FormatValuesWithArrows(List<int> values)
        {
            return string.Join(" -> ", values);
        }
    }
}
