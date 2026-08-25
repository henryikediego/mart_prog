using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MartProg.Modules
{
    /// <summary>
    /// Phase 25 / 25b / 25c: the Selection module's UI controller — all
    /// three modes. Ported from ar_learning_screen.gd's
    /// `_handle_selection_*_button()` handlers, `parse_basic_condition()`,
    /// `evaluate_condition()`, `parse_single_integer_input()`,
    /// `sync_selection_visual_for_current_mode()`,
    /// `update_selection_dashboard()`, `reset_selection_mode_state()` and
    /// `_on_selection_mode_option_item_selected()`.
    ///
    /// **All three modes are now built** — Basic If (Phase 25), Else-if
    /// Ladder (Phase 25b) and Nested If (Phase 25c). The mode scope gate
    /// is gone.
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
    /// 6. **Toggle Rule cycles a FOUR-entry list in Basic mode**, not the
    ///    module script's six comparison rules: ">= 50", "< 50", "== 0",
    ///    "!= 0", wrapping at the end.
    ///
    ///    **Correction made in Phase 25b:** Phase 25 recorded that Toggle
    ///    Rule is disabled outside Basic mode, because
    ///    configure_buttons_for_current_module() contains
    ///    `if selection_module.selection_mode == "basic": traverse_button.disabled = false`
    ///    `else: traverse_button.disabled = true` with the tooltip "Toggle
    ///    Rule is available only in Basic If mode". That gate reads
    ///    `selection_module.selection_mode` — the DEAD module script's own
    ///    mode field (note 1), which the live implementation never writes.
    ///    It is therefore permanently "basic" and the disable branch never
    ///    runs. Meanwhile `_handle_selection_traverse_button()` has fully
    ///    working Else-if and Nested branches. So Toggle Rule is in fact
    ///    available in every mode, and is enabled here accordingly. A good
    ///    illustration of why note 1 matters: reading the dead state as if
    ///    it were live produced a wrong conclusion about observable
    ///    behaviour.
    ///
    /// 7. **Result is "pending" until Check is pressed.** Set Value, Set
    ///    Condition and Toggle Rule all push "?" into the RESULT box rather
    ///    than re-evaluating. Source is emphatic about this (a comment in
    ///    selection_module.gd reads "Result starts as pending and should
    ///    only become TRUE/FALSE after Check"), and every live handler
    ///    passes "?" to sync_selection_visual_for_current_mode(). Preserved.
    ///
    /// 8. **Set Value's failure path does not log, but its SUCCESS path
    ///    does — under a name emitted from a different function.**
    ///
    ///    **CORRECTED in Phase 27b (research-log audit).** Phase 25 recorded
    ///    that Basic mode's Set Value "does not log either" on success. That
    ///    was wrong, and the error came from reading only
    ///    `_handle_selection_insert_button()`. Its Basic branch is not the
    ///    whole story: after a clean parse it calls
    ///    `if has_method("handle_selection_set_value"): await
    ///    handle_selection_set_value()` — a `has_method` guard that reads
    ///    like a dead-code hedge but always passes, because the function is
    ///    defined in the same script. That delegated function logs
    ///    `"Selection Set Value" / typed_value / "Success" /
    ///    get_last_action_message()`. Every successful Basic-mode Set Value
    ///    therefore produces a research row in Godot; Unity produced none
    ///    until this phase. Now emitted.
    ///
    ///    The failure half of the original note stands and is unchanged: a
    ///    bad parse returns from the button handler BEFORE delegating, so
    ///    nothing is logged, while Else-if and Nested both log a "Failed"
    ///    event from their own branches. That asymmetry is source and is
    ///    preserved.
    ///
    ///    Two consequences of the same delegation, both handled here:
    ///
    ///    (a) `handle_selection_set_value()` re-validates the input and has
    ///        its own "Empty input" and "Invalid integer input" failure
    ///        logs. Both are UNREACHABLE — the button handler already
    ///        validated the identical field — so they are deliberately not
    ///        ported. Their absence is correct, not an omission.
    ///
    ///    (b) The function's success tail runs `value_input.text = ""`, so
    ///        Basic mode clears the input box after Set Value while Else-if's
    ///        Set Score and Nested's Set Values do NOT — neither delegates.
    ///        One of source's own inconsistencies; now reproduced exactly
    ///        (see HandleSetValue / HandleSetScore / HandleSetNestedValues).
    ///
    ///    **Deliberately NOT ported from that function:** its
    ///    `index_explanation_label.text = selection_module.get_status_text()`
    ///    line, and its `show_code`/`show_code_explanation` calls, all of
    ///    which read SelectionModule — the dead shadow implementation (note
    ///    1), whose value and rule list differ from the live screen state.
    ///    Porting them would put dead-code state on screen. Documented as a
    ///    deviation; the observable difference is that Unity's Status panel
    ///    keeps showing live state where Godot briefly shows the shadow
    ///    module's.
    ///
    /// 9. **Reset logs as plain "Reset"** — unprefixed, matching Functions
    ///    (Phase 23 note 4) rather than Loops' "Loop Reset". Source's own
    ///    inconsistency across modules, preserved.
    ///
    /// 11. **Else-if Ladder uses two PRESET rules, not a typed ladder.**
    ///     Source's `get_current_ladder_rule()` returns one of two
    ///     dictionaries — "Grade Rule" (>= 80 A, >= 70 B, >= 50 Pass, else
    ///     Fail) and "Performance Rule" (>= 90 Excellent, >= 70 Good, >= 50
    ///     Average, else Needs Improvement) — and Toggle Rule switches
    ///     between them. Students never type a ladder; Set Condition is
    ///     replaced by "Rule Info", which just reports which preset is
    ///     active. Both presets are ported verbatim, including their result
    ///     strings, which are mixed-case ("Pass", "Fail", "Excellent") unlike
    ///     Basic mode's uppercase TRUE/FALSE.
    ///
    /// 12. **The ladder evaluator reports its whole decision trace.**
    ///     `evaluate_else_if_ladder()` walks the branches in order, appending
    ///     "score >= 80 → True" or "... → False" to a `checks` list, and
    ///     BREAKS on the first match — so the trace shows exactly the
    ///     branches that were actually evaluated, not all of them. The
    ///     dashboard joins that list with " | ". This is the pedagogical
    ///     point of the mode, so the trace, the arrow glyph and the break
    ///     are all ported exactly.
    ///
    /// 13. **Toggle Rule's wrap is hardcoded to two rules.** Source does
    ///     `if selection_ladder_rule_index > 1: selection_ladder_rule_index = 0`
    ///     rather than comparing against the rule count. Functionally
    ///     identical while there are exactly two presets, but it would
    ///     silently break if a third were added. Ported as a proper
    ///     length-based wrap, since the observable behaviour is identical
    ///     and the source form is a latent bug rather than a quirk worth
    ///     reproducing.
    ///
    /// 14. **Check logs TWICE in Basic mode — restored in Phase 25b.**
    ///     `_handle_selection_search_button()` ends with a generic
    ///     `ResearchLogger.log_event("Selection Check", "", "Success",
    ///     "Selection checked for mode " + mode)` that sits OUTSIDE the
    ///     if/elif chain, and the Basic branch does not return — so Basic
    ///     writes its own detailed entry AND that generic one, while Else-if
    ///     and Nested write only the generic one. Phase 25 shipped with the
    ///     detailed entry only; this was a fidelity gap on my part, not a
    ///     deliberate choice, and is corrected here. Row counts in the
    ///     research log now match Godot's.
    ///
    /// 17. **Nested If uses two PRESET rules with NAMED values.** Source's
    ///     `get_current_nested_rule()` returns "Pass Rule"
    ///     (score >= 50 outer, attendance >= 75 inner → Pass / "Attendance
    ///     too low" / Fail) or "Project Eligibility Rule" (exam >= 80,
    ///     project >= 70 → Eligible / "Project score too low" / "Exam score
    ///     too low"). The `first_name`/`second_name` keys are what let the
    ///     dashboard say "score = 65, attendance = 80" rather than "first"
    ///     and "second". All strings ported verbatim, including the long
    ///     failure results — those are exactly what SelectionModule's RESULT
    ///     box widening table was written for (its 18-character bucket
    ///     covers "Attendance too low" precisely).
    ///
    /// 18. **The nested explanation has two shapes, and that IS the lesson.**
    ///     When the outer condition passes, source says the inner condition
    ///     "was checked"; when it fails, it says the inner condition "was
    ///     skipped". That distinction is the entire pedagogical content of
    ///     the mode, so both sentences are ported word-for-word rather than
    ///     collapsed into one.
    ///
    /// 19. **Toggle Rule logs in EVERY mode — added in Phase 25c.**
    ///     `_handle_selection_traverse_button()` ends with a generic
    ///     `ResearchLogger.log_event("Selection Toggle Rule", ...)` sitting
    ///     outside the mode branches, and no branch returns, so all three
    ///     modes reach it. Phases 25 and 25b logged nothing at all for
    ///     Toggle Rule — a fidelity gap on my part, the third of this kind
    ///     in this module (see notes 14 and 6). Corrected: LogToggleRule()
    ///     is called from all three toggle paths.
    ///
    ///     The pattern is now clear enough to state as a rule for Memory
    ///     Management: **in this codebase, a handler's trailing
    ///     ResearchLogger call after an if/elif chain usually fires for
    ///     every branch**, because the branches rarely return. Check for a
    ///     trailing log before assuming a branch's own log is the only one.
    ///
    /// 16. **The CONDITION block is never the tall variant — a second
    ///     dead-state trap.** `create_condition_block()` switches height
    ///     1.08 -> 1.35 and font 38 -> 28 when
    ///     `selection_mode == "elseif"` or `"nested"`. That is
    ///     `selection_module.selection_mode` — the same dead field behind
    ///     note 6's correction, never written by the live implementation.
    ///     It is permanently "basic", so the tall geometry never renders in
    ///     the real app. It exists for the DEAD path's multi-line ladder
    ///     text (`get_elseif_condition_text()` returns
    ///     "if >= 85\nelif >= 70\nelif >= 50\nelse"), whereas the live
    ///     path puts only the rule NAME in the block — one short line.
    ///
    ///     Phase 25b initially called SetBlockTall(true) for Else-if,
    ///     reasoning from the source constant rather than from what runs;
    ///     on-device the block was visibly too tall for "Grade Rule".
    ///     Corrected to always false. SelectionModule.SetBlockTall() is
    ///     retained but is now only ever called with false — see its own
    ///     porting note 6.
    ///
    ///     That is twice in this module that reading the dead state as if
    ///     it were live produced a wrong conclusion, both caught only
    ///     on-device. Worth remembering for Memory Management: when a
    ///     module script has a shadow implementation, every `if` that reads
    ///     its state is suspect.
    ///
    /// 15. **Rule Info's log is shared across the non-basic modes.** The
    ///     "Selection Rule Info" entry sits at the end of
    ///     `_handle_selection_delete_button()`, after the mode branches, and
    ///     is only reached by Else-if and Nested because the Basic branch
    ///     returns early. Preserved: Basic logs "Selection Set Condition",
    ///     the others log "Selection Rule Info".
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

        /// <summary>
        /// Read by QuizController so the Selection quiz can be filtered down
        /// to the mode the learner is actually in — see QuizQuestionBank's
        /// "Selection quiz deviations", item (a). Source has no equivalent
        /// accessor because source never filters the Selection quiz; this
        /// exists purely to serve that deviation.
        ///
        /// Read-only on purpose: the mode dropdown stays the only thing that
        /// can change it, so nothing outside this controller can put the
        /// module and the quiz out of step.
        /// </summary>
        public string CurrentMode => selectionCurrentMode;

        private int selectionBasicValue = 75;
        private string selectionBasicConditionText = ">= 50";
        private int selectionBasicRuleIndex;

        /// Godot: selection_basic_rules. FOUR entries — see porting note 6.
        private static readonly string[] BasicRules = { ">= 50", "< 50", "== 0", "!= 0" };

        // ----- Else-if Ladder state (Phase 25b) -----
        private int selectionLadderScore = 75;
        private int selectionLadderRuleIndex;

        /// Godot: get_current_ladder_rule()'s two preset dictionaries — see
        /// porting note 11. Ported verbatim, mixed-case result strings included.
        private sealed class LadderBranch
        {
            public readonly string Label;
            public readonly int Threshold;
            public readonly string Result;

            public LadderBranch(string label, int threshold, string result)
            {
                Label = label;
                Threshold = threshold;
                Result = result;
            }
        }

        private sealed class LadderRule
        {
            public readonly string Name;
            public readonly LadderBranch[] Branches;
            public readonly string ElseResult;

            public LadderRule(string name, LadderBranch[] branches, string elseResult)
            {
                Name = name;
                Branches = branches;
                ElseResult = elseResult;
            }
        }

        private static readonly LadderRule[] LadderRules =
        {
            new LadderRule("Grade Rule", new[]
            {
                new LadderBranch("score >= 80", 80, "A"),
                new LadderBranch("score >= 70", 70, "B"),
                new LadderBranch("score >= 50", 50, "Pass")
            }, "Fail"),

            new LadderRule("Performance Rule", new[]
            {
                new LadderBranch("score >= 90", 90, "Excellent"),
                new LadderBranch("score >= 70", 70, "Good"),
                new LadderBranch("score >= 50", 50, "Average")
            }, "Needs Improvement")
        };

        private LadderRule CurrentLadderRule => LadderRules[Mathf.Clamp(selectionLadderRuleIndex, 0, LadderRules.Length - 1)];

        // ----- Nested If state -----
        private int selectionNestedFirstValue = 65;
        private int selectionNestedSecondValue = 80;
        private int selectionNestedRuleIndex;

        /// Godot: get_current_nested_rule()'s two preset dictionaries — see
        /// porting note 17. Every string ported verbatim, including the
        /// deliberately long failure results ("Attendance too low",
        /// "Project score too low"), which are what the RESULT box's
        /// widening table exists for.
        private sealed class NestedRule
        {
            public readonly string Name;
            public readonly string FirstName;
            public readonly string SecondName;
            public readonly string OuterCondition;
            public readonly string InnerCondition;
            public readonly int OuterThreshold;
            public readonly int InnerThreshold;
            public readonly string TrueResult;
            public readonly string InnerFalseResult;
            public readonly string OuterFalseResult;

            public NestedRule(string name, string firstName, string secondName,
                string outerCondition, string innerCondition,
                int outerThreshold, int innerThreshold,
                string trueResult, string innerFalseResult, string outerFalseResult)
            {
                Name = name;
                FirstName = firstName;
                SecondName = secondName;
                OuterCondition = outerCondition;
                InnerCondition = innerCondition;
                OuterThreshold = outerThreshold;
                InnerThreshold = innerThreshold;
                TrueResult = trueResult;
                InnerFalseResult = innerFalseResult;
                OuterFalseResult = outerFalseResult;
            }
        }

        private static readonly NestedRule[] NestedRules =
        {
            new NestedRule("Pass Rule", "score", "attendance",
                "score >= 50", "attendance >= 75", 50, 75,
                "Pass", "Attendance too low", "Fail"),

            new NestedRule("Project Eligibility Rule", "exam", "project",
                "exam >= 80", "project >= 70", 80, 70,
                "Eligible", "Project score too low", "Exam score too low")
        };

        private NestedRule CurrentNestedRule => NestedRules[Mathf.Clamp(selectionNestedRuleIndex, 0, NestedRules.Length - 1)];

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

            if (valueInput != null) valueInput.onSubmit.AddListener(_ => OnSetValueClicked());

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

            selectionCurrentMode = chosen;

            if (valueInput != null) valueInput.text = "";

            ResetModeState(selectionCurrentMode);

            // The block stays SHORT in every mode — see porting note 16.
            if (selectionModule != null) selectionModule.SetBlockTall(false);

            ClearVisualForCurrentMode();
            ApplyButtonLabelsForMode();
            RefreshDashboardForMode();

            LogEvent("Selection Mode", chosen, "Success", "Selection mode changed");
        }

        private int IndexForMode(string mode)
        {
            if (mode == ModeElseIf) return 1;
            if (mode == ModeNested) return 2;
            return 0;
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
            else if (modeName == ModeElseIf)
            {
                selectionLadderScore = 75;
                selectionLadderRuleIndex = 0;
            }
            else if (modeName == ModeNested)
            {
                selectionNestedFirstValue = 65;
                selectionNestedSecondValue = 80;
                selectionNestedRuleIndex = 0;
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

            // Available in every mode — see porting note 6's correction.
            if (toggleRuleButton != null) toggleRuleButton.interactable = !isBusy;
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
            if (isBusy) return;

            if (selectionCurrentMode == ModeElseIf)
            {
                HandleSetScore();
                return;
            }

            if (selectionCurrentMode == ModeNested)
            {
                HandleSetNestedValues();
                return;
            }

            string typed = valueInput != null ? valueInput.text.Trim() : "";

            // Godot: parse_single_integer_input(). The FAILURE path does not
            // log — the button handler returns before delegating. The success
            // path below does, via handle_selection_set_value(). Note 8.
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

            // Godot: the success tail of handle_selection_set_value(), which
            // _handle_selection_insert_button() delegates to once the parse
            // succeeds. Basic mode's success DOES log, and it clears the
            // input box — neither of which Else-if or Nested do, because
            // neither delegates. Porting note 8.
            // The details column is SelectionModule.get_last_action_message()
            // after its set_value_from_text() — and the ">= 50" in it is a
            // CONSTANT, not the student's current condition. The shadow
            // module's current_rule and threshold are only ever written by
            // its own toggle_rule() and set_condition_from_text(), both of
            // which are dead code (note 1), so get_rule_code() returns
            // "value >= 50" for the lifetime of the app no matter what the
            // student toggles on screen. A source defect, but it lives in a
            // log field rather than in the UI, and reproducing it is the
            // whole point of this row — normalising it would break the
            // column-for-column comparison against the Godot build's CSV.
            // Flagged in the dev report rather than fixed. Porting note 8.
            LogEvent("Selection Set Value", typed, "Success",
                $"Value set to {selectionBasicValue}. Click Check to evaluate value >= 50.");

            if (valueInput != null) valueInput.text = "";

            RefreshStatusAndProgress();
        }

        /// Godot: _handle_selection_insert_button(), Else-if branch. Unlike
        /// the Basic branch this one DOES log, on both success and failure —
        /// porting note 8.
        private void HandleSetScore()
        {
            string typed = valueInput != null ? valueInput.text.Trim() : "";

            if (string.IsNullOrEmpty(typed) || !int.TryParse(typed, out int parsed))
            {
                SetOperationExplanation(
                    "Set Score failed.",
                    "Enter one numeric score such as 75 before clicking Set Score.");

                LogEvent("Selection Set Score", typed, "Failed", "Invalid score input for Else-if Ladder mode");
                return;
            }

            selectionLadderScore = parsed;
            lastResultText = "?";
            SyncVisual("?");

            SetOperationExplanation(
                $"Score set to {selectionLadderScore}.",
                "The value box has been updated with the new score. Click Check to evaluate " +
                "this score using the current else-if ladder rule.");

            RefreshStatusAndProgress();

            LogEvent("Selection Set Score", selectionLadderScore.ToString(), "Success", "Else-if Ladder score updated");
        }

        /// Godot: _handle_selection_insert_button(), Nested branch, plus
        /// parse_two_integer_inputs(). Two comma-separated values, named per
        /// rule ("score"/"attendance", or "exam"/"project").
        private void HandleSetNestedValues()
        {
            string typed = valueInput != null ? valueInput.text.Trim() : "";

            if (!TryParseTwoIntegers(typed, out int first, out int second))
            {
                SetOperationExplanation(
                    "Set Values failed.",
                    "Enter exactly two numbers separated by a comma. For example, enter 65,80.");

                LogEvent("Selection Set Nested Values", typed, "Failed", "Invalid Nested If values");
                return;
            }

            selectionNestedFirstValue = first;
            selectionNestedSecondValue = second;
            lastResultText = "?";
            SyncVisual("?");

            NestedRule rule = CurrentNestedRule;

            SetOperationExplanation(
                "Nested values were set.",
                $"{rule.FirstName} = {selectionNestedFirstValue}, " +
                $"{rule.SecondName} = {selectionNestedSecondValue}. " +
                "These values will be checked using the current nested if rule.");

            RefreshStatusAndProgress();

            LogEvent("Selection Set Nested Values",
                $"{selectionNestedFirstValue},{selectionNestedSecondValue}",
                "Success", "Nested If values updated");
        }

        /// Godot: parse_two_integer_inputs(). Requires exactly two parts,
        /// both valid integers.
        private static bool TryParseTwoIntegers(string text, out int first, out int second)
        {
            first = 0;
            second = 0;

            if (string.IsNullOrEmpty(text)) return false;

            string[] parts = text.Split(',');
            if (parts.Length != 2) return false;

            return int.TryParse(parts[0].Trim(), out first)
                && int.TryParse(parts[1].Trim(), out second);
        }

        // ---------------------------------------------------------------
        // Set Condition (Basic) / Rule Info (Else-if, Nested) —
        // Godot: _handle_selection_delete_button()
        // ---------------------------------------------------------------

        public void OnSetConditionClicked()
        {
            if (isBusy) return;

            if (selectionCurrentMode != ModeBasic)
            {
                HandleRuleInfo();
                return;
            }

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

        /// Godot: _handle_selection_delete_button()'s Else-if branch, plus the
        /// shared "Selection Rule Info" log that follows the mode branches —
        /// porting note 15. Reports the active preset rather than parsing
        /// anything; students switch ladders with Toggle Rule.
        private void HandleRuleInfo()
        {
            if (selectionCurrentMode == ModeNested)
            {
                NestedRule nested = CurrentNestedRule;

                SetOperationExplanation(
                    $"Current nested rule is {nested.Name}.",
                    $"Outer condition: {nested.OuterCondition}. " +
                    $"Inner condition: {nested.InnerCondition}. " +
                    "Use Toggle Rule to switch examples.");
            }
            else
            {
                LadderRule rule = CurrentLadderRule;

                SetOperationExplanation(
                    $"Current ladder rule is {rule.Name}.",
                    "Else-if Ladder uses preset rules. Use Toggle Rule to switch between rule " +
                    "examples instead of typing a full ladder manually.");
            }

            RefreshStatusAndProgress();

            LogEvent("Selection Rule Info", "", "Success",
                "Selection rule action completed for mode " + selectionCurrentMode);
        }

        // ---------------------------------------------------------------
        // Check — Godot: _handle_selection_search_button(), Basic branch
        // ---------------------------------------------------------------

        public void OnCheckClicked()
        {
            if (isBusy) return;

            if (selectionCurrentMode == ModeElseIf)
            {
                StartCoroutine(RunLadderCheck());
                return;
            }

            if (selectionCurrentMode == ModeNested)
            {
                StartCoroutine(RunNestedCheck());
                return;
            }

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

            // Source's generic trailing log, which the Basic branch also
            // reaches because it does not return — porting note 14.
            LogEvent("Selection Check", "", "Success", "Selection checked for mode " + selectionCurrentMode);

            SetBusy(false);
        }

        /// Godot: _handle_selection_search_button()'s Else-if branch plus
        /// evaluate_else_if_ladder(). The decision trace is the point of this
        /// mode — porting note 12.
        private IEnumerator RunLadderCheck()
        {
            SetBusy(true);

            LadderRule rule = CurrentLadderRule;
            List<string> checks = new List<string>();
            string finalResult = rule.ElseResult;

            foreach (LadderBranch branch in rule.Branches)
            {
                if (selectionLadderScore >= branch.Threshold)
                {
                    checks.Add(branch.Label + " → True");
                    finalResult = branch.Result;
                    break;   // source breaks on the first match
                }

                checks.Add(branch.Label + " → False");
            }

            lastResultText = finalResult;
            SyncVisual(finalResult);

            if (selectionModule != null)
            {
                yield return selectionModule.PlayCheckAnimation();
            }

            SetOperationExplanation(
                $"Score {selectionLadderScore} was checked using the {rule.Name}. Result = {finalResult}.",
                $"The ladder checked conditions in order: {string.Join(" | ", checks)}. " +
                "Once the first true condition was found, the remaining branches were skipped.");

            RefreshStatusAndProgress();

            // Else-if reaches only the generic trailing log — porting note 14.
            LogEvent("Selection Check", "", "Success", "Selection checked for mode " + selectionCurrentMode);

            SetBusy(false);
        }

        /// Godot: _handle_selection_search_button()'s Nested branch plus
        /// evaluate_nested_if(). The explanation has two distinct shapes
        /// depending on whether the outer condition passed — porting note 18.
        private IEnumerator RunNestedCheck()
        {
            SetBusy(true);

            NestedRule rule = CurrentNestedRule;

            bool outerTrue = selectionNestedFirstValue >= rule.OuterThreshold;
            bool innerTrue = false;
            string finalResult;

            if (outerTrue)
            {
                innerTrue = selectionNestedSecondValue >= rule.InnerThreshold;
                finalResult = innerTrue ? rule.TrueResult : rule.InnerFalseResult;
            }
            else
            {
                finalResult = rule.OuterFalseResult;
            }

            lastResultText = finalResult;
            SyncVisual(finalResult);

            if (selectionModule != null)
            {
                yield return selectionModule.PlayCheckAnimation();
            }

            string explanation = outerTrue
                ? $"The outer condition {rule.OuterCondition} was true, so the inner condition " +
                  $"{rule.InnerCondition} was checked. Final result: {finalResult}."
                : $"The outer condition {rule.OuterCondition} was false, so the inner condition " +
                  $"was skipped. Final result: {finalResult}.";

            SetOperationExplanation(
                $"Nested If checked the current values. Result = {finalResult}.",
                explanation);

            RefreshStatusAndProgress();

            LogEvent("Selection Check", "", "Success", "Selection checked for mode " + selectionCurrentMode);

            SetBusy(false);
        }

        // ---------------------------------------------------------------
        // Toggle Rule — Godot: _handle_selection_traverse_button()
        // ---------------------------------------------------------------

        public void OnToggleRuleClicked()
        {
            if (isBusy) return;

            if (selectionCurrentMode == ModeElseIf)
            {
                HandleToggleLadderRule();
                return;
            }

            if (selectionCurrentMode == ModeNested)
            {
                HandleToggleNestedRule();
                return;
            }

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
            LogToggleRule();
        }

        /// Godot: _handle_selection_traverse_button()'s Else-if branch.
        /// Source wraps with a hardcoded `> 1`; ported as a length-based
        /// wrap — porting note 13.
        private void HandleToggleLadderRule()
        {
            selectionLadderRuleIndex++;

            if (selectionLadderRuleIndex >= LadderRules.Length)
            {
                selectionLadderRuleIndex = 0;
            }

            lastResultText = "?";
            SyncVisual("?");

            SetOperationExplanation(
                $"Else-if Ladder rule changed to {CurrentLadderRule.Name}.",
                "The visual condition box now matches the selected ladder rule. " +
                "The ladder will be used when Check is selected.");

            RefreshStatusAndProgress();
            LogToggleRule();
        }

        /// Godot: _handle_selection_traverse_button()'s Nested branch.
        private void HandleToggleNestedRule()
        {
            selectionNestedRuleIndex++;

            if (selectionNestedRuleIndex >= NestedRules.Length)
            {
                selectionNestedRuleIndex = 0;
            }

            lastResultText = "?";
            SyncVisual("?");

            SetOperationExplanation(
                $"Nested If rule changed to {CurrentNestedRule.Name}.",
                "The visual condition box now matches the selected nested rule. " +
                "The inner condition will only be checked if the outer condition is true.");

            RefreshStatusAndProgress();
            LogToggleRule();
        }

        /// Godot logs this after the mode branches in
        /// _handle_selection_traverse_button(), and no branch returns — so it
        /// fires in every mode. Porting note 19.
        private void LogToggleRule()
        {
            LogEvent("Selection Toggle Rule", "", "Success",
                "Selection rule toggled for mode " + selectionCurrentMode);
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
                CurrentValueText(),
                CurrentConditionText(),
                resultText);
        }

        /// The value box's content per mode. Godot builds this inline in
        /// sync_selection_visual_for_current_mode().
        private string CurrentValueText()
        {
            if (selectionCurrentMode == ModeElseIf) return selectionLadderScore.ToString();

            // Nested shows both values joined with " / " — the longest VALUE
            // string in the app, and what the font-fit table exists for.
            if (selectionCurrentMode == ModeNested)
            {
                return $"{selectionNestedFirstValue} / {selectionNestedSecondValue}";
            }

            return selectionBasicValue.ToString();
        }

        /// Godot: get_selection_visual_condition_text_for_current_mode().
        /// Note Else-if shows the RULE NAME in the condition block, not the
        /// ladder itself — the branches only appear in the dashboard trace.
        private string CurrentConditionText()
        {
            if (selectionCurrentMode == ModeElseIf) return CurrentLadderRule.Name;
            if (selectionCurrentMode == ModeNested) return CurrentNestedRule.Name;
            return "value " + selectionBasicConditionText;
        }

        /// Godot: clear_selection_visual_for_current_mode() — note the value
        /// box shows "-", not the current value.
        private void ClearVisualForCurrentMode()
        {
            if (selectionModule == null) return;

            selectionModule.SetSelectionVisualTexts(
                "-",
                CurrentConditionText(),
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
                instruction = "Enter one score such as 75, then choose Set Score, Check, Toggle Rule, Reset, or Quiz. Toggle Rule switches between ladder examples.";
            }
            else if (selectionCurrentMode == ModeNested)
            {
                summary = "A nested if statement checks one condition first, then checks another condition inside it.";
                instruction = "Enter two values such as 65,80, then choose Set Values, Check, Toggle Rule, Reset, or Quiz. Toggle Rule switches between nested examples.";
            }
            else
            {
                summary = "A basic if statement checks one condition and decides whether the condition is true or false.";
                instruction = "Enter one value such as 75, or enter a condition such as >= 50. " +
                              "Then choose Set Value, Set Condition, Check, Toggle Rule, Reset, or Quiz.";
            }

            // The module/mode title used to lead this panel ("Selection
            // Module: Basic If"). Dropped in the Phase 26b/26c audit — the
            // Change Module selector and the mode dropdown both already name
            // it, and a panel headed "Summary" should hold a summary. Nothing
            // is lost here because these three sentences are themselves
            // mode-specific.
            DashboardController.Instance?.SetSummary(summary);
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

            if (toggleRuleButton != null) toggleRuleButton.interactable = !busy;
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
            if (selectionCurrentMode == ModeElseIf)
            {
                // Godot: get_status_text()'s else-if branch — note it omits
                // the condition, unlike the basic branch.
                DashboardController.Instance?.SetStatus(
                    $"Selection status: mode = else-if ladder | value = {selectionLadderScore}" +
                    $" | result = {lastResultText}");

                DashboardController.Instance?.SetProgress(
                    $"Rule {selectionLadderRuleIndex + 1}/{LadderRules.Length}: {CurrentLadderRule.Name}");
            }
            else if (selectionCurrentMode == ModeNested)
            {
                // Godot: get_status_text()'s nested branch — like else-if it
                // omits the condition.
                DashboardController.Instance?.SetStatus(
                    $"Selection status: mode = nested if | value = {CurrentValueText()}" +
                    $" | result = {lastResultText}");

                DashboardController.Instance?.SetProgress(
                    $"Rule {selectionNestedRuleIndex + 1}/{NestedRules.Length}: {CurrentNestedRule.Name}");
            }
            else
            {
                DashboardController.Instance?.SetStatus(
                    $"Selection status: mode = basic if | value = {selectionBasicValue}" +
                    $" | condition: value {selectionBasicConditionText} | result = {lastResultText}");

                DashboardController.Instance?.SetProgress(
                    $"Rule {selectionBasicRuleIndex + 1}/{BasicRules.Length}: value {selectionBasicConditionText}");
            }

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
