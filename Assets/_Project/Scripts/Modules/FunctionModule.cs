using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using MartProg.Menu;

namespace MartProg.Modules
{
    /// <summary>
    /// Phase 23: the Functions module's 3D visual, ported from
    /// function_module.gd — read directly from source for this phase, not
    /// from any earlier summary or by analogy with the modules already
    /// built.
    ///
    /// The visual is a left-to-right pipeline: two input value boxes (a and
    /// b), an arrow, a dark function panel showing e.g. "add(a, b)", a
    /// second arrow, and an output box showing the return value — with
    /// INPUTS / FUNCTION / OUTPUT headings above and small a / b / return
    /// captions below. Structurally different from every module before it
    /// (no list of cells, no index markers), so almost none of
    /// ArrayModule/StackModule/QueueModule/LinkedListModule's layout logic
    /// carries over; what does carry over is the AR-scaling approach and the
    /// coroutine-based tween helpers.
    ///
    /// Porting decisions and preserved/adapted source quirks, all confirmed
    /// by reading function_module.gd and ar_learning_screen.gd directly:
    ///
    /// 1. **A large amount of function_module.gd is dead code and is NOT
    ///    ported.** Verified unreachable both from draw_function() (the only
    ///    thing that actually builds the visual) and from every
    ///    function_module.* call site in ar_learning_screen.gd:
    ///    - create_value_cell() — never called. draw_function() instantiates
    ///      and configures its cells inline instead.
    ///    - create_function_block() — never called. draw_function() uses
    ///      add_function_panel() instead. This is why function_label is
    ///      always null in practice, and why get_function_block_width()
    ///      (also never called) is dead too.
    ///    - add_marker() — only ever called by create_value_cell(), so dead
    ///      by extension. add_text_label() is what draw_function() uses.
    ///    - add_arrow() — never called; add_arrow_label() is used instead.
    ///      Note the two draw different glyphs ("➜" vs "→"); the live one
    ///      draws "→", so that is what this port draws.
    ///    - set_values(), set_data(), set_output_value() — self-described
    ///      "compatibility" aliases. ar_learning_screen.gd calls
    ///      set_input_values() (which forwards to set_parameters()), so only
    ///      that path is ported.
    ///    - The constants FUNCTION_INPUT_BOX_WIDTH, FUNCTION_OUTPUT_BOX_WIDTH,
    ///      FUNCTION_INPUT_GAP, FUNCTION_SECTION_GAP, FUNCTION_VISUAL_Y and
    ///      FUNCTION_ARROW_Z — none are referenced anywhere, including by the
    ///      dead functions. FUNCTION_SECTION_GAP even carries a comment
    ///      calling it "the important value", but nothing reads it; the real
    ///      layout comes from the hardcoded x positions in draw_function().
    ///    - The $Inputs, $FunctionBox, $Output and $Markers node references —
    ///      $Inputs and $Output are never touched at all; $FunctionBox and
    ///      $Markers are written to only by the dead functions above. Only
    ///      $Cells and $Labels are actually used, so this port has only
    ///      cellsParent and labelsParent.
    ///    Same treatment Phase 21 gave LinkedListModule's dead code: left
    ///    out, and listed here so the omission is a recorded decision rather
    ///    than something that looks overlooked.
    ///
    /// 2. **Three identical result calculators collapsed into one.**
    ///    calculate_function_result(), get_function_result() and
    ///    calculate_result(function_name) all contain the same add/subtract/
    ///    multiply switch with the same "default to add" fallback; the first
    ///    two take no argument and read current_function_name, the third
    ///    takes the name as a parameter. This port keeps one method,
    ///    CalculateResult(string), and calls it with CurrentFunctionName
    ///    where the no-argument versions were used. Pure de-duplication — no
    ///    behavior change is possible, since all three bodies are identical.
    ///
    /// 3. **The numeric-label lookup heuristic is NOT ported; direct
    ///    references are used instead.** To update the input boxes after
    ///    Set Input, source calls _get_sorted_function_numeric_labels(),
    ///    which walks the ENTIRE node tree collecting every Label3D whose
    ///    text happens to parse as an integer, sorts them by global X, and
    ///    then assumes index 0 is a, index 1 is b, and the last one is the
    ///    output. That works only because no other numeric label exists in
    ///    the scene right now — it would silently mis-target the moment one
    ///    did (a numeric heading, a value in a caption, anything). Since
    ///    DrawFunction() creates these three boxes itself, this port simply
    ///    keeps direct references to them. Same visible result, and it can't
    ///    be broken by unrelated labels. Recorded as a deliberate
    ///    improvement, in the same spirit as Phase 21 correcting Linked
    ///    List's Reset call rather than porting it faithfully broken.
    ///
    /// 4. **Set Input deliberately updates ONLY the input boxes, never the
    ///    output.** set_parameters() calls _update_function_input_numbers()
    ///    and nothing else, with a source comment stating the intent
    ///    outright, and ar_learning_screen.gd's Set Input handler tells the
    ///    student "the return/output value will be updated after you choose
    ///    Add, Subtract, or Multiply." Preserved exactly: SetParameters()
    ///    below refreshes the two input labels and leaves the output box
    ///    showing the previous result until a function is actually called.
    ///
    /// 5. **get_function_font_size() takes a function_text argument it never
    ///    reads** — it switches on current_function_name instead. Preserved
    ///    as a parameterless method here, since the parameter was inert.
    ///    The sizes themselves (30 for add, 25 for subtract/multiply, 28 for
    ///    the unreachable default) are ported as-is.
    ///
    /// 6. **Godot's absolute center_y (2.45) is not ported**, same call made
    ///    for every module since ArrayModule: those world coordinates suit
    ///    Godot's fixed camera, not AR, where content is placed relative to
    ///    the AR origin. arHeight/arDistance/arScale replace it. The
    ///    horizontal layout (input_a_x -3.90, input_b_x -2.45, function_x
    ///    0.05, output_x 2.60) and every relative offset ARE ported exactly,
    ///    scaled by arScale.
    ///
    /// 7. **Source's per-element Z depths collapse to one tunable.**
    ///    draw_function() places elements at six slightly different Z values
    ///    (headings 0.55, arrows 0.40, captions 0.30, function text 0.15,
    ///    panel 0.02, boxes 0). All that matters is that the flat labels
    ///    render clear of the boxes and that the function text sits in front
    ///    of its panel, so — exactly as Phase 21 did for LinkedListModule's
    ///    three arrow/marker depths — they collapse into labelForwardOffset,
    ///    with the panel left at box depth so its text still floats in front
    ///    of it.
    ///
    /// 8. **The function panel builds its own URP material at runtime.**
    ///    Godot's add_function_panel() creates a StandardMaterial3D; the
    ///    direct Unity equivalent would be GameObject.CreatePrimitive(), but
    ///    that assigns the BUILT-IN Standard shader, which this project's URP
    ///    setup cannot render — the panel comes out solid magenta (confirmed
    ///    on-device during Phase 23 bring-up). This is the first module to
    ///    generate a mesh at runtime rather than instantiate the shared
    ///    ArrayCell prefab, which is why nothing before it hit this.
    ///    BuildPanelMaterial() below resolves a URP shader instead, with a
    ///    Sprites/Default fallback that exists in every pipeline, and an
    ///    optional Inspector Material field to override the whole thing.
    ///    Unlit is preferred over Lit because the AR scene has no controlled
    ///    lighting — a lit panel would read as a different shade depending on
    ///    the student's room, which Godot's fixed-lighting scene never had to
    ///    worry about.
    ///
    /// 9. **The layout is re-centred; source's is not centred.** Godot's
    ///    hardcoded positions span x = -3.90 (input a) to x = +2.60 (output),
    ///    whose midpoint is -0.65, so the whole pipeline sits noticeably left
    ///    of its own origin. That is invisible in Godot — the scene camera is
    ///    framed to suit it — but in AR it means the content hangs off to one
    ///    side of wherever the student is looking, and every other module in
    ///    this project centres its content on the AR origin. layoutXOffset
    ///    defaults to +0.65 to cancel exactly that skew, and is applied
    ///    BEFORE spacingMultiplier so the layout stays centred at any spacing.
    ///    Set it to 0 to reproduce source's own off-centre placement. Same
    ///    class of AR adaptation as note 6's treatment of center_y.
    ///
    /// 10. **labelForwardOffset must stay small, for a reason that isn't
    ///     obvious.** It defaults to 0.02, not the 0.08 LinkedListModule uses
    ///     for its markers. Pulling a label toward the camera also moves it
    ///     DOWN in screen space whenever the content is viewed from above —
    ///     which is the normal way AR content sitting at or below eye level
    ///     gets looked at. On LinkedListModule that goes unnoticed because
    ///     its markers sit above the boxes anyway, so a small downward shift
    ///     reads as nothing. Here, the two arrows and the function name sit
    ///     at exactly box height by design, so the same shift reads as them
    ///     sagging below the boxes they are supposed to line up with — which
    ///     is precisely how it was reported during Phase 23 on-device
    ///     testing, as "not vertically centered to match the boxes". It is a
    ///     parallax artifact of the forward offset, not a centring bug:
    ///     nothing is mis-positioned in Y, and no vertical correction is
    ///     wanted. The panel is only ~0.013m thick at default scale, so
    ///     0.02 clears it with room to spare while keeping the shift
    ///     imperceptible. If arrows/text ever look low again, reduce this
    ///     before touching any Y value.
    /// </summary>
    public class FunctionModule : MonoBehaviour
    {
        [Header("Assign in the Inspector")]
        [Tooltip("Same ArrayCell prefab used by every other module — the value box visuals are identical.")]
        [SerializeField] private GameObject cellPrefab;      // was: @export var cell_scene
        [Tooltip("Container for the three value boxes and the function panel.")]
        [SerializeField] private Transform cellsParent;      // was: $Cells
        [Tooltip("Container for all flat text: headings, captions, arrows, and the function name.")]
        [SerializeField] private Transform labelsParent;     // was: $Labels

