using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MartProg.Modules
{
    /// <summary>
    /// Phase 23: the Functions module's UI controller, ported from
    /// ar_learning_screen.gd's _handle_functions_*_button() handlers,
    /// update_functions_dashboard(), set_functions_dashboard_message() and
    /// update_function_status_label() — all read directly from source for
    /// this phase.
    ///
    /// Same shape as every other module controller: owns its control strip's
    /// buttons, drives DashboardController's panels, logs through
    /// ResearchLogger, and implements IModuleController.
    ///
    /// Porting decisions and preserved/adapted source quirks:
    ///
    /// 1. **Godot's five shared buttons are five DIFFERENT operations here,
    ///    and the button names reflect that.** ar_learning_screen.gd has one
    ///    shared button row that it relabels per module — for Functions it
    ///    sets insert_button.text = "Add", delete_button.text = "Subtract",
    ///    search_button.text = "Multiply", traverse_button.text = "Set
    ///    Input", reset_button.text = "Reset", and the handlers are still
    ///    named _handle_functions_insert_button() etc. Unity gives each
    ///    module its own control strip (established in Phase 18 and unchanged
    ///    since), so there is no relabeling to port: the buttons below are
    ///    simply named addButton/subtractButton/multiplyButton/
    ///    setInputButton/resetButton. The mapping from source is:
    ///        insert   -> Add       (call_function("add"))
    ///        delete   -> Subtract  (call_function("subtract"))
    ///        search   -> Multiply  (call_function("multiply"))
    ///        traverse -> Set Input (parse "a,b", set parameters only)
    ///        reset    -> Reset
    ///    Recorded explicitly because the source handler NAMES are actively
    ///    misleading — "_handle_functions_traverse_button" does not traverse
    ///    anything, it sets input values.
    ///
    /// 2. **Set Input takes TWO comma-separated values, unlike every other
    ///    module's single-value input.** Source's placeholder is "Enter two
    ///    values, e.g. 15,25" and the handler splits on ",", requiring
    ///    exactly two parts, each a valid integer. All four of its distinct
    ///    failure branches are ported verbatim with their own messages and
    ///    their own log entries: empty input, wrong number of parts, a
    ///    non-integer part, and a missing module reference.
    ///
    /// 3. **Set Input updates only the inputs, never the output** — see
    ///    FunctionModule.cs porting note 4. The explanation text says so to
    ///    the student, and is ported word-for-word.
    ///
    /// 4. **Source's log action names are inconsistent, and that is
    ///    preserved.** Add/Subtract/Multiply/Set Input log as "Function Add",
    ///    "Function Subtract", "Function Multiply" and "Function Set Input"
    ///    — all module-prefixed. Reset logs as plain "Reset", with no
    ///    prefix, unlike the other four and unlike Linked List's own
    ///    "Linked List Insert" style. Kept exactly as source has it: the
    ///    research log is analysed against Godot's output, so normalising the
    ///    name would silently break comparability. Same call made for
    ///    Stack's "Stack Pop"-vs-"Pop" asymmetry in Phase 18.
    ///
    /// 5. **The Status panel carries Godot's function status line.**
    ///    update_function_status_label() writes to index_explanation_label —
    ///    the same label this project maps to the Status panel — in the form
    ///    "Function status: add(a, b) | a = 10 | b = 20 | return value = 30".
    ///    Ported verbatim, and called after Add/Subtract/Multiply/Reset
    ///    exactly where source calls it. Note source does NOT call it after
    ///    Set Input, so neither does this — the status line keeps showing the
    ///    previous call's values until a function is run, which is consistent
    ///    with Set Input deliberately not updating the output either.
    ///
    /// 6. **Status starts blank on entry**, matching every module since
    ///    Stack: update_functions_dashboard() explicitly sets
    ///    index_explanation_label.visible = false and blanks its text when
    ///    the module is entered, and only the status-label call above ever
    ///    populates it.
    ///
    /// 7. **The Progress panel is Unity-only** (DashboardController's sixth
    ///    panel has no Godot equivalent — see Migration Spec §8f). For
    ///    Functions there is no count to report, so it shows the current
    ///    function and its result instead, refreshed after every operation.
    ///
    /// 8. **No busy-guard is needed around Set Input or Reset in source, but
    ///    one is applied anyway.** FunctionModule.SetParameters() and
    ///    ResetFunction() both self-guard on is_animating exactly as source
    ///    does, so the behaviour matches either way; SetBusy() additionally
    ///    greys the buttons out during the call animation, the same
    ///    affordance every other module controller provides.
    /// </summary>
    public class FunctionsController : MonoBehaviour, IModuleController
    {
        [Header("Module Reference")]
        [Tooltip("Auto-found via FindFirstObjectByType if left empty.")]
        [SerializeField] private FunctionModule functionModule;

        [Header("UI References")]
        [Tooltip("Source placeholder: \"Enter two values, e.g. 15,25\" — this module takes TWO comma-separated numbers, unlike every other module's single value.")]
        [SerializeField] private TMP_InputField valueInput;
        [Tooltip("Godot's insert_button, relabelled \"Add\" for this module — see porting note 1.")]
        [SerializeField] private Button addButton;
        [Tooltip("Godot's delete_button, relabelled \"Subtract\".")]
        [SerializeField] private Button subtractButton;
        [Tooltip("Godot's search_button, relabelled \"Multiply\".")]
        [SerializeField] private Button multiplyButton;
        [Tooltip("Godot's traverse_button, relabelled \"Set Input\". Despite the source handler name, this does NOT traverse anything.")]
        [SerializeField] private Button setInputButton;
        [SerializeField] private Button resetButton;
        [Tooltip("The Quiz trigger button living in THIS module's own control strip, " +
                 "alongside the others (not the admin row). Assign the same Button " +
                 "GameObject here as on QuizController's Functions quiz-button field — " +
                 "that script wires the click behavior; this field only makes sure this " +
                 "module's button-grid column/row/cell-size math (CountAssignedButtons() " +
                 "below) accounts for it.")]
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

        private bool isBusy;

        private void Start()
        {
            if (functionModule == null)
            {
                functionModule = FindFirstObjectByType<FunctionModule>();
            }

            if (functionModule == null)
            {
                Debug.LogError("FunctionsController: no FunctionModule found in the scene.");
            }

            if (ResearchLogger.Instance != null)
            {
                ResearchLogger.Instance.ModuleName = "Functions";
            }

            WireButtons();
            StartCoroutine(InitializeDeferred());
        }

        /// Same one-frame defer as every other module controller — see
        /// Migration Spec §5.
        private IEnumerator InitializeDeferred()
        {
            yield return null;

            EnterModule();

            ApplyResponsiveButtonLayout();
        }

        /// <summary>
        /// IModuleController.EnterModule(). Mirrors Godot's
        /// reset_dashboard_state_for_module_start("Functions") +
        /// update_functions_dashboard() pair — including resetting the
        /// underlying module, which source does in that same block.
        /// </summary>
        public void EnterModule()
        {
            SetBusy(false);

            if (functionModule != null) functionModule.ResetFunction();
            if (valueInput != null) valueInput.text = "";

            DashboardController.Instance?.SetSummary("Functions Module");
            DashboardController.Instance?.SetInstruction(
                "Enter two values like 15,25, then choose Set Input, Add, Subtract, Multiply, Reset, or Quiz.");
            DashboardController.Instance?.SetOperation("No operation selected yet.");
            DashboardController.Instance?.SetExplanation(
                "A function receives values as parameters, performs an operation, " +
                "and returns a result. For example, add(a, b) returns the sum of a and b.");
            // Blank on entry — see porting note 6.
            DashboardController.Instance?.SetStatus("");
            RebuildDashboardLayout();

            RefreshProgress();
        }

        private void WireButtons()
        {
            if (addButton != null) addButton.onClick.AddListener(OnAddClicked);
            if (subtractButton != null) subtractButton.onClick.AddListener(OnSubtractClicked);
            if (multiplyButton != null) multiplyButton.onClick.AddListener(OnMultiplyClicked);
            if (setInputButton != null) setInputButton.onClick.AddListener(OnSetInputClicked);
            if (resetButton != null) resetButton.onClick.AddListener(OnResetClicked);
        }

        /// Same responsive grid pattern as every other module controller —
        /// see Migration Spec §5.
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

            Debug.Log($"FunctionsController: applying a {columns}-column / {rows}-row button layout " +
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
            if (addButton != null) count++;
            if (subtractButton != null) count++;
            if (multiplyButton != null) count++;
            if (setInputButton != null) count++;
            if (resetButton != null) count++;
            if (quizButton != null) count++;
            return count;
        }

        // ---------------------------------------------------------------
        // Add / Subtract / Multiply — Godot: _handle_functions_insert_button(),
        // _handle_functions_delete_button(), _handle_functions_search_button().
        // Identical bodies apart from the function name, the verb in the two
        // dashboard sentences, and the log action — so they share one
        // coroutine rather than being copy-pasted three times.
        // ---------------------------------------------------------------

        public void OnAddClicked()
        {
            if (isBusy || functionModule == null) return;
            StartCoroutine(RunFunction("add", "Add", "returned their sum", "Function Add"));
        }

        public void OnSubtractClicked()
        {
            if (isBusy || functionModule == null) return;
            StartCoroutine(RunFunction("subtract", "Subtract", "returned their output", "Function Subtract"));
        }

        public void OnMultiplyClicked()
        {
            if (isBusy || functionModule == null) return;
            StartCoroutine(RunFunction("multiply", "Multiply", "returned their output", "Function Multiply"));
        }

        /// <summary>
        /// Note the explanation wording is NOT uniform in source: Add says
        /// "returned their sum" while Subtract and Multiply both say
        /// "returned their output". Preserved rather than tidied into three
        /// matching sentences.
        /// </summary>
        private IEnumerator RunFunction(string functionName, string displayVerb, string explanationTail, string logAction)
        {
            SetBusy(true);

            int resultValue = 0;
            yield return functionModule.CallFunction(functionName, r => resultValue = r);

            int a = functionModule.ParameterA;
            int b = functionModule.ParameterB;

            SetOperationExplanation(
                $"{displayVerb} function executed using a = {a} and b = {b}. Result = {resultValue}.",
                $"The {functionName}(a, b) function received two parameters and {explanationTail}.");

            UpdateFunctionStatusLabel();
            RefreshProgress();

            LogEvent(logAction, $"{a},{b}", "Success", $"Result = {resultValue}");

            SetBusy(false);
        }

        // ---------------------------------------------------------------
        // Set Input — Godot: _handle_functions_traverse_button().
        // Despite that name it does not traverse anything; it parses two
        // comma-separated values and updates the input boxes only.
        // ---------------------------------------------------------------

        public void OnSetInputClicked()
        {
            if (isBusy) return;
            HandleSetInput();
        }

        private void HandleSetInput()
        {
            string typedValue = valueInput != null ? valueInput.text.Trim() : "";

            if (string.IsNullOrEmpty(typedValue))
            {
                SetOperationExplanation(
                    "Set Input failed. No values were entered.",
                    "Enter two numbers separated by a comma before clicking Set Input. For example, enter 15,25.");
                LogEvent("Function Set Input", "", "Failed", "No input values entered");
                return;
            }

            string[] parts = typedValue.Split(',');

            if (parts.Length != 2)
            {
                SetOperationExplanation(
                    "Set Input failed. Invalid input format.",
                    "Enter exactly two numbers separated by a comma. For example, enter 15,25.");
                LogEvent("Function Set Input", typedValue, "Failed", "Invalid input format");
                return;
            }

            string firstValueText = parts[0].Trim();
            string secondValueText = parts[1].Trim();

            if (!int.TryParse(firstValueText, out int a) || !int.TryParse(secondValueText, out int b))
            {
                SetOperationExplanation(
                    "Set Input failed. One or both values are not valid numbers.",
                    "The Functions module currently accepts numeric inputs only. Enter values such as 15,25.");
                LogEvent("Function Set Input", typedValue, "Failed", "Invalid numeric input");
                return;
            }

            if (functionModule == null)
            {
                SetOperationExplanation(
                    "Set Input failed. Function visual module is missing.",
                    "The function visual module could not be found, so the input values could not be displayed.");
                LogEvent("Function Set Input", typedValue, "Failed", "Function module missing");
                return;
            }

            functionModule.SetInputValues(a, b);

            SetOperationExplanation(
                $"Input values set to a = {a} and b = {b}.",
                "Set Input updates only the function parameters. The return/output value will be " +
                "updated after you choose Add, Subtract, or Multiply.");

            RefreshProgress();

            LogEvent("Function Set Input", $"{a},{b}", "Success", "Function input values updated visually");
        }

        // ---------------------------------------------------------------
        // Reset — Godot: _handle_functions_reset_button()
        // ---------------------------------------------------------------

        public void OnResetClicked()
        {
            if (isBusy || functionModule == null) return;

            functionModule.ResetFunction();

            if (valueInput != null) valueInput.text = "";

            UpdateFunctionStatusLabel();

            SetOperationExplanation(
                "Function inputs were reset successfully.",
                "Input values and calculated outputs returned to their default state.");

            RefreshProgress();

            // Logged as plain "Reset", with no module prefix — source's own
            // inconsistency, preserved. See porting note 4.
            LogEvent("Reset", "", "Success", "Functions module reset to default values");
        }

        // ---------------------------------------------------------------
        // Shared helpers
        // ---------------------------------------------------------------

        private void SetBusy(bool busy)
        {
            isBusy = busy;

            if (addButton != null) addButton.interactable = !busy;
            if (subtractButton != null) subtractButton.interactable = !busy;
            if (multiplyButton != null) multiplyButton.interactable = !busy;
            if (setInputButton != null) setInputButton.interactable = !busy;
            if (resetButton != null) resetButton.interactable = !busy;
        }

        private void SetOperationExplanation(string operation, string explanation)
        {
            DashboardController.Instance?.SetOperation(operation);
            DashboardController.Instance?.SetExplanation(explanation);
            RebuildDashboardLayout();
        }

        /// <summary>
        /// Direct port of update_function_status_label(). Source writes this
        /// to index_explanation_label, which this project maps to the Status
        /// panel. Deliberately NOT called after Set Input — see porting
        /// note 5.
        /// </summary>
        private void UpdateFunctionStatusLabel()
        {
            if (functionModule == null) return;

            DashboardController.Instance?.SetStatus(
                $"Function status: {functionModule.CurrentFunctionName}(a, b)" +
                $" | a = {functionModule.ParameterA}" +
                $" | b = {functionModule.ParameterB}" +
                $" | return value = {functionModule.ResultValue}");

            RebuildDashboardLayout();
        }

        /// <summary>
        /// Progress panel only — Unity-only, see porting note 7. Functions
        /// has no item count to report, so it shows the active function and
        /// its current result instead.
        /// </summary>
        private void RefreshProgress()
        {
            if (functionModule == null) return;

            DashboardController.Instance?.SetProgress(
                $"{functionModule.CurrentFunctionName}({functionModule.ParameterA}, {functionModule.ParameterB}) = {functionModule.ResultValue}");

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
    }
}
