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

    // Godot: func set_summary(text_value: String)
    public void SetSummary(string textValue) => summaryPanel.SetContent(textValue);

    // Godot: func set_instruction(text_value: String)
    public void SetInstruction(string textValue) => instructionPanel.SetContent(textValue);

    // Godot: func set_operation(text_value: String)
    public void SetOperation(string textValue) => operationPanel.SetContent(textValue);

    // Godot: func set_explanation(text_value: String)
    public void SetExplanation(string textValue) => explanationPanel.SetContent(textValue);

    // Godot: func set_status(text_value: String)
    public void SetStatus(string textValue) => statusPanel.SetContent(textValue);

    // Godot: func set_progress(text_value: String)
    public void SetProgress(string textValue) => progressPanel.SetContent(textValue);
}