        [Header("AR placement tuning (Godot units are too big for real-world AR)")]
        [Tooltip("Master multiplier shrinking the whole Godot-scale layout into real-world metres. Lower = smaller/closer together.")]
        [SerializeField] private float arScale = 0.12f;
        [Tooltip("How far in front of the AR origin (metres) the function pipeline sits.")]
        [SerializeField] private float arDistance = 0.5f;
        [Tooltip("Height (metres) relative to the AR origin. Godot's absolute center_y (2.45) is intentionally not ported — see class comment note 6.")]
        [SerializeField] private float arHeight = 0f;
        [Tooltip("Grows/shrinks the value boxes (and their text) without changing the spacing between them.")]
        [SerializeField] private float cellSizeMultiplier = 1.4f;
        [Tooltip("Tightens or widens the whole left-to-right layout without changing box size. " +
                 "This module is far wider than any other (five elements in a row rather than a " +
                 "centred group of boxes), so it needs a smaller value than the others to fit a " +
                 "phone screen at arm's length.")]
        [SerializeField] private float spacingMultiplier = 0.55f;
        [Tooltip("Shifts the whole layout horizontally, in pre-arScale Godot units, BEFORE " +
                 "Spacing Multiplier is applied. Defaults to +0.65, which centres the pipeline — " +
                 "see class comment note 9. Set to 0 to reproduce source's own off-centre layout.")]
        [SerializeField] private float layoutXOffset = 0.65f;

