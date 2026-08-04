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
}
