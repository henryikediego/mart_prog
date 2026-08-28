using UnityEngine;

// Direct port of dashboard_controller.gd.
// Godot used an autoload (global singleton); Unity's equivalent is a
// singleton MonoBehaviour with a static Instance any script can reach,
// e.g.  DashboardController.Instance.SetSummary("...");
//
// =====================================================================
// DASHBOARD PANEL CONTRACT  (added Phase 26b, after an audit)
// =====================================================================
//
// Godot has SIX labels stacked in one container. Unity has SIX permanent
// panels, each with its own visible HEADING. They map one-to-one:
//
//   Godot label                  Unity panel     Contains
//   ---------------------------  --------------  --------------------------
//   module_title_label           (not shown)     Dropped — see rule 1
//   module_description_label     Summary         The module's own
//                                                one-sentence description
//   code_label                   Instruction     What to do next
//   code_explanation_label  \
//     (first line)           >   Operation       What just happened
//     (second line)          >   Explanation     The concept behind it
//   index_explanation_label      Status          Current data state
//   operation_feedback_label     Progress        Progress / counters
//
// THREE RULES, all of which were being broken somewhere before the audit:
//
// 1. **Summary is the description sentence, and nothing else.** Godot
//    writes module_title_label AND module_description_label. Six of the
//    seven controllers built before this audit passed only the TITLE, so a
//    panel headed "Summary" contained no summary. The first fix passed
//    title + "\n" + description; the title was then dropped entirely,
//    because the Change Module selector already names the module and the
//    panel heading already says what it is. Pass the description only.
//
// 2. **Strip Godot's inline prefixes.** Godot's strings begin with
//    "Instruction: ", "Operation: " and "Explanation: " because its labels
//    are bare — nothing else would say which is which. Unity's panels are
//    headed, so keeping the prefix renders as "Instruction / Instruction:
//    Choose an activity...". Pass the text WITHOUT the prefix. (Reported
//    from device on Memory, which was the one controller that kept it.)
//
// 3. **No panel is ever left empty, including on entry.** Godot hides a
//    label it has nothing for, and its container shrinks to fit. Unity's
//    panels are permanent, so an empty one is a headed box with nothing in
//    it, which reads as a bug rather than as absence. Where Godot hides,
//    Unity supplies the truthful default: "No operation selected yet." for
//    Operation, and the real (often zero-valued) status and progress lines.
//    LinkedListController and FunctionsController each blanked Status in
//    EnterModule() and only filled it after the first operation; both now
//    populate it on entry.
//
// A PANEL NEVER REPEATS ANOTHER PANEL. Godot stacks all six strings in one
// label, so it happily builds a block that includes text already shown
// above it — there is no "above it". Unity's panels are separate and
// headed, so the same composition prints the sentence twice. Anything that
// assembles one panel's text must therefore EXCLUDE what another panel
// already owns. MemoryController.GetModeDetailsBlock() is where this bit:
// it ported Godot's habit of leading the Operation block with the
// explanation text, which the Explanation panel was already displaying.
//
// A NOTE ON WHICH PANEL CHANGES WHEN. Summary is constant for a module.
// Instruction changes with the mode/activity. Explanation changes with the
// mode/activity AND with the last action. Operation changes with every
// action. Status and Progress track data state. Memory shipped with Summary
// and Explanation swapped, which froze the dashboard — nothing appeared to
// change as activities changed. If a panel ever looks static, check this
// list first.
//
// Anything ported into these panels should be checked against
// ar_learning_screen.gd's update_<module>_dashboard() function, which is
// where all eight modules' six strings actually live.
public class DashboardController : MonoBehaviour
{
    public static DashboardController Instance { get; private set; }

    private InformationPanel summaryPanel;
    private InformationPanel instructionPanel;
    private InformationPanel operationPanel;
    private InformationPanel explanationPanel;
    private InformationPanel statusPanel;
    private InformationPanel progressPanel;