        [Header("Label appearance")]
        [Tooltip("Pulls all flat text (headings, captions, arrows, function name) toward the camera, " +
                 "clear of box/panel occlusion — see class comment note 7. Keep this SMALL. Anything " +
                 "nearer the camera projects lower on screen when the content is viewed from above, " +
                 "so a large value makes the arrows and the function name appear to sag below the " +
                 "boxes they should line up with (Phase 23 on-device finding, note 10). The panel is " +
                 "only about 0.013m thick at default scale, so very little is needed to clear it.")]
        [SerializeField] private float labelForwardOffset = 0.02f;
        [Tooltip("Base scale for flat text objects, before arScale/cellSizeMultiplier.")]
        [SerializeField] private float labelBaseScale = 0.06f;

        [Header("Function panel appearance")]
        [Tooltip("Godot: add_function_panel()'s albedo_color of (0.06, 0.06, 0.06).")]
        [SerializeField] private Color functionPanelColor = new Color(0.06f, 0.06f, 0.06f, 1f);
        [Tooltip("Godot: panel_size of (2.0, 0.50, 0.08), in pre-arScale Godot units.")]
        [SerializeField] private Vector3 functionPanelSize = new Vector3(2.0f, 0.50f, 0.08f);
        [Tooltip("OPTIONAL. Leave empty and a URP-compatible material is built at runtime " +
                 "(see class comment note 8). Assign one only if you want the panel to use a " +
                 "specific material asset — its colour then comes from that material, not from " +
                 "Function Panel Color above.")]
        [SerializeField] private Material functionPanelMaterial;

