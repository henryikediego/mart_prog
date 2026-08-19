using UnityEngine;
using TMPro;

// Direct port of array_cell.gd.
// Godot's Label3D becomes Unity's TextMeshPro (the 3D/world-space
// version — note this is different from TextMeshProUGUI, which is
// only for Canvas/UI text).
public class ArrayCell : MonoBehaviour
{
    [Header("Assign these in the Inspector")]
    [SerializeField] private TextMeshPro valueLabel;
    [SerializeField] private TextMeshPro indexLabel;

    // Godot: func set_data(value: int, index: int)
    public void SetData(int value, int index)
    {
        valueLabel.text = value.ToString();
        indexLabel.text = "Index " + index;
    }

    // Matches what array_module.gd's draw_array() actually does:
    // it sets the value directly and blanks the index label, since
    // separate floating index markers are used instead.
    public void SetValueOnly(string valueText)
    {
        valueLabel.text = valueText;
        valueLabel.fontStyle = FontStyles.Bold;
        valueLabel.outlineWidth = 0.25f;
        valueLabel.outlineColor = Color.black;

        indexLabel.text = "";
    }

    // Added for Phase 18 (Stack): stack_module.gd's search_value()/
    // traverse_stack() tint the VALUE LABEL's text color during their
    // highlight animation (_set_stack_label_color), not just the box's
    // material like array_module.gd's search_value()/traverse_array() do.
    // ArrayModule doesn't need this (it only recolors the box via its own
    // renderer-material helpers), so this wasn't required until Stack.
    // Kept here rather than duplicated per-module since ArrayCell is the
    // shared cell script for both.
    public void SetValueLabelColor(Color color)
    {
        if (valueLabel != null) valueLabel.color = color;
    }

    public void ResetValueLabelColor()
    {
        if (valueLabel != null) valueLabel.color = Color.white;
    }

    // Added for Phase 25 (Selection). That module sizes the value text to
    // fit its content — a result can be "?" or "Attendance too low" — via
    // selection_module.gd's _fit_selection_*_box_to_text() helpers. No
    // module before it varied font size at runtime, so this had no reason
    // to exist until now. Kept here rather than duplicated per-module, same
    // as SetValueLabelColor.
    //
    // Deliberately a MULTIPLIER, not an absolute size. Godot's helpers set
    // Label3D.font_size to values like 78 or 34, but Label3D pairs
    // font_size with pixel_size to reach a world size — it is not the same
    // unit as TextMeshPro.fontSize. Porting those numbers literally made
    // the text render several times too large for its box (found on-device
    // in Phase 25). What those tables actually encode is a RATIO — shrink
    // the text as the content gets longer — so that is what is ported,
    // applied against whatever size the prefab itself was authored at.
    private float defaultValueFontSize = -1f;

    public void SetValueFontScale(float multiplier)
    {
        if (valueLabel == null) return;

        if (defaultValueFontSize < 0f)
        {
            defaultValueFontSize = valueLabel.fontSize;
        }

        valueLabel.fontSize = defaultValueFontSize * multiplier;
    }

    public void ResetValueFontScale()
    {
        SetValueFontScale(1f);
    }
}
