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
    //
    // Phase 30 fix (F9): this and SetValueOnly() below were the only two
    // methods in this class that dereferenced the labels unguarded — every
    // other one already checked `if (valueLabel != null)` first. ArrayCell is
    // the shared cell script for EVERY visual module, and SetValueOnly() runs
    // on every redraw in seven of them, so an unassigned label on the prefab
    // would have thrown on the first cell drawn and left the module blank.
    // Same failure mode as the DashboardController incident: one missing
    // Inspector reference, no useful error, a whole screen dead.
    public void SetData(int value, int index)
    {
        if (valueLabel != null) valueLabel.text = value.ToString();
        if (indexLabel != null) indexLabel.text = "Index " + index;
    }

    // Matches what array_module.gd's draw_array() actually does:
    // it sets the value directly and blanks the index label, since
    // separate floating index markers are used instead.
    public void SetValueOnly(string valueText)
    {
        // Phase 30 fix (F9) — see the note on SetData() above.
        if (valueLabel != null)
        {
            valueLabel.text = valueText;
            valueLabel.fontStyle = FontStyles.Bold;
            valueLabel.outlineWidth = 0.25f;
            valueLabel.outlineColor = Color.black;

            // Phase 32 fix (F14) — DEVIATION from source, reported on device:
            // a long value spilled outside its box and became unreadable.
            //
            // Insert/Push/Enqueue accept anything int.TryParse takes, which is
            // up to 11 characters, while every cell font was authored for the
            // two-digit tutorial defaults (10/20/30). array_module.gd sets a
            // flat font_size = 96 with no length handling, so source overflows
            // the same way — but a number the student cannot read is broken
            // functionality, not a quirk worth preserving.
            //
            // The ratio table is NOT invented: it is Godot's own
            // get_value_font_size() from selection_module.gd (84/68/60/54 by
            // length), which is the one place source DID handle this. Applying
            // it here means every module inherits it in one place rather than
            // five modules each growing their own copy.
            //
            // Applied as a MULTIPLIER against the prefab's authored size for
            // the reason in SetValueFontScale() below — Label3D.font_size and
            // TextMeshPro.fontSize are not the same unit. SelectionModule
            // still calls SetValueFontScale() explicitly after this and wins,
            // since that call is absolute rather than cumulative.
            SetValueFontScale(FitScaleForLength(valueText));
        }

        if (indexLabel != null) indexLabel.text = "";
    }

    /// <summary>
    /// Godot: selection_module.gd's get_value_font_size(), ported as a ratio
    /// against the prefab's own authored font size. See SetValueOnly().
    /// </summary>
    private static float FitScaleForLength(string text)
    {
        if (string.IsNullOrEmpty(text)) return 1f;
        if (text == "?") return 1f;
        if (text.Length <= 2) return 1f;
        if (text.Length <= 4) return 68f / 84f;
        if (text.Length <= 5) return 60f / 84f;
        if (text.Length <= 8) return 44f / 84f;
        return 34f / 84f;
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

    // Added for Phase 25c (Selection, Nested If). SelectionModule widens the
    // RESULT box for long results like "Exam score too low" by scaling the
    // cell's X axis — but this label is a CHILD of the cell, so that scale
    // propagates into the glyphs and smears them horizontally. Counter-
    // scaling the label by the inverse keeps the text correctly proportioned
    // inside a wider box.
    //
    // Pass the parent's X growth factor (1 = no growth). Absolute, not
    // cumulative: calling it repeatedly with different values is safe.
    private Vector3 defaultValueLabelScale = Vector3.zero;

    public void SetValueLabelCounterScaleX(float parentXGrowth)
    {
        if (valueLabel == null) return;

        if (defaultValueLabelScale == Vector3.zero)
        {
            defaultValueLabelScale = valueLabel.transform.localScale;
        }

        float safeGrowth = Mathf.Approximately(parentXGrowth, 0f) ? 1f : parentXGrowth;

        valueLabel.transform.localScale = new Vector3(
            defaultValueLabelScale.x / safeGrowth,
            defaultValueLabelScale.y,
            defaultValueLabelScale.z);
    }
}