        // ----- Source layout constants, ported exactly (pre-arScale Godot units) -----
        private const float InputAX = -3.90f;
        private const float InputBX = -2.45f;
        private const float FunctionX = 0.05f;
        private const float OutputX = 2.60f;

        private const float InputCellScale = 0.62f;
        private const float OutputCellScale = 0.82f;

        private const float HeadingYOffset = 0.78f;    // center_y + 0.78
        private const float CaptionYOffset = -0.80f;   // center_y - 0.80
        private const float ArrowLeftX = -1.30f;
        private const float ArrowRightX = 1.30f;

        private static readonly Color InputColor = new Color(0.3f, 0.8f, 1.0f, 1f);
        private static readonly Color FunctionColor = new Color(1.0f, 0.9f, 0.1f, 1f);
        private static readonly Color OutputColor = new Color(0.3f, 1.0f, 0.5f, 1f);

        // ----- Runtime state (Godot's module-level vars) -----
        private int parameterA = 10;
        private int parameterB = 20;
        private int resultValue = 30;
        private string currentFunctionName = "add";
        private bool isAnimating;

        // Direct references replacing source's numeric-label search — note 3.
        private ArrayCell inputCellA;
        private ArrayCell inputCellB;
        private ArrayCell outputCell;
        private Transform functionPanel;
        private readonly List<Transform> inputCellTransforms = new List<Transform>();

        // ----- Read-only accessors for FunctionsController -----
        public int ParameterA => parameterA;
        public int ParameterB => parameterB;
        public int ResultValue => resultValue;
        public string CurrentFunctionName => currentFunctionName;
        public bool IsAnimating => isAnimating;

        private void Start()
        {
            DrawFunction();
        }

        /// <summary>
        /// Converts a source X coordinate into a local Unity X, applying the
        /// centring offset first and the spacing multiplier second — so the
        /// layout stays centred at whatever spacing you tune it to. See class
        /// comment note 9.
        /// </summary>
        private float LayoutX(float preScaleX)
        {
            return (preScaleX + layoutXOffset) * spacingMultiplier * arScale;
        }

        // Godot: func clear_visuals()
        private void ClearVisuals()
        {
            ClearContainer(cellsParent);
            ClearContainer(labelsParent);

            inputCellA = null;
            inputCellB = null;
            outputCell = null;
            functionPanel = null;
            inputCellTransforms.Clear();
        }

        private void ClearContainer(Transform container)
        {
            if (container == null) return;

            List<Transform> children = new List<Transform>();
            for (int i = 0; i < container.childCount; i++)
            {
                children.Add(container.GetChild(i));
            }

            foreach (Transform child in children)
            {
                Destroy(child.gameObject);
            }
        }

        // Godot: func get_function_display_text() -> String
        private string GetFunctionDisplayText()
        {
            switch (currentFunctionName)
            {
                case "add": return "add(a, b)";
                case "subtract": return "subtract(a, b)";
                case "multiply": return "multiply(a, b)";
                default: return "add(a, b)";
            }
        }

        // Godot: func get_function_font_size(function_text: String) -> int
        // The source parameter is never read — see class comment note 5.
        private int GetFunctionFontSize()
        {
            switch (currentFunctionName)
            {
                case "add": return 30;
                case "subtract": return 25;
                case "multiply": return 25;
                default: return 28;
            }
        }

        // Godot: calculate_function_result() / get_function_result() /
        // calculate_result(function_name) — all three collapsed, see note 2.
        private int CalculateResult(string functionName)
        {
            switch (functionName)
            {
                case "add": return parameterA + parameterB;
                case "subtract": return parameterA - parameterB;
                case "multiply": return parameterA * parameterB;
                default: return parameterA + parameterB;
            }
        }

