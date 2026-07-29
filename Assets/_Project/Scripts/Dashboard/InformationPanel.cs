using UnityEngine;
using TMPro;

// Direct port of information_panel.gd.
// Attach this to the InformationPanel prefab (the root object,
// the same one that has the Vertical Layout Group / Content Size Fitter).
public class InformationPanel : MonoBehaviour
{
    [Header("Assign these in the Inspector")]
    [SerializeField] private TMP_Text headerLabel;   // was: $Content/HeaderLabel
    [SerializeField] private TMP_Text contentLabel;  // was: $Content/ContentLabel

    // Godot: func set_header(text_value: String) -> void
    public void SetHeader(string textValue)
    {
        headerLabel.text = textValue;
    }

    // Godot: func set_content(text_value: String) -> void
    public void SetContent(string textValue)
    {
        contentLabel.text = textValue;
    }
}
