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

    // ----- Accessibility text scale (Phase 42, no Godot equivalent) -----
    //
    // Backlog §4.4/§3.2: the one exception to "researcher sets it,
    // participant doesn't see it" — text size is inclusion, not a
    // confound. This is the dashboard half only; the AR labels (Array,
    // ArrayCell, Stack, Queue, LinkedList, Loop, Function, Selection —
    // eight files, each setting tmp.fontSize directly at creation time
    // with no shared hook) are a deliberately separate, larger follow-up
    // phase, not part of this one.
    //
    // Mirrors ArrayCell.SetValueFontScale()'s exact pattern: capture each
    // label's own authored font size the first time it's touched, then
    // always multiply from that captured baseline — never from the
    // label's current (possibly already-scaled) size. Absolute, not
    // cumulative: calling this twice with different multipliers does not
    // compound.
    //
    // Called exactly once per session, from
    // LandingPageController.OnStartLearningClicked() — see that method's
    // comment for why that click is the one guaranteed-safe moment to
    // call it, given DashboardContainer's own Start()-order (which wires
    // Initialize()) is not guaranteed. Deliberately NOT wired into
    // SetHeader()/SetContent(): TMP preserves fontSize across text
    // changes on its own, so re-applying the scale on every content
    // update would be redundant work for no behavioural difference.
    private float defaultHeaderFontSize = -1f;
    private float defaultContentFontSize = -1f;

    public void SetTextScale(float multiplier)
    {
        if (headerLabel != null)
        {
            if (defaultHeaderFontSize < 0f)
            {
                defaultHeaderFontSize = headerLabel.fontSize;
            }

            headerLabel.fontSize = defaultHeaderFontSize * multiplier;
        }

        if (contentLabel != null)
        {
            if (defaultContentFontSize < 0f)
            {
                defaultContentFontSize = contentLabel.fontSize;
            }

            contentLabel.fontSize = defaultContentFontSize * multiplier;
        }
    }
}