        // Godot: func draw_function(force_redraw := false)
        public void DrawFunction(bool forceRedraw = false)
        {
            if (isAnimating && !forceRedraw)
            {
                return;
            }

            ClearVisuals();

            resultValue = CalculateResult(currentFunctionName);

            // Phase 47 on-device fix: on a narrower device, "FUNCTION" and
            // "OUTPUT" rendered with no gap between them at Extra Large,
            // and the arrow beside the FUNCTION panel overlapped the
            // display text's closing parenthesis ("add(a, b)"). Neither
            // InputAX/InputBX/FunctionX/OutputX nor ArrowLeftX/ArrowRightX
            // move — this module's panel is deliberately NOT resized (see
            // class comment) — so instead the header labels and the arrow
            // glyphs grow at HALF the accessibility scale's rate, same as
            // every other module's section headers (see
            // MemoryModule.AddHeaderRow() for the fuller reasoning). The
            // function-name text inside the panel and the a/b/return
            // values keep the full rate — those are what a participant
            // actually reads closely.
            float headerTextScale = 1f + (LandingPageController.CurrentTextScale - 1f) * 0.5f;

            // ----- Section headings -----
            AddTextLabel("INPUTS", (InputAX + InputBX) / 2f, HeadingYOffset, 30, InputColor, headerTextScale);
            AddTextLabel("FUNCTION", FunctionX, HeadingYOffset, 30, FunctionColor, headerTextScale);
            AddTextLabel("OUTPUT", OutputX, HeadingYOffset, 30, OutputColor, headerTextScale);

            // ----- Input a -----
            inputCellA = CreateValueBox("FunctionInput_A", InputAX, InputCellScale, parameterA);
            if (inputCellA != null) inputCellTransforms.Add(inputCellA.transform);
            AddTextLabel("a", InputAX, CaptionYOffset, 40, InputColor);

            // ----- Input b -----
            inputCellB = CreateValueBox("FunctionInput_B", InputBX, InputCellScale, parameterB);
            if (inputCellB != null) inputCellTransforms.Add(inputCellB.transform);
            AddTextLabel("b", InputBX, CaptionYOffset, 40, InputColor);

            // ----- Function panel + its label -----
            AddFunctionPanel();
            AddTextLabel(GetFunctionDisplayText(), FunctionX, 0f, GetFunctionFontSize(), Color.white);

            // ----- Output / return value -----
            outputCell = CreateValueBox("FunctionOutput", OutputX, OutputCellScale, resultValue);
            AddTextLabel("return", OutputX, CaptionYOffset, 35, InputColor);

            // ----- Arrows -----
            AddArrowLabel(ArrowLeftX, headerTextScale);
            AddArrowLabel(ArrowRightX, headerTextScale);
        }

        /// Instantiates one value box. Replaces the inline cell setup
        /// draw_function() repeats three times (source's create_value_cell()
        /// is dead code and is NOT what draw_function() uses — see note 1).
        private ArrayCell CreateValueBox(string objectName, float preScaleX, float cellScale, int value)
        {
            if (cellPrefab == null || cellsParent == null) return null;

            GameObject cell = Instantiate(cellPrefab, cellsParent);
            cell.name = objectName;
            cell.transform.localPosition = new Vector3(LayoutX(preScaleX), arHeight, arDistance);
            cell.transform.localScale = Vector3.one * cellScale * arScale * cellSizeMultiplier;
            cell.transform.localRotation = Quaternion.identity;

            // Same defensive billboard add every module since Phase 18 uses.
            if (cell.GetComponent<Billboard>() == null)
            {
                cell.AddComponent<Billboard>();
            }

            ArrayCell arrayCell = cell.GetComponent<ArrayCell>();
            if (arrayCell != null)
            {
                // Source sets the value label and blanks the index label —
                // exactly what SetValueOnly does.
                arrayCell.SetValueOnly(value.ToString());
            }

            return arrayCell;
        }