    private void Awake()
    {
        // Standard Unity singleton guard: if one already exists
        // (e.g. from re-entering the scene), destroy the duplicate.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    // Godot: func initialize(summary, instruction, operation, explanation, status, progress)
    public void Initialize(
        InformationPanel summary,
        InformationPanel instruction,
        InformationPanel operation,
        InformationPanel explanation,
        InformationPanel status,
        InformationPanel progress)
    {
        summaryPanel = summary;
        instructionPanel = instruction;
        operationPanel = operation;
        explanationPanel = explanation;
        statusPanel = status;
        progressPanel = progress;

        // Phase 42 fix: self-apply the moment wiring finishes, rather than
        // trusting a caller elsewhere to have already pushed the right
        // value in. See ApplySavedTextScale()'s comment for why that
        // trust turned out to be misplaced.
        ApplySavedTextScale();
    }

    // Fix (Phase 17): these six panel references are only wired up by an
    // external Initialize() call (typically from DashboardContainer.Start()),
    // not in this component's own Awake()/Start(). Unity does not guarantee
    // that DashboardContainer's Start() runs before some other script's
    // Start() calls one of the Set* methods below in the same frame -- and
    // that race is exactly what produced a NullReferenceException here once
    // AdminController joined the scene and shifted Start() ordering enough
    // for ArrayListController.Start() to occasionally run first. The real
    // fix on the caller side is to defer any Start()-time Set* call by one
    // frame (see ArrayListController's InitializeDeferred()) -- these guards
    // are a second line of defense so a future controller that forgets to
    // do that fails safely (a console warning) instead of crashing.

    // Godot: func set_summary(text_value: String)
    public void SetSummary(string textValue)
    {
        if (summaryPanel == null)
        {
            Debug.LogWarning("DashboardController: SetSummary() called before Initialize() finished wiring panels.");
            return;
        }

        summaryPanel.SetContent(textValue);
    }

    // Godot: func set_instruction(text_value: String)
    public void SetInstruction(string textValue)
    {
        if (instructionPanel == null)
        {
            Debug.LogWarning("DashboardController: SetInstruction() called before Initialize() finished wiring panels.");
            return;
        }

        instructionPanel.SetContent(textValue);
    }

    // Godot: func set_operation(text_value: String)
    public void SetOperation(string textValue)
    {
        if (operationPanel == null)
        {
            Debug.LogWarning("DashboardController: SetOperation() called before Initialize() finished wiring panels.");
            return;
        }

        operationPanel.SetContent(textValue);
    }

    // Godot: func set_explanation(text_value: String)
    public void SetExplanation(string textValue)
    {
        if (explanationPanel == null)
        {
            Debug.LogWarning("DashboardController: SetExplanation() called before Initialize() finished wiring panels.");
            return;
        }

        explanationPanel.SetContent(textValue);
    }

    // Godot: func set_status(text_value: String)
    public void SetStatus(string textValue)
    {
        if (statusPanel == null)
        {
            Debug.LogWarning("DashboardController: SetStatus() called before Initialize() finished wiring panels.");
            return;
        }

        statusPanel.SetContent(textValue);
    }

    // Godot: func set_progress(text_value: String)
    public void SetProgress(string textValue)
    {
        if (progressPanel == null)
        {
            Debug.LogWarning("DashboardController: SetProgress() called before Initialize() finished wiring panels.");
            return;
        }

        progressPanel.SetContent(textValue);
    }

    // ----- Accessibility text scale (Phase 42, no Godot equivalent) -----
    //
    // Broadcasts to all six panels in one call.
    //
    // CORRECTED after the first real test. This was originally written to
    // be called exactly once, from
    // LandingPageController.OnStartLearningClicked(), on the theory that a
    // user-triggered click is necessarily later than every MonoBehaviour's
    // own Awake()/Start() for that frame — so it would always land after
    // DashboardContainer.Start() had already called Initialize() above.
    // That reasoning was wrong: a real Play-mode test hit the warning
    // below on that exact click, meaning Initialize() had NOT run yet —
    // whatever activates the Dashboard/DashboardContainer hierarchy
    // apparently happens later than the Landing Page's own Start Learning
    // click, not before it (unsurprising in hindsight, given
    // ChangeModuleController is documented elsewhere as keeping "the
    // module world" hidden at boot — DashboardContainer.cs itself was
    // never supplied to confirm the exact mechanism, so this is inferred
    // from the observed warning, not read from that file).
    //
    // Fixed by no longer depending on being called at the right moment at
    // all: Initialize() above now calls ApplySavedTextScale() itself, the
    // instant the panels are wired, regardless of when that turns out to
    // be. LandingPageController.OnStartLearningClicked() still calls this
    // method directly too — now a live re-apply for the (normal) case
    // where Initialize() already ran and the participant is pressing Start
    // Learning again after changing their choice, not the only path that
    // makes the scale take effect.
    public void SetTextScale(float multiplier)
    {
        if (summaryPanel == null)
        {
            // Deliberately silent here, unlike the six Set*() methods
            // above. A call landing before Initialize() isn't lost the way
            // a dropped SetSummary()/etc. call would be: Initialize()
            // itself calls ApplySavedTextScale() the moment it finishes
            // wiring, so whatever this call would have set gets applied
            // moments later regardless, from the same saved value. Given
            // this is now a confirmed-normal occurrence on the very first
            // Start Learning click (see above), warning on it here would
            // fire on every ordinary run, not just a real ordering bug.
            return;
        }

        summaryPanel.SetTextScale(multiplier);
        instructionPanel.SetTextScale(multiplier);
        operationPanel.SetTextScale(multiplier);
        explanationPanel.SetTextScale(multiplier);
        statusPanel.SetTextScale(multiplier);
        progressPanel.SetTextScale(multiplier);
    }

    private const string TextScalePrefsKey = "MartProg_TextScale";

    // Pulls the currently saved accessibility scale and applies it,
    // instead of waiting to be pushed one by a caller whose timing isn't
    // guaranteed (see SetTextScale()'s comment). Written by
    // LandingPageController.OnTextScaleClicked()/Start() as a plain float,
    // not the Normal/Large/Extra-Large index — this class has no reason to
    // know about that naming, only the resulting multiplier. Defaults to
    // 1.0 (Normal) if nothing has been saved yet, which is also the
    // correct behaviour on a fresh install before the Landing Page has
    // ever written the key.
    private void ApplySavedTextScale()
    {
        float savedScale = PlayerPrefs.GetFloat(TextScalePrefsKey, 1.0f);
        SetTextScale(savedScale);
    }
}
