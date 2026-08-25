using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MartProg.Modules
{
    /// <summary>
    /// Phase 24: the Loops module's UI controller, ported from
    /// ar_learning_screen.gd's _handle_loops_*_button() handlers,
    /// parse_loop_values_from_input(), handle_loop_set_values(),
    /// update_loop_dashboard(), set_loop_dashboard_message() and
    /// update_loop_status_label() — all read directly from source.
    ///
    /// Porting decisions and preserved/adapted source quirks:
    ///
    /// 1. **Loops has only FOUR action buttons, not five.** Every module
    ///    before it used all five of Godot's shared buttons; Loops
    ///    explicitly sets traverse_button.visible = false and uses:
    ///        insert -> Set Values
    ///        delete -> Step
    ///        search -> Run Loop
    ///        reset  -> Reset
    ///    So this controller has no traverse equivalent at all, and the
    ///    control strip carries five buttons total including Quiz rather
    ///    than the usual six. CountAssignedButtons() reflects that.
    ///
    /// 2. **A REAL BUG IS FIXED, NOT PORTED: Set Values reports success
    ///    even when the value list was rejected.** This is the deviation
    ///    this phase most needs recorded.
    ///
    ///    Source's _handle_loops_insert_button() does:
    ///        if has_method("handle_loop_set_values"):
    ///            await self.call("handle_loop_set_values")
    ///            visual_updated = true
    ///    — it sets visual_updated unconditionally, never checking what the
    ///    call actually did. handle_loop_set_values() calls
    ///    loop_module.set_values_from_text(), which returns FALSE for more
    ///    than six values, and on failure shows its own feedback and
    ///    returns. Control then falls back to the outer handler, which
    ///    proceeds as if everything worked: it stores the parsed values,
    ///    writes "Operation: Loop values were set to 1 -> 2 -> ... -> 7."
    ///    to the dashboard, and logs the event as **Success** — while the
    ///    visual still shows the old values.
    ///
    ///    Compounding it, parse_loop_values_from_input() does NOT enforce
    ///    the six-value cap (only set_values_from_text does), so the outer
    ///    handler has no way to know the input was over the limit. And
    ///    because both functions log, a successful Set Values writes TWO
    ///    "Loop Set Values" entries to the research log, while an
    ///    over-limit one writes a "Failed" entry immediately followed by a
    ///    contradictory "Success" entry.
    ///
    ///    Porting this literally would give students a dashboard that lies
    ///    about what happened and a research log that contradicts itself —
    ///    squarely the "genuine bug worth correcting rather than
    ///    preserving" case, same as Phase 21's Linked List Reset. So this
    ///    controller: checks the six-value cap itself before calling the
    ///    module, reports a specific failure message when it trips, uses
    ///    the module's actual return value to decide success, and logs
    ///    exactly once per action.
    ///
    /// 3. **DELIBERATE DEVIATION: the Status panel is populated here, where
    ///    Godot leaves it permanently blank.**
    ///
    ///    In source, update_loop_dashboard() ends by hiding and blanking
    ///    index_explanation_label (the label this project maps to the Status
    ///    panel), and set_loop_dashboard_message() calls it. Reset and
    ///    handle_loop_set_values() DO call update_loop_status_label() to
    ///    write "Loop status: current index = N | running total = X" — but
    ///    both call it BEFORE the dashboard message, so the write is
    ///    immediately overwritten. Net effect in Godot: the Loops status
    ///    line is never visible. Note the contrast with Functions, whose
    ///    handlers call set_functions_dashboard_message() FIRST and
    ///    update_function_status_label() SECOND — so its status line
    ///    survives. The two modules differ only in call order, which is
    ///    what makes this look like an accident rather than a decision.
    ///
    ///    This was initially ported faithfully (Status left blank) and
    ///    reported during Phase 24 bring-up as "the status does not update".
    ///    Corrected on the user's instruction: SetStatusLine() is now called
    ///    after every operation, not just the two source attempts it. The
    ///    running index and total are exactly what a student stepping
    ///    through a loop needs, so leaving the panel blank — or refreshing
    ///    it only on Set Values and Reset, where it would go stale during
    ///    the stepping the module exists to demonstrate — would both be
    ///    worse than the source behaviour rather than more faithful to it.
    ///
    ///    Source's own format is otherwise preserved verbatim; the one
    ///    addition is a "finished" state, documented on SetStatusLine().
    ///
    /// 4. **Reset's dashboard value list disagrees with the module's actual
    ///    reset values.** Source's _handle_loops_reset_button() sets
    ///    loop_dashboard_values = [5, 10, 15] while loop_module.reset_loop()
    ///    sets values = [10, 20, 30]. Since that list is only ever read to
    ///    build Set Values' confirmation sentence, the mismatch never
    ///    surfaces — but it is wrong. This port keeps no parallel list at
    ///    all and reads the module's real values when it needs them, which
    ///    removes the possibility of the two disagreeing.
    ///
    /// 5. **Commented-out code in _handle_loops_reset_button() is not
    ///    ported** — a whole has_method("handle_loop_reset") fallback block
    ///    sits commented out in source. The live path simply calls
    ///    loop_module.reset_loop() directly.
    ///
    /// 6. **Step's success message is unconditional in source**, including
    ///    when the loop has already finished — step_loop() returns null
    ///    without advancing, yet the handler still says "The loop moved one
    ///    step forward." Preserved: this is cosmetic, self-correcting (the
    ///    LOOP block itself reads "done"), and unlike note 2 it does not
    ///    corrupt the log or contradict what the student sees.
    ///
    /// 7. **The Progress panel is Unity-only** (Migration Spec §8f). Loops
    ///    shows iteration progress plus the running total.
    /// </summary>
    public class LoopsController : MonoBehaviour, IModuleController
    {
        [Header("Module Reference")]
        [Tooltip("Auto-found via FindFirstObjectByType if left empty.")]
        [SerializeField] private LoopModule loopModule;

        [Header("UI References")]
        [Tooltip("Source placeholder equivalent: values separated by commas, e.g. 5,10,15. Maximum six.")]
        [SerializeField] private TMP_InputField valueInput;
        [Tooltip("Godot's insert_button, relabelled \"Set Values\".")]
        [SerializeField] private Button setValuesButton;
        [Tooltip("Godot's delete_button, relabelled \"Step\".")]
        [SerializeField] private Button stepButton;
        [Tooltip("Godot's search_button, relabelled \"Run Loop\".")]
        [SerializeField] private Button runLoopButton;
        [SerializeField] private Button resetButton;
        [Tooltip("The Quiz trigger button in THIS module's control strip. Assign the same Button " +
                 "GameObject here as on QuizController's Loops quiz-button field — that script " +
                 "wires the click; this field only feeds CountAssignedButtons() below. NOTE: Loops " +
                 "has no Traverse button (see class comment note 1), so this strip has FIVE " +
                 "buttons, not six.")]
        [SerializeField] private Button quizButton;

        [Header("Layout (optional)")]
        [SerializeField] private GridLayoutGroup buttonGrid;
        [Tooltip("The dashboard's scrollable 'Content' RectTransform.")]
        [SerializeField] private RectTransform dashboardContent;

        private bool isBusy;

        private void Start()
        {
            if (loopModule == null)
            {
                loopModule = FindFirstObjectByType<LoopModule>();
            }

            if (loopModule == null)
            {
                Debug.LogError("LoopsController: no LoopModule found in the scene.");
            }

            if (ResearchLogger.Instance != null)
            {
                ResearchLogger.Instance.ModuleName = "Loops";
            }

            WireButtons();
            StartCoroutine(InitializeDeferred());
        }

        private IEnumerator InitializeDeferred()
        {
            yield return null;

            EnterModule();

            ApplyResponsiveButtonLayout();
        }

        /// <summary>
        /// IModuleController.EnterModule(). Mirrors Godot's
        /// reset_dashboard_state_for_module_start("Loops") +
        /// update_loop_dashboard() pair, including resetting the module.
        /// </summary>
        public void EnterModule()
        {
            // Phase 29 (F1) backstop: if this controller's GameObject was
            // disabled mid-animation but the module's was not, the module's
            // own OnDisable() never fired and isAnimating is still true —
            // which would make the Reset*() call below silently no-op and
            // leave the module permanently unusable. Clear it first.
            if (loopModule != null) loopModule.ForceStopAnimation();

            SetBusy(false);

            if (loopModule != null) loopModule.ResetLoop();
            if (valueInput != null) valueInput.text = "";

            // Phase 26b dashboard audit — see DashboardController.cs.
            DashboardController.Instance?.SetSummary(
                "A loop repeats a block of code. It can process each value in a list " +
                "one value at a time.");
            DashboardController.Instance?.SetInstruction(
                "Enter values like 5,10,15, then choose Set Values, Step, Run Loop, Reset, or Quiz.");
            DashboardController.Instance?.SetOperation("No operation selected yet.");
            DashboardController.Instance?.SetExplanation(
                "A loop repeats instructions until its task is complete. " +
                "In this module, the loop visits the values one after another and updates the running total.");
            RebuildDashboardLayout();

            // Status is populated rather than left blank — see class comment
            // note 3. On entry this reads "current index = not started".
            RefreshProgress();
            SetStatusLine();
        
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

            if (valueInput != null) valueInput.onSubmit.AddListener(_ => OnSetValuesClicked());

            if (setValuesButton != null) setValuesButton.onClick.AddListener(OnSetValuesClicked);
            if (stepButton != null) stepButton.onClick.AddListener(OnStepClicked);
            if (runLoopButton != null) runLoopButton.onClick.AddListener(OnRunLoopClicked);
            if (resetButton != null) resetButton.onClick.AddListener(OnResetClicked);
        }

        private void ApplyResponsiveButtonLayout()
        {
            if (buttonGrid == null) return;

            int columns = 2;

            if (ResponsiveManager.Instance != null)
            {
                columns = Mathf.Clamp(ResponsiveManager.Instance.GetButtonColumns(), 1, 5);
            }

            int buttonCount = CountAssignedButtons();
            int rows = columns > 0 ? Mathf.CeilToInt(buttonCount / (float)columns) : 1;

            Debug.Log($"LoopsController: applying a {columns}-column / {rows}-row button layout " +
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

        /// Five buttons, not six — Loops has no Traverse (note 1).
        private int CountAssignedButtons()
        {
            int count = 0;
            if (setValuesButton != null) count++;
            if (stepButton != null) count++;
            if (runLoopButton != null) count++;
            if (resetButton != null) count++;
            if (quizButton != null) count++;
            return count;
        }

        // ---------------------------------------------------------------
        // Set Values — Godot: _handle_loops_insert_button() +
        // parse_loop_values_from_input() + handle_loop_set_values().
        // See class comment note 2 for the bug corrected here.
        // ---------------------------------------------------------------

        public void OnSetValuesClicked()
        {
            if (isBusy || loopModule == null) return;
            HandleSetValues();
        }

        private void HandleSetValues()
        {
            string typedValue = valueInput != null ? valueInput.text.Trim() : "";

            if (string.IsNullOrEmpty(typedValue))
            {
                SetOperationExplanation(
                    "Set Values failed. No values were entered.",
                    "Enter values separated by commas before clicking Set Values. For example, enter 5,10,15.");
                LogEvent("Loop Set Values", "", "Failed", "Invalid loop input");
                return;
            }

            string[] parts = typedValue.Split(',');
            List<int> parsedValues = new List<int>();

            foreach (string part in parts)
            {
                string cleaned = part.Trim();

                if (string.IsNullOrEmpty(cleaned))
                {
                    SetOperationExplanation(
                        "Set Values failed. One value is missing.",
                        "Check the comma-separated list and make sure every position contains a number. " +
                        "For example, use 5,10,15 instead of 5,,15.");
                    LogEvent("Loop Set Values", typedValue, "Failed", "Invalid loop input");
                    return;
                }

                if (!int.TryParse(cleaned, out int parsed))
                {
                    SetOperationExplanation(
                        "Set Values failed. The input contains a non-numeric value.",
                        "The Loops module currently accepts numeric values only. Enter values such as 5,10,15.");
                    LogEvent("Loop Set Values", typedValue, "Failed", "Invalid loop input");
                    return;
                }

                parsedValues.Add(parsed);
            }

            // Deviation from source — see class comment note 2. Godot's parse
            // step never checks the cap, so the outer handler reported success
            // for over-limit input while the visual silently kept the old
            // values. Checked here, before the module is touched.
            if (parsedValues.Count > loopModule.MaxValueCount)
            {
                SetOperationExplanation(
                    $"Set Values failed. {parsedValues.Count} values were entered, " +
                    $"but this module accepts at most {loopModule.MaxValueCount}.",
                    $"Enter up to {loopModule.MaxValueCount} whole numbers separated by commas, " +
                    "for example 5,10,15.");
                LogEvent("Loop Set Values", typedValue, "Failed",
                    $"Over the {loopModule.MaxValueCount}-value limit");
                return;
            }

            // Use the module's actual return value rather than assuming success.
            bool applied = loopModule.SetValuesFromText(typedValue);

            if (!applied)
            {
                SetOperationExplanation(
                    "Set Values failed. The loop values could not be updated.",
                    "The loop values could not be set because the input was not valid.");
                LogEvent("Loop Set Values", typedValue, "Failed", "Module rejected the values");
                return;
            }

            SetOperationExplanation(
                $"Loop values were set to {FormatValuesWithArrows(loopModule.Values)}.",
                "Set Values prepares the list of values that the loop will process. " +
                "The loop can now step through the values or run to completion.");

            RefreshProgress();
            SetStatusLine();

            // Logged exactly once — source logs twice on this path (note 2).
            LogEvent("Loop Set Values", typedValue, "Success", "Loop values updated");
        }

        // ---------------------------------------------------------------
        // Step — Godot: _handle_loops_delete_button()
        // ---------------------------------------------------------------

        public void OnStepClicked()
        {
            if (isBusy || loopModule == null) return;
            StartCoroutine(RunStep());
        }

        private IEnumerator RunStep()
        {
            SetBusy(true);

            yield return loopModule.StepLoop();

            // Unconditional success message, matching source — see note 6.
            SetOperationExplanation(
                "The loop moved one step forward.",
                "Step executes one loop iteration. The loop visits the current value, " +
                "updates the running total, and prepares to move to the next value.");

            RefreshProgress();
            SetStatusLine();

            LogEvent("Loop Step", "", "Success", "Loop advanced by one step");

            SetBusy(false);
        }

        // ---------------------------------------------------------------
        // Run Loop — Godot: _handle_loops_search_button()
        // ---------------------------------------------------------------

        public void OnRunLoopClicked()
        {
            if (isBusy || loopModule == null) return;
            StartCoroutine(RunWholeLoop());
        }

        private IEnumerator RunWholeLoop()
        {
            SetBusy(true);

            yield return loopModule.RunLoop();

            SetOperationExplanation(
                "The loop ran to completion.",
                "Run Loop executes the remaining iterations automatically until all values " +
                "have been visited and the final total has been produced.");

            RefreshProgress();
            SetStatusLine();

            LogEvent("Loop Run", "", "Success", "Loop ran to completion");

            SetBusy(false);
        }

        // ---------------------------------------------------------------
        // Reset — Godot: _handle_loops_reset_button()
        // ---------------------------------------------------------------

        public void OnResetClicked()
        {
            if (isBusy || loopModule == null) return;

            loopModule.ResetLoop();

            if (valueInput != null) valueInput.text = "";

            SetOperationExplanation(
                "Loops module reset successful.",
                "Reset returns the loop to its starting state. You can enter values again, " +
                "step through them, or run the loop from the beginning.");

            RefreshProgress();
            SetStatusLine();

            LogEvent("Loop Reset", "", "Success", "Loops module reset");
        }

        // ---------------------------------------------------------------
        // Shared helpers
        // ---------------------------------------------------------------

        private void SetBusy(bool busy)
        {
            isBusy = busy;

            if (setValuesButton != null) setValuesButton.interactable = !busy;
            if (stepButton != null) stepButton.interactable = !busy;
            if (runLoopButton != null) runLoopButton.interactable = !busy;
            if (resetButton != null) resetButton.interactable = !busy;
        }

        private void SetOperationExplanation(string operation, string explanation)
        {
            DashboardController.Instance?.SetOperation(operation);
            DashboardController.Instance?.SetExplanation(explanation);
            RebuildDashboardLayout();
        }

        /// <summary>
        /// update_loop_status_label()'s format, now actually displayed — see
        /// class comment note 3 for why it was blank in Godot and why that
        /// is not preserved.
        ///
        /// One extension to source's format: after RunLoop() finishes,
        /// currentIndex is deliberately -1 (that is how the CURRENT marker
        /// is removed), so source's own rule would print "not started" for a
        /// loop that just completed — actively wrong. LoopModule.VisitedCount
        /// distinguishes the two, so a finished loop reads "finished".
        /// </summary>
        private void SetStatusLine()
        {
            if (loopModule == null) return;

            string indexText;

            if (loopModule.CurrentIndex >= 0)
            {
                indexText = loopModule.CurrentIndex.ToString();
            }
            else if (loopModule.VisitedCount > 0)
            {
                indexText = "finished";
            }
            else
            {
                indexText = "not started";
            }

            DashboardController.Instance?.SetStatus(
                $"Loop status: current index = {indexText} | running total = {loopModule.RunningTotal}");

            RebuildDashboardLayout();
        }

        /// <summary>
        /// Progress panel only — Unity-only, see note 7.
        ///
        /// Reads VisitedCount, NOT CurrentIndex + 1. RunLoop() clears
        /// currentIndex to -1 on completion to remove the CURRENT marker, so
        /// deriving progress from it reported "0/3 values visited" straight
        /// after a completed run — found during Phase 24 bring-up.
        /// </summary>
        private void RefreshProgress()
        {
            if (loopModule == null) return;

            DashboardController.Instance?.SetProgress(
                $"{loopModule.VisitedCount}/{loopModule.Values.Count} values visited. " +
                $"Running total: {loopModule.RunningTotal}");

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

        // Godot: func format_loop_values_with_arrows(values: Array) -> String
        private static string FormatValuesWithArrows(IReadOnlyList<int> values)
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