        // Godot: func add_text_label(label_text, label_position, font_size_value, label_color)
        // Phase 47: textScaleOverride lets a caller (section headers,
        // arrows) opt out of the full CurrentTextScale multiplier — see
        // DrawFunction()'s headerTextScale for why. null (the default)
        // preserves every existing caller's Phase 45 behaviour exactly.
        private void AddTextLabel(string text, float preScaleX, float preScaleYOffset, int fontSize, Color color, float? textScaleOverride = null)
        {
            if (labelsParent == null) return;

            GameObject labelObj = new GameObject("FunctionLabel_" + text);
            labelObj.transform.SetParent(labelsParent, false);
            labelObj.transform.localPosition = new Vector3(
                LayoutX(preScaleX),
                arHeight + preScaleYOffset * arScale,
                arDistance - labelForwardOffset);
            labelObj.transform.localScale = Vector3.one * labelBaseScale * arScale * cellSizeMultiplier;
            labelObj.transform.localRotation = Quaternion.identity;

            TextMeshPro tmp = labelObj.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = fontSize * (textScaleOverride ?? LandingPageController.CurrentTextScale); // Phase 45/47
            tmp.color = color;
            tmp.fontStyle = FontStyles.Bold;
            tmp.outlineWidth = 0.25f;
            tmp.outlineColor = Color.black;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = false; // Phase 45 fix — see ArrayModule.CreateIndexMarker()

            labelObj.AddComponent<Billboard>();
        }

        // Godot: func add_arrow_label(arrow_position). Draws "→" — the glyph
        // the LIVE code path uses; dead add_arrow() draws "➜" (note 1).
        private void AddArrowLabel(float preScaleX, float? textScaleOverride = null)
        {
            AddTextLabel("→", preScaleX, 0f, 40, Color.white, textScaleOverride);
        }

        // Godot: func add_function_panel(panel_position, panel_size)
        private void AddFunctionPanel()
        {
            if (cellsParent == null) return;

            GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = "FunctionPanel";

            // The panel is decorative — no collider, matching every other
            // generated visual in this project.
            Collider panelCollider = panel.GetComponent<Collider>();
            if (panelCollider != null) Destroy(panelCollider);

            panel.transform.SetParent(cellsParent, false);
            panel.transform.localPosition = new Vector3(LayoutX(FunctionX), arHeight, arDistance);
            panel.transform.localScale = new Vector3(
                functionPanelSize.x * arScale * cellSizeMultiplier,
                functionPanelSize.y * arScale * cellSizeMultiplier,
                functionPanelSize.z * arScale * cellSizeMultiplier);
            panel.transform.localRotation = Quaternion.identity;

            Renderer panelRenderer = panel.GetComponent<Renderer>();
            if (panelRenderer != null)
            {
                panelRenderer.material = BuildPanelMaterial();
            }

            functionPanel = panel.transform;
        }

