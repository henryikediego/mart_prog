using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MartProg.Modules
{
    /// <summary>
    /// Phase 25: the Selection module's UI controller — **Basic If mode
    /// only**. Ported from ar_learning_screen.gd's
    /// `_handle_selection_*_button()` handlers, `parse_basic_condition()`,
    /// `evaluate_condition()`, `parse_single_integer_input()`,
    /// `sync_selection_visual_for_current_mode()`,
    /// `update_selection_dashboard()`, `reset_selection_mode_state()` and
    /// `_on_selection_mode_option_item_selected()`.
    ///
    /// **Scope: Phase 25 delivers the visual, the mode dropdown, and Basic
    /// If.** Else-if Ladder and Nested If are Phase 25b. The dropdown is
    /// built and wired now (all three modes listed) so that adding them
    /// later is additive rather than structural — selecting one of the two
    /// unbuilt modes reports that it is not available yet, in the same
    /// spirit as ChangeModuleController's `IsBuilt` gate. The mode-switch
    /// plumbing, per-mode button relabelling and per-mode dashboard text are
    /// all already in place for three modes.
    ///
    /// Porting decisions and preserved/adapted source quirks:
    ///
    /// 1. **The logic lives here, not in SelectionModule — because that is
    ///    where the LIVE Godot implementation lives.** See SelectionModule.cs
    ///    porting note 1 for the full finding: selection_module.gd's own
    ///    state machine (value/threshold, six rules, its own evaluators,
    ///    check_selection(), toggle_rule(), set_condition_from_text()) is
    ///    dead code, and ar_learning_screen.gd carries a complete parallel
    ///    implementation that drives the visual through a single
    ///    set_selection_visual_texts() call. This controller ports that
    ///    parallel implementation. Its state — selectionBasicValue = 75,
    ///    selectionBasicConditionText = ">= 50", and a FOUR-entry rule list
    ///    — comes from the screen script, not from the module script (whose
    ///    equivalents are 60, ">= 50", and six rules).
    ///
    /// 2. **Selection is the first module with a mode selector.** Godot uses
    ///    an OptionButton (`selection_mode_option`) whose item TEXT is the
    ///    mode identity: "Basic If", "Else-if Ladder", "Nested If" — the
    ///    handler literally does
    ///    `selection_current_mode = selection_mode_option.get_item_text(index)`.
    ///    Unity's TMP_Dropdown is the direct equivalent and is what
    ///    ChangeModuleController already uses, so the same approach is taken
    ///    here; the mode strings are kept identical to source because they
    ///    are compared by value throughout.
    ///
    /// 3. **All five action buttons are used, and three of them are
    ///    relabelled per mode.** Unlike Loops (four buttons) this module
    ///    uses all five, but the labels change with the mode:
    ///        insert   -> "Set Value"  (Basic) / "Set Score" / "Set Values"
    ///        delete   -> "Set Condition" (Basic) / "Rule Info" (both others)
    ///        search   -> "Check"      (all modes)
    ///        traverse -> "Toggle Rule" (all modes)
    ///        reset    -> "Reset"      (all modes)
    ///    Because Unity gives each module its own control strip, the buttons
    ///    are named for their Basic-mode role and relabelled at runtime when
    ///    the mode changes — the one place this project does runtime
    ///    relabelling, and only because the mode genuinely changes what a
    ///    button does.
    ///
    /// 4. **The input box is dual-purpose in Basic mode.** Its placeholder is
    ///    "Value: 75 or condition: >= 50" — the same field feeds Set Value
    ///    (parsed as an integer) and Set Condition (parsed as an operator
    ///    plus a threshold). Ported as-is.
    ///
    /// 5. **parse_basic_condition() accepts an optional "value" prefix** —
    ///    both ">= 50" and "value >= 50" parse, via a `begins_with("value")`
    ///    strip. Note it strips exactly 5 characters, so "values >= 50"
    ///    would leave a stray "s". Ported faithfully, quirk included; the
    ///    operator list is ordered [">=", "<=", "==", "!=", ">", "<"] so the
    ///    two-character operators match before their one-character prefixes.
    ///
    /// 6. **Toggle Rule cycles a FOUR-entry list**, not the module script's
    ///    six comparison rules: ">= 50", "< 50", "== 0", "!= 0". It wraps at
    ///    the end. Source also disables Toggle Rule outside Basic mode, with
    ///    the tooltip "Toggle Rule is available only in Basic If mode" —
    ///    that gating is ported and will matter in Phase 25b.
    ///
    /// 7. **Result is "pending" until Check is pressed.** Set Value, Set
    ///    Condition and Toggle Rule all push "?" into the RESULT box rather
    ///    than re-evaluating. Source is emphatic about this (a comment in
    ///    selection_module.gd reads "Result starts as pending and should
    ///    only become TRUE/FALSE after Check"), and every live handler
    ///    passes "?" to sync_selection_visual_for_current_mode(). Preserved.
    ///
    /// 8. **Set Value's failure path does not log, but the other modes' do.**
    ///    In `_handle_selection_insert_button()`, the Basic branch returns
    ///    after `set_selection_dashboard_message(...)` with no
    ///    ResearchLogger call, while the Else-if and Nested branches both log
    ///    a "Failed" event. Its SUCCESS path does not log either, again
    ///    unlike the other two modes. Preserved rather than normalised, for
    ///    the same research-log-comparability reason as Phase 23's Reset
    ///    naming: the log is analysed against Godot's output.
    ///
    /// 9. **Reset logs as plain "Reset"** — unprefixed, matching Functions
    ///    (Phase 23 note 4) rather than Loops' "Loop Reset". Source's own
    ///    inconsistency across modules, preserved.
    ///
    /// 10. **The Status panel is populated.** update_selection_dashboard()
    ///     blanks the status label the same way Loops' does, and
    ///     `_handle_selection_reset_button()` calls
    ///     update_selection_status_label() BEFORE update_selection_dashboard()
    ///     — so, exactly as in Loops (Phase 24 note 3), the write is
    ///     immediately overwritten and the panel is never visible in Godot.
    ///     Populated here for the same reason it was in Loops, and by the
    ///     same decision: the mode/value/condition/result line is the most
    ///     useful thing this module can show, and the blanking is an
    ///     accident of call order rather than a design choice.
    /// </summary>
    public class SelectionController : MonoBehaviour, IModuleController
    {
        // Mode identities — the exact strings source compares against.
        public const string ModeBasic = "Basic If";
        public const string ModeElseIf = "Else-if Ladder";
        public const string ModeNested = "Nested If";

        [Header("Module Reference")]
        [Tooltip("Auto-found via FindFirstObjectByType if left empty.")]
        [SerializeField] private SelectionModule selectionModule;

        [Header("UI References")]
        [Tooltip("Dual-purpose in Basic mode — feeds both Set Value and Set Condition. See porting note 4.")]
        [SerializeField] private TMP_InputField valueInput;
        [Tooltip("Godot's selection_mode_option (an OptionButton). Its item TEXT is the mode identity — see porting note 2.")]
        [SerializeField] private TMP_Dropdown modeDropdown;

        [Tooltip("Godot's insert_button. Labelled \"Set Value\" in Basic mode.")]
        [SerializeField] private Button setValueButton;
        [Tooltip("Godot's delete_button. Labelled \"Set Condition\" in Basic mode, \"Rule Info\" in the others.")]
        [SerializeField] private Button setConditionButton;
        [Tooltip("Godot's search_button. Always \"Check\".")]
        [SerializeField] private Button checkButton;
        [Tooltip("Godot's traverse_button. Always \"Toggle Rule\", but disabled outside Basic mode — see porting note 6.")]
        [SerializeField] private Button toggleRuleButton;
        [SerializeField] private Button resetButton;
        [Tooltip("The Quiz trigger button in THIS module's control strip. Assign the same Button " +
                 "GameObject here as on QuizController's Selection quiz-button field.")]
        [SerializeField] private Button quizButton;

        [Header("Button labels (assign the TMP text INSIDE each button)")]
        [Tooltip("Needed because this module relabels buttons per mode — see porting note 3.")]
        [SerializeField] private TMP_Text setValueButtonLabel;
        [SerializeField] private TMP_Text setConditionButtonLabel;

        [Header("Layout (optional)")]
        [SerializeField] private GridLayoutGroup buttonGrid;
        [SerializeField] private RectTransform dashboardContent;

        // ----- Live screen-side state (porting note 1) -----
        private string selectionCurrentMode = ModeBasic;

        private int selectionBasicValue = 75;
        private string selectionBasicConditionText = ">= 50";
        private int selectionBasicRuleIndex;

        /// Godot: selection_basic_rules. FOUR entries — see porting note 6.
        private static readonly string[] BasicRules = { ">= 50", "< 50", "== 0", "!= 0" };

        private string lastResultText = "?";
        private bool isBusy;

        private void Start()
        {
            if (selectionModule == null)
            {
                selectionModule = FindFirstObjectByType<SelectionModule>();
            }

            if (selectionModule == null)
            {
                Debug.LogError("SelectionController: no SelectionModule found in the scene.");
            }

            if (ResearchLogger.Instance != null)
            {
                ResearchLogger.Instance.ModuleName = "Selection";
            }

            WireButtons();
            SetUpModeDropdown();
            StartCoroutine(InitializeDeferred());
        }

        private IEnumerator InitializeDeferred()
        {
            yield return null;

            EnterModule();

            ApplyResponsiveButtonLayout();
        }

        public void EnterModule()
        {
            SetBusy(false);

            selectionCurrentMode = ModeBasic;
            if (modeDropdown != null) modeDropdown.SetValueWithoutNotify(0);

            ResetModeState(selectionCurrentMode);

            if (selectionModule != null) selectionModule.SetBlockTall(false);

            ClearVisualForCurrentMode();
            ApplyButtonLabelsForMode();
            RefreshDashboardForMode();
        }

        private void WireButtons()
        {
            if (setValueButton != null) setValueButton.onClick.AddListener(OnSetValueClicked);
            if (setConditionButton != null) setConditionButton.onClick.AddListener(OnSetConditionClicked);
            if (checkButton != null) checkButton.onClick.AddListener(OnCheckClicked);
            if (toggleRuleButton != null) toggleRuleButton.onClick.AddListener(OnToggleRuleClicked);
            if (resetButton != null) resetButton.onClick.AddListener(OnResetClicked);
        }

        /// Godot: setup_selection_mode_dropdown() / ensure_selection_mode_option().
        /// All three modes are listed even though only Basic is built —
        /// see the scope note in the class comment.
        private void SetUpModeDropdown()
        {
            if (modeDropdown == null) return;

            modeDropdown.ClearOptions();
            modeDropdown.AddOptions(new List<string> { ModeBasic, ModeElseIf, ModeNested });
            modeDropdown.SetValueWithoutNotify(0);
            modeDropdown.onValueChanged.AddListener(OnModeDropdownChanged);
        }

        /// Godot: _on_selection_mode_option_item_selected(index)
        public void OnModeDropdownChanged(int index)
        {
            if (modeDropdown == null || isBusy) return;
            if (index < 0 || index >= modeDropdown.options.Count) return;

            string chosen = modeDropdown.options[index].text;

            // Phase 25 scope gate — Else-if and Nested arrive in 25b.
            if (chosen != ModeBasic)
            {
                SetOperationExplanation(
                    $"{chosen} is not available yet.",
                    "This mode is still being built. Basic If mode is available now — " +
                    "it checks one condition and reports whether it is true or false.");

                LogEvent("Selection Mode", chosen, "Unavailable", "Mode not built yet");

                // Snap the dropdown back so it never shows a mode that isn't active.
                modeDropdown.SetValueWithoutNotify(0);
                return;
            }

            selectionCurrentMode = chosen;

            if (valueInput != null) valueInput.text = "";

            ResetModeState(selectionCurrentMode);
            if (selectionModule != null) selectionModule.SetBlockTall(false);

            ClearVisualForCurrentMode();
            ApplyButtonLabelsForMode();
            RefreshDashboardForMode();

            LogEvent("Selection Mode", chosen, "Success", "Selection mode changed");
        }

        /// Godot: reset_selection_mode_state(mode_name)
        private void ResetModeState(string modeName)
        {
            if (valueInput != null) valueInput.text = "";

            if (modeName == ModeBasic)
            {
                selectionBasicValue = 75;
                selectionBasicConditionText = ">= 50";
                selectionBasicRuleIndex = 0;
            }

            lastResultText = "?";
        }

        /// Godot: the per-mode branch of configure_buttons_for_current_module().
        private void ApplyButtonLabelsForMode()
        {
            bool basic = selectionCurrentMode == ModeBasic;

            if (setValueButtonLabel != null)
            {
                setValueButtonLabel.text = basic ? "Set Value"
                    : selectionCurrentMode == ModeElseIf ? "Set Score" : "Set Values";
            }

            if (setConditionButtonLabel != null)
            {
                setConditionButtonLabel.text = basic ? "Set Condition" : "Rule Info";
            }

            if (valueInput != null)
            {
                TMP_Text placeholder = valueInput.placeholder as TMP_Text;
                if (placeholder != null)
                {
                    placeholder.text = basic ? "Value: 75 or condition: >= 50"
                        : selectionCurrentMode == ModeElseIf ? "Enter score, e.g. 75"
                        : "Enter values, e.g. 65,80";
                }
            }

            // Toggle Rule is Basic-mode only — porting note 6.
            if (toggleRuleButton != null) toggleRuleButton.interactable = basic && !isBusy;
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

            Debug.Log($"SelectionController: applying a {columns}-column / {rows}-row button layout " +
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
            if (setValueButton != null) count++;
            if (setConditionButton != null) count++;
            if (checkButton != null) count++;
            if (toggleRuleButton != null) count++;
            if (resetButton != null) count++;
            if (quizButton != null) count++;
            return count;
        }

        // ---------------------------------------------------------------
        // Set Value — Godot: _handle_selection_insert_button(), Basic branch
        // ---------------------------------------------------------------

        public void OnSetValueClicked()
        {
            if (isBusy || selectionCurrentMode != ModeBasic) return;

            string typed = valueInput != null ? valueInput.text.Trim() : "";

            // Godot: parse_single_integer_input(). Note neither the failure
            // nor the success path logs in the Basic branch — porting note 8.
            if (string.IsNullOrEmpty(typed) || !int.TryParse(typed, out int parsed))
            {
                SetOperationExplanation(
                    "Set Value failed.",
                    "Enter one numeric value such as 75 before clicking Set Value.");
                return;
            }

            selectionBasicValue = parsed;
            lastResultText = "?";
            SyncVisual("?");

            SetOperationExplanation(
                $"Selection value set to {selectionBasicValue}.",
                $"This value will be tested against the current condition: value {selectionBasicConditionText}.");

            RefreshStatusAndProgress();
        }

        // ---------------------------------------------------------------
        // Set Condition — Godot: _handle_selection_delete_button(), Basic branch
        // ---------------------------------------------------------------

        public void OnSetConditionClicked()
        {
            if (isBusy || selectionCurrentMode != ModeBasic) return;

            string typed = valueInput != null ? valueInput.text.Trim() : "";

            if (!TryParseBasicCondition(typed, out string _, out int _, out string normalized, out string reason))
            {
                if (reason == "empty_condition")
                {
                    SetOperationExplanation(
                        "Set Condition failed. No condition was entered.",
                        "Enter a condition such as >= 50, < 40, == 0, or != 10 before clicking Set Condition.");
                }
                else
                {
                    SetOperationExplanation(
                        "Set Condition failed. Invalid condition format.",
                        "Enter the condition as an operator followed by a number. " +
                        "Examples: >= 50, < 40, == 0, or != 10.");
                }

                LogEvent("Selection Set Condition", typed, "Failed", "Invalid Basic If condition");
                return;
            }

            selectionBasicConditionText = normalized;
            lastResultText = "?";
            SyncVisual("?");

            SetOperationExplanation(
                $"Condition set to value {selectionBasicConditionText}.",
                $"The Basic If mode will now test the current value using this condition: " +
                $"value {selectionBasicConditionText}. Click Check to evaluate it.");

            RefreshStatusAndProgress();

            LogEvent("Selection Set Condition", "value " + selectionBasicConditionText,
                "Success", "Basic If condition updated");
        }

        // ---------------------------------------------------------------
        // Check — Godot: _handle_selection_search_button(), Basic branch
        // ---------------------------------------------------------------

        public void OnCheckClicked()
        {
            if (isBusy || selectionCurrentMode != ModeBasic) return;
            StartCoroutine(RunCheck());
        }

        private IEnumerator RunCheck()
        {
            if (!TryParseBasicCondition(selectionBasicConditionText, out string op, out int threshold, out string _, out string _))
            {
                SetOperationExplanation(
                    "Check failed.",
                    "The current condition is invalid. Set a valid condition such as >= 50 before checking.");
                LogEvent("Selection Check", selectionBasicConditionText, "Failed", "Invalid Basic If condition");
                yield break;
            }

            SetBusy(true);

            bool checkResult = EvaluateCondition(selectionBasicValue, op, threshold);
            lastResultText = checkResult ? "TRUE" : "FALSE";

            SyncVisual(lastResultText);

            if (selectionModule != null)
            {
                yield return selectionModule.PlayCheckAnimation();
            }

            // Source formats the result with GDScript's bool-to-string, which
            // renders as "true"/"false" lowercase — preserved here rather
            // than using the uppercase TRUE/FALSE shown in the RESULT box.
            string resultWord = checkResult ? "true" : "false";

            SetOperationExplanation(
                $"Basic If checked value {selectionBasicValue}. Result = {resultWord}.",
                $"The condition value {selectionBasicConditionText} was evaluated. " +
                $"The result was {resultWord}.");

            RefreshStatusAndProgress();

            LogEvent("Selection Check", "value " + selectionBasicConditionText,
                "Success", "Basic If checked. Result = " + resultWord);

            SetBusy(false);
        }

        // ---------------------------------------------------------------
        // Toggle Rule — Godot: _handle_selection_traverse_button(), Basic branch
        // ---------------------------------------------------------------

        public void OnToggleRuleClicked()
        {
            if (isBusy || selectionCurrentMode != ModeBasic) return;

            selectionBasicRuleIndex++;

            if (selectionBasicRuleIndex >= BasicRules.Length)
            {
                selectionBasicRuleIndex = 0;
            }

            selectionBasicConditionText = BasicRules[selectionBasicRuleIndex];
            lastResultText = "?";
            SyncVisual("?");

            SetOperationExplanation(
                $"Basic If rule changed to value {selectionBasicConditionText}.",
                "Toggle Rule changes the condition used by the Basic If statement. " +
                "The visual condition box has been updated to match the selected rule.");

            RefreshStatusAndProgress();
        }

        // ---------------------------------------------------------------
        // Reset — Godot: _handle_selection_reset_button()
        // ---------------------------------------------------------------

        public void OnResetClicked()
        {
            if (isBusy) return;

            if (selectionModule != null) selectionModule.ResetSelection();

            ResetModeState(selectionCurrentMode);

            if (valueInput != null) valueInput.text = "";

            ClearVisualForCurrentMode();
            RefreshDashboardForMode();

            SetOperationExplanation(
                "Selection module reset.",
                "Reset returns the module to its starting value and condition. " +
                "Click Check to evaluate the condition.");

            RefreshStatusAndProgress();

            // Plain "Reset", unprefixed — porting note 9.
            LogEvent("Reset", "", "Success", "Selection module reset for mode " + selectionCurrentMode);
        }

        // ---------------------------------------------------------------
        // Parsing / evaluation — ported from the screen script's helpers
        // ---------------------------------------------------------------

        /// Godot: parse_basic_condition(condition_text). See porting note 5.
        private static bool TryParseBasicCondition(string conditionText, out string op, out int threshold,
            out string normalized, out string reason)
        {
            op = "";
            threshold = 0;
            normalized = "";
            reason = "";

            string text = (conditionText ?? "").Trim();

            if (string.IsNullOrEmpty(text))
            {
                reason = "empty_condition";
                return false;
            }

            // Source allows "value >= 50" as well as ">= 50", stripping
            // exactly five characters — so "values >= 50" leaves a stray
            // "s" and fails. Preserved.
            if (text.ToLowerInvariant().StartsWith("value"))
            {
                text = text.Substring(5).Trim();
            }

            // Two-character operators first, so ">=" is not shadowed by ">".
            string[] operators = { ">=", "<=", "==", "!=", ">", "<" };

            foreach (string candidate in operators)
            {
                if (!text.StartsWith(candidate)) continue;

                string numberText = text.Substring(candidate.Length).Trim();

                if (int.TryParse(numberText, out int parsedThreshold))
                {
                    op = candidate;
                    threshold = parsedThreshold;
                    normalized = candidate + " " + numberText;
                    reason = "valid_condition";
                    return true;
                }
            }

            reason = "invalid_condition";
            return false;
        }

        /// Godot: evaluate_condition(value, operator_text, threshold).
        /// Note the final `return false` — an unrecognised operator reads as
        /// false rather than raising. Preserved.
        private static bool EvaluateCondition(int value, string op, int threshold)
        {
            switch (op)
            {
                case ">=": return value >= threshold;
                case "<=": return value <= threshold;
                case ">": return value > threshold;
                case "<": return value < threshold;
                case "==": return value == threshold;
                case "!=": return value != threshold;
                default: return false;
            }
        }

        // ---------------------------------------------------------------
        // Visual sync — Godot: sync_selection_visual_for_current_mode()
        // ---------------------------------------------------------------

        private void SyncVisual(string resultText)
        {
            if (selectionModule == null) return;

            selectionModule.SetSelectionVisualTexts(
                selectionBasicValue.ToString(),
                "value " + selectionBasicConditionText,
                resultText);
        }

        /// Godot: clear_selection_visual_for_current_mode() — note the value
        /// box shows "-", not the current value.
        private void ClearVisualForCurrentMode()
        {
            if (selectionModule == null) return;

            selectionModule.SetSelectionVisualTexts(
                "-",
                "value " + selectionBasicConditionText,
                "?");
        }

        // ---------------------------------------------------------------
        // Dashboard — Godot: update_selection_dashboard()
        // ---------------------------------------------------------------

        private void RefreshDashboardForMode()
        {
            string summary;
            string instruction;

            if (selectionCurrentMode == ModeElseIf)
            {
                summary = "An else-if ladder checks several conditions in order. The first true condition determines the result.";
                instruction = "Enter one score such as 75, then choose Set Score and Check. Use Toggle Rule to switch ladder examples.";
            }
            else if (selectionCurrentMode == ModeNested)
            {
                summary = "A nested if statement checks one condition first, then checks another condition inside it.";
                instruction = "Enter two values such as 65,80. Then choose Set Values and Check. Use Toggle Rule to switch nested examples.";
            }
            else
            {
                summary = "A basic if statement checks one condition and decides whether the condition is true or false.";
                instruction = "Enter one value such as 75, or enter a condition such as >= 50. " +
                              "Then choose Set Value, Set Condition, Check, Toggle Rule, Reset, or Quiz.";
            }

            // Source's title is "Selection Module: <Mode>" — the mode is part
            // of the title, not a separate panel.
            DashboardController.Instance?.SetSummary($"Selection Module: {selectionCurrentMode}\n{summary}");
            DashboardController.Instance?.SetInstruction(instruction);
            DashboardController.Instance?.SetOperation("No operation selected yet.");
            DashboardController.Instance?.SetExplanation(GetDefaultExplanation());

            RebuildDashboardLayout();
            RefreshStatusAndProgress();
        }

        /// Godot: get_selection_default_explanation()
        private string GetDefaultExplanation()
        {
            if (selectionCurrentMode == ModeElseIf)
            {
                return "An else-if ladder checks conditions from top to bottom. " +
                       "When the first true condition is found, the remaining conditions are skipped.";
            }

            if (selectionCurrentMode == ModeNested)
            {
                return "A nested if statement checks an outer condition first. " +
                       "The inner condition is checked only if the outer condition is true.";
            }

            return "A basic if statement evaluates one condition. " +
                   "If the condition is true, the true path is followed. If it is false, the false path is followed.";
        }

        // ---------------------------------------------------------------
        // Shared helpers
        // ---------------------------------------------------------------

        private void SetBusy(bool busy)
        {
            isBusy = busy;

            if (setValueButton != null) setValueButton.interactable = !busy;
            if (setConditionButton != null) setConditionButton.interactable = !busy;
            if (checkButton != null) checkButton.interactable = !busy;
            if (resetButton != null) resetButton.interactable = !busy;

            // Toggle Rule additionally respects the Basic-mode gate.
            if (toggleRuleButton != null)
            {
                toggleRuleButton.interactable = !busy && selectionCurrentMode == ModeBasic;
            }
        }

        private void SetOperationExplanation(string operation, string explanation)
        {
            DashboardController.Instance?.SetOperation(operation);
            DashboardController.Instance?.SetExplanation(explanation);
            RebuildDashboardLayout();
        }

        /// Status line from selection_module.gd's get_status_text() basic
        /// branch, driven by the LIVE screen-side state. Populated rather
        /// than blank — porting note 10.
        private void RefreshStatusAndProgress()
        {
            DashboardController.Instance?.SetStatus(
                $"Selection status: mode = basic if | value = {selectionBasicValue}" +
                $" | condition: value {selectionBasicConditionText} | result = {lastResultText}");

            DashboardController.Instance?.SetProgress(
                $"Rule {selectionBasicRuleIndex + 1}/{BasicRules.Length}: value {selectionBasicConditionText}");

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
