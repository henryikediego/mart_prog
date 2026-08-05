using UnityEngine;

// Direct port of dashboard_controller.gd.
// Godot used an autoload (global singleton); Unity's equivalent is a
// singleton MonoBehaviour with a static Instance any script can reach,
// e.g.  DashboardController.Instance.SetSummary("...");
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
}