        /// <summary>
        /// GameObject.CreatePrimitive() assigns Unity's BUILT-IN Standard
        /// shader, which this project's URP setup cannot render — the panel
        /// comes out magenta (see class comment note 8). This builds a
        /// URP-compatible material instead, falling back through the URP
        /// shader names and finally to Sprites/Default, which exists in every
        /// pipeline. Godot's own material sets albedo + roughness 0.85 with
        /// default (lit) shading; Unlit is preferred here because the AR scene
        /// has no controlled lighting, so a lit panel would read as a
        /// different shade depending on the student's room.
        /// </summary>
        private Material BuildPanelMaterial()
        {
            if (functionPanelMaterial != null)
            {
                // Instanced so tweaking one panel never edits the shared asset.
                return new Material(functionPanelMaterial);
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            Material material = new Material(shader);
            material.color = functionPanelColor;
            return material;
        }

        // Godot: func set_parameters(a_value: int, b_value: int)
        // Updates ONLY the input boxes — see class comment note 4.
        public int SetParameters(int aValue, int bValue)
        {
            if (isAnimating)
            {
                return resultValue;
            }

            parameterA = aValue;
            parameterB = bValue;

            if (inputCellA != null) inputCellA.SetValueOnly(parameterA.ToString());
            if (inputCellB != null) inputCellB.SetValueOnly(parameterB.ToString());

            return resultValue;
        }

        // Godot: func set_input_values(a: int, b: int) — the one alias
        // ar_learning_screen.gd actually calls; forwards to set_parameters().
        public int SetInputValues(int a, int b)
        {
            return SetParameters(a, b);
        }

        /// Godot: func call_function(function_name := "add")
        /// Recalculates, redraws for the new function name, then plays the
        /// highlight sequence: each input box in turn, then the function
        /// panel, then the output box.
        public IEnumerator CallFunction(string functionName, Action<int> onComplete = null)
        {
            if (isAnimating)
            {
                onComplete?.Invoke(resultValue);
                yield break;
            }

            currentFunctionName = functionName;
            resultValue = CalculateResult(currentFunctionName);

            // Redraw picks up both the new function name and the new output.
            DrawFunction(true);

            if (functionPanel == null || outputCell == null)
            {
                onComplete?.Invoke(resultValue);
                yield break;
            }

            isAnimating = true;

            // Highlight input cells left to right.
            foreach (Transform cell in inputCellTransforms)
            {
                if (cell == null) continue;

                Vector3 originalScale = cell.localScale;

                yield return ScaleTo(cell, originalScale * 1.18f, 0.18f, true);
                yield return new WaitForSeconds(0.10f);
                yield return ScaleTo(cell, originalScale, 0.18f, false);
            }

            // Highlight the function panel.
            if (functionPanel != null)
            {
                Vector3 originalPanelScale = functionPanel.localScale;

                yield return ScaleTo(functionPanel, originalPanelScale * 1.15f, 0.20f, true);
                yield return new WaitForSeconds(0.15f);
                yield return ScaleTo(functionPanel, originalPanelScale, 0.20f, false);
            }

            // Highlight the output box.
            if (outputCell != null)
            {
                Transform outputTransform = outputCell.transform;
                Vector3 originalOutputScale = outputTransform.localScale;

                yield return ScaleTo(outputTransform, originalOutputScale * 1.22f, 0.20f, true);
                yield return new WaitForSeconds(0.15f);
                yield return ScaleTo(outputTransform, originalOutputScale, 0.20f, false);
            }

            isAnimating = false;

            onComplete?.Invoke(resultValue);
        }

        // Godot: func reset_function()
        public void ResetFunction()
        {
            if (isAnimating)
            {
                return;
            }

            parameterA = 10;
            parameterB = 20;
            currentFunctionName = "add";
            resultValue = CalculateResult(currentFunctionName);

            DrawFunction(true);
        }

        // ----- Tweening helpers (same pattern as every other module) -----

        private IEnumerator ScaleTo(Transform target, Vector3 endScale, float duration, bool easeOut)
        {
            if (target == null) yield break;

            Vector3 startScale = target.localScale;
            float t = 0f;

            while (t < duration)
            {
                if (target == null) yield break;

                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / duration);
                float eased = easeOut ? EaseOutBack(p) : EaseInBack(p);
                target.localScale = Vector3.LerpUnclamped(startScale, endScale, eased);
                yield return null;
            }

            if (target != null) target.localScale = endScale;
        }

        // Approximates Godot's TRANS_BACK easing (a slight overshoot).
        private float EaseOutBack(float x)
        {
            float c1 = 1.70158f;
            float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3) + c1 * Mathf.Pow(x - 1f, 2);
        }

        private float EaseInBack(float x)
        {
            float c1 = 1.70158f;
            float c3 = c1 + 1f;
            return c3 * x * x * x - c1 * x * x;
        }
    
        // -----------------------------------------------------------------
        // Phase 29 fix (F1): a module can be disabled MID-ANIMATION.
        //
        // ChangeModuleController.ShowSelector() disables every module
        // unconditionally, including the one currently on screen, and the
        // Change Module button is never made non-interactable — so a student
        // can tap it during an animation. Disabling a GameObject halts its
        // coroutines instantly, killing the animation between
        // "isAnimating = true" and the "isAnimating = false" at its tail.
        //
        // Nothing else in the project ever cleared that flag. It stayed true
        // for the rest of the app run, and because Reset*() self-guards on
        // it, EnterModule() -> Reset*() silently did nothing on re-entry.
        // The module was then permanently unusable — every button answered
        // "Please wait. An animation is still running." until the app was
        // restarted. Reachable in ~3 seconds of ordinary use during a
        // Search or Traverse.
        //
        // OnDisable() is the primary hook: it fires on every deactivation
        // whatever the cause. ForceStopAnimation() is public as a backstop
        // for the case where the CONTROLLER's GameObject is disabled but
        // this module's is not — OnDisable() here would not run then, so
        // the controller also calls it from EnterModule().
        // -----------------------------------------------------------------

        private void OnDisable()
        {
            ForceStopAnimation();
        }

        /// <summary>
        /// Clears any in-flight animation state. Safe to call at any time.
        /// See the Phase 29 note above.
        /// </summary>
        public void ForceStopAnimation()
        {
            StopAllCoroutines();
            isAnimating = false;
        }
    }
}
