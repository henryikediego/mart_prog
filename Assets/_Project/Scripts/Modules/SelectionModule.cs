using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using MartProg.Menu;

namespace MartProg.Modules
{
    /// <summary>
    /// Phase 25: the Selection module's 3D visual, ported from
    /// selection_module.gd — read directly from source for this phase.
    ///
    /// VALUE box → arrow → CONDITION block → arrow → RESULT box, with
    /// VALUE / CONDITION / RESULT headings above. Same three-part
    /// composition as Functions and Loops.
    ///
    /// **Read porting note 1 before comparing this against
    /// selection_module.gd — most of that file does not run.**
    ///
    /// Porting decisions and preserved/adapted source quirks:
    ///
    /// 1. **selection_module.gd's own state machine is DEAD CODE and is not
    ///    ported. This module is a display surface only.**
    ///
    ///    The .gd file looks like a complete implementation: `value`,
    ///    `threshold`, six comparison rules, `evaluate_condition()`,
    ///    `evaluate_elseif_logic()`, `evaluate_nested_logic()`,
    ///    `check_selection()`, `toggle_rule()`, `set_condition_from_text()`,
    ///    `set_basic_if_mode()`, `set_elseif_mode()`, `set_selection_mode()`,
    ///    `update_result()`, `reset_pending_result()`, `get_status_text()`,
    ///    `get_mode_code_text()`, `get_rule_code()`. Almost none of it is
    ///    reachable.
    ///
    ///    Verified against every `selection_module.*` call site in
    ///    ar_learning_screen.gd: `check_selection()`, `toggle_rule()` and
    ///    `set_condition_from_text()` are each called exactly once, and all
    ///    three callers (`handle_selection_check()`,
    ///    `handle_selection_toggle_rule()`,
    ///    `handle_selection_set_condition()`) are themselves never called.
    ///    Two of them are dead by explicit intent — the live handlers carry
    ///    comments saying "Do not call the old handle_selection_set_condition()
    ///    here, because it may overwrite the condition box with old rule
    ///    logic" and "Do not call handle_selection_check() here. The old
    ///    handler may use the old condition/value and overwrite the visual
    ///    result."
    ///
    ///    What actually runs is a second, parallel implementation living
    ///    entirely in ar_learning_screen.gd, with its own state
    ///    (`selection_current_mode`, `selection_basic_value`,
    ///    `selection_basic_condition_text`, `selection_basic_rules`, and the
    ///    ladder/nested equivalents) and its own evaluators. It drives this
    ///    visual through ONE entry point: `set_selection_visual_texts(value,
    ///    condition, result)`. That is the only method of this class the
    ///    live app uses, besides `reset_selection()`.
    ///
    ///    The two implementations genuinely disagree — the .gd module
    ///    defaults to value 60 with six rules; the live screen state
    ///    defaults to value 75 with four (">= 50", "< 50", "== 0", "!= 0").
    ///    So "port the module" and "port what the app does" are different
    ///    programs. This project ports what the app does, so the logic lives
    ///    in SelectionController.cs and this class holds no selection state
    ///    at all.
    ///
    /// 2. **set_selection_visual_texts()'s label-finding heuristic is NOT
    ///    ported.** Source locates the three labels by walking the whole node
    ///    tree collecting every Label3D, then classifying each by inspecting
    ///    its TEXT: `_looks_like_selection_value_label()` (parses as int, or
    ///    contains "," or "/"), `_looks_like_selection_condition_label()`
    ///    (contains "value"/"score"/"rule"/">="/"<" ...), and
    ///    `_looks_like_selection_result_label()` (matches a hardcoded list
    ///    of "TRUE"/"FALSE"/"PASS"/"A"/"B"/"Excellent"/"Attendance too
    ///    low"/...). It is extremely fragile: a result of "75" would be
    ///    classified as the value label, and any result string not on the
    ///    hardcoded list is silently never found. Since DrawSelection()
    ///    creates these three labels here, this port keeps direct
    ///    references. Same call as Phase 23's note 3 for Functions, but a
    ///    much larger fragility removed.
    ///
    /// 3. **The dynamic box-resizing helpers ARE ported, in spirit.**
    ///    `_fit_selection_value_box_to_text()`,
    ///    `_fit_selection_result_box_to_text()` and
    ///    `_fit_selection_condition_box_to_text()` shrink the font and widen
    ///    the box as the text gets longer — this matters, because later
    ///    modes produce results like "Attendance too low" that will not fit
    ///    a value-sized box. The font-size thresholds are ported exactly.
    ///    The box widening is applied as a local X scale rather than by
    ///    rebuilding a BoxMesh and stashing the original size in node
    ///    metadata, which is what source does.
    ///
    /// 4. **Two font-size tables exist for the same job, and the live one
    ///    wins.** `create_value_cell()` calls `get_value_font_size()`
    ///    (84/84/68/60/54 by length) when first drawing a box, while
    ///    `_fit_selection_value_box_to_text()` uses a different table
    ///    (78/58/44/34) when updating it afterwards. Since the live app
    ///    always goes through `set_selection_visual_texts()`, the second
    ///    table is what a student actually sees for everything except the
    ///    very first frame. Both are ported to their respective paths,
    ///    preserving the inconsistency rather than picking one.
    ///
    /// 5. **The CONDITION block builds a URP Unlit material at runtime** —
    ///    same reason as Functions (Phase 23 note 8) and Loops (Phase 24
    ///    note 2), and like Loops source sets SHADING_MODE_UNSHADED so Unlit
    ///    is a faithful port rather than only an AR-lighting workaround.
    ///
    /// 6. **The taller condition block exists but is never used.** Source
    ///    switches height 1.08 → 1.35 and font 38 → 28 when
    ///    `selection_mode` is "elseif" or "nested" — but that is the DEAD
    ///    module field (note 1), never written by the live implementation,
    ///    so it is permanently "basic" and the tall geometry never renders.
    ///    It was written for the dead path's multi-line ladder text; the
    ///    live path puts only a rule NAME in the block, one short line.
    ///    SetBlockTall() is kept as a faithful port of the capability, but
    ///    SelectionController only ever calls it with false — see that
    ///    file's porting note 16 for how calling it with true looked
    ///    on-device.
    ///
    /// 7. **Godot's absolute SELECTION_VISUAL_Y (2.90) is not ported, but
    ///    its intent is.** arHeight/arDistance replace the raw value, as for
    ///    every module since ArrayModule. What IS carried across is source's
    ///    own reason for it — the constant carries the comment "Moved upward
    ///    from 1.65 to give the visual more space above the dashboard", and
    ///    1.65 is exactly the height loop_module.gd uses. So unlike every
    ///    other module, this one is deliberately raised relative to the
    ///    dashboard, and arHeight therefore defaults to 0.12 rather than 0.
    ///    Confirmed necessary on the first Phase 25 build: at 0 the
    ///    composition rendered behind the Summary panel, which is precisely
    ///    the problem source's comment describes.
    ///
    /// 12. **The RESULT box grows UNIFORMLY, not in X only — a source
    ///     technique that cannot be ported directly.**
    ///     `_fit_selection_result_box_to_text()` widens the box by
    ///     rebuilding its BoxMesh with `max(original.x, desired_width)`,
    ///     leaving Y and Z alone. In Godot that works because the Label3D is
    ///     a sibling positioned in world space, unaffected by the mesh's
    ///     dimensions. Here the value label is a CHILD of the ArrayCell
    ///     prefab, so scaling the cell's X axis alone stretches the glyphs
    ///     horizontally rather than giving them room — which is precisely
    ///     how "Exam score too low" rendered on-device: smeared across and
    ///     past the box.
    ///
    ///     The fix is to widen in X as source does, then counter-scale the
    ///     label by the inverse via ArrayCell.SetValueLabelCounterScaleX(),
    ///     so the box grows horizontally while its glyphs stay correctly
    ///     proportioned. An intermediate attempt scaled the box uniformly
    ///     instead — that removed the distortion but did not give the text
    ///     any more horizontal room, so long results still overflowed.
    ///     Recorded because "scale uniformly" is the obvious-looking fix and
    ///     it is not sufficient.
    ///
    ///     The source strings themselves are NOT shortened — they are
    ///     content a student reads and they also appear verbatim in the
    ///     dashboard explanation, so changing them would be a content edit
    ///     rather than a layout fix.
    ///
    ///     Once the widening worked, the long buckets' font scales were
    ///     raised back up (0.30 -> 0.45, 0.24 -> 0.36, landing near source's
    ///     own 32/78 and 28/78 ratios). They had been cut hard while the box
    ///     could not grow, which made the text safe but hard to read. Worth
    ///     noting as a sequencing lesson: the font reduction was
    ///     compensating for a geometry bug, and once that was fixed the
    ///     compensation had to be undone rather than left in place.
    ///
    /// 11. **The two long condition-font buckets are reduced from source's
    ///     values (25 -> 19, 22 -> 16).** A continuation of note 9: Godot's
    ///     font_size numbers combine with pixel_size to reach a world size
    ///     and do not map 1:1 onto TextMeshPro.fontSize, so they are
    ///     starting points rather than absolutes. Source's own proportions
    ///     hold for its shorter strings, but "Project Eligibility Rule" (24
    ///     characters, the longest in the module) rendered wider than the
    ///     block at font 25 and ran across both arrows on-device. The two
    ///     short buckets are untouched, since they were correct.
    ///
    ///     An earlier attempt fixed this by removing spacingMultiplier from
    ///     the block's own scale, on the reasoning that a spacing control
    ///     should not resize an element. That is defensible in principle but
    ///     made the block far too wide in every mode — it is reverted, and
    ///     the block keeps spacingMultiplier as the other modules' blocks
    ///     do. Recorded because the principle is still worth knowing if the
    ///     geometry is ever revisited: the block and its text scale by
    ///     different terms, so changing one without the other breaks the
    ///     fit at non-default spacing.
    ///
    /// 10. **TextMeshPro labels need an explicit rect; Label3D has none.**
    ///     Godot's Label3D renders text at whatever width the string needs —
    ///     there is no bounding box and nothing ever wraps. Unity's
    ///     TextMeshPro is rect-bound, and a freshly added component defaults
    ///     to 200 units wide. That is narrower than this module's longer
    ///     labels: "Performance Rule" at font 29 wrapped onto two lines and
    ///     clipped inside the CONDITION block on the first Phase 25b build.
    ///     AddMarker() therefore gives every label a deliberately generous
    ///     600x100 rect with TextOverflowModes.Overflow, which reproduces
    ///     Label3D's behaviour. Because alignment is Center and the text is
    ///     centred on the transform, the larger rect is invisible for the
    ///     short labels. Earlier modules never hit this — their longest
    ///     label was Loops' "value = 10\ntotal = 10", which carries its own
    ///     explicit line break and has short lines either side of it.
    ///
    /// 9. **Godot font sizes are ported as RATIOS, not absolutes.** The
    ///    _fit_* helpers and get_value_font_size() set Label3D.font_size to
    ///    values like 78, 58 or 34 — but Label3D reaches a world size by
    ///    combining font_size with pixel_size, so those numbers are not in
    ///    the same unit as TextMeshPro.fontSize. Porting them literally made
    ///    the value and result text render several times too large for their
    ///    boxes (found immediately on the first Phase 25 build — the "?" in
    ///    the RESULT box dwarfed the whole composition). What those tables
    ///    actually encode is a ratio: shrink the text as the content grows.
    ///    Each table's largest entry is therefore treated as 1.0 and the
    ///    rest expressed against it, applied via
    ///    ArrayCell.SetValueFontScale() to whatever size the prefab was
    ///    authored at. The relative behaviour is preserved exactly; only the
    ///    unit changes. No earlier module hit this because none of them
    ///    varied cell font size at runtime at all.
    ///
    /// 8. **get_status_text() has an unreachable duplicate.** Its basic-mode
    ///    return is followed by a second, identical return block that can
    ///    never execute. The status line is ported (SelectionController owns
    ///    it, from the screen's own state), the dead duplicate is not.
    /// </summary>
    public class SelectionModule : MonoBehaviour
    {
        [Header("Assign in the Inspector")]
        [Tooltip("Same ArrayCell prefab used by every other module.")]
        [SerializeField] private GameObject cellPrefab;
        [Tooltip("Container for the VALUE box.")]
        [SerializeField] private Transform inputParent;        // was: $Input
        [Tooltip("Container for the CONDITION block and its text.")]
        [SerializeField] private Transform conditionParent;    // was: $ConditionBlock
        [Tooltip("Container for the RESULT box.")]
        [SerializeField] private Transform outputParent;       // was: $Output
        [Tooltip("Container for the headings and arrows.")]
        [SerializeField] private Transform markersParent;      // was: $Markers

        [Header("AR placement tuning")]
        [SerializeField] private float arScale = 0.12f;
        [SerializeField] private float arDistance = 0.5f;
        [Tooltip("Godot's absolute SELECTION_VISUAL_Y (2.90) is not ported, but its INTENT is — " +
                 "see class comment note 7. Every other module sits at 0; this one defaults higher " +
                 "because source deliberately raised it (its own comment: \"Moved upward from 1.65 " +
                 "to give the visual more space above the dashboard\"). At 0 the composition sits " +
                 "behind the Summary panel, which is the exact problem that comment describes.")]
        [SerializeField] private float arHeight = 0.12f;
        [SerializeField] private float cellSizeMultiplier = 1.4f;
        [Tooltip("Tightens or widens the whole composition. Selection is wide (VALUE + CONDITION + RESULT), so it needs a smaller value than the box-only modules.")]
        [SerializeField] private float spacingMultiplier = 0.55f;

        [Header("Label appearance")]
        [Tooltip("Keep SMALL — anything nearer the camera projects lower on screen when viewed from above, making arrows appear to sag. See Phase 23 note 10.")]
        [SerializeField] private float labelForwardOffset = 0.02f;
        [SerializeField] private float labelBaseScale = 0.06f;

        [Header("CONDITION block appearance")]
        [Tooltip("Godot: create_condition_block()'s albedo_color of (0.16, 0.16, 0.16).")]
        [SerializeField] private Color conditionBlockColor = new Color(0.16f, 0.16f, 0.16f, 1f);
        [Tooltip("OPTIONAL. Leave empty and a URP Unlit material is built at runtime (class comment note 5).")]
        [SerializeField] private Material conditionBlockMaterial;

        // ----- Source constants (pre-arScale Godot units) -----
        private const float InputBoxWidth = 1.15f;
        private const float OutputBoxWidth = 1.45f;
        private const float ConditionWidth = 3.05f;
        private const float ConditionHeight = 1.08f;
        private const float ConditionHeightTall = 1.35f;   // elseif / nested — note 6
        private const float ConditionDepth = 0.18f;
        private const float SectionGap = 1.60f;

        private const float HeadingYOffset = 1.05f;
        private const float ArrowYOffset = 0.02f;

        private const int HeaderFontSize = 30;
        private const int ArrowFontSize = 44;
        private const int ConditionFontSize = 38;
        private const int ConditionFontSizeTall = 28;      // elseif / nested — note 6

        private const float InputCellScale = 0.72f;
        private const float OutputCellScale = 0.78f;

        private static readonly Color ValueColor = new Color(0.4f, 0.8f, 1.0f, 1f);
        private static readonly Color ConditionColor = new Color(1.0f, 0.85f, 0.1f, 1f);
        private static readonly Color ResultColor = new Color(0.4f, 1.0f, 0.55f, 1f);

        // ----- Display state only. No selection logic lives here — note 1. -----
        private string valueText = "60";
        private string conditionText = "value >= 50";
        private string resultText = "?";
        private bool useTallBlock;

        private ArrayCell inputCell;
        private ArrayCell outputCell;
        private Transform conditionBody;
        private TextMeshPro conditionLabel;

        public Transform ConditionBody => conditionBody;
        public Transform OutputCellTransform => outputCell != null ? outputCell.transform : null;

        private void Start()
        {
            DrawSelection();
        }

        private void ClearVisuals()
        {
            inputCell = null;
            outputCell = null;
            conditionBody = null;
            conditionLabel = null;

            ClearContainer(inputParent);
            ClearContainer(conditionParent);
            ClearContainer(outputParent);
            ClearContainer(markersParent);
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

        private float LayoutX(float preScaleX) => preScaleX * spacingMultiplier * arScale;
        private float LayoutY(float preScaleYOffset) => arHeight + preScaleYOffset * arScale;

        // Godot: func draw_selection(force_redraw := false)
        public void DrawSelection()
        {
            ClearVisuals();

            float totalWidth = InputBoxWidth + SectionGap + ConditionWidth + SectionGap + OutputBoxWidth;
            float leftEdge = -totalWidth / 2f;

            float inputCenterX = leftEdge + InputBoxWidth / 2f;
            float inputRightEdge = inputCenterX + InputBoxWidth / 2f;

            float conditionLeftEdge = inputRightEdge + SectionGap;
            float conditionCenterX = conditionLeftEdge + ConditionWidth / 2f;
            float conditionRightEdge = conditionCenterX + ConditionWidth / 2f;

            float outputLeftEdge = conditionRightEdge + SectionGap;
            float outputCenterX = outputLeftEdge + OutputBoxWidth / 2f;

            float leftArrowX = (inputRightEdge + conditionLeftEdge) / 2f;
            float rightArrowX = (conditionRightEdge + outputLeftEdge) / 2f;

            // VALUE box — source adds no caption below it, deliberately
            // ("to avoid duplication with the VALUE heading").
            // Note 4: the initial draw uses get_value_font_size()'s table.
            // The _fit_* tables only apply once SetSelectionVisualTexts()
            // runs, exactly as in source.
            inputCell = CreateValueBox("SelectionValue", valueText, inputCenterX, InputCellScale, inputParent);

            // CONDITION block
            CreateConditionBlock(conditionCenterX);

            // RESULT box — likewise no caption below.
            outputCell = CreateValueBox("SelectionResult", resultText, outputCenterX, OutputCellScale, outputParent);

            // Headings
            AddMarker("VALUE", inputCenterX, HeadingYOffset, HeaderFontSize, ValueColor);
            AddMarker("CONDITION", conditionCenterX, HeadingYOffset, HeaderFontSize, ConditionColor);
            AddMarker("RESULT", outputCenterX, HeadingYOffset, HeaderFontSize, ResultColor);

            // Arrows — both true midpoints in this module (contrast Loops,
            // whose right arrow had a /2.2 typo).
            AddArrow(leftArrowX);
            AddArrow(rightArrowX);
        }

        private ArrayCell CreateValueBox(string objectName, string text, float preScaleX, float cellScale, Transform parent)
        {
            if (cellPrefab == null || parent == null) return null;

            GameObject cell = Instantiate(cellPrefab, parent);
            cell.name = objectName;
            cell.transform.localPosition = new Vector3(LayoutX(preScaleX), arHeight, arDistance);
            cell.transform.localScale = Vector3.one * cellScale * arScale * cellSizeMultiplier;
            cell.transform.localRotation = Quaternion.identity;

            if (cell.GetComponent<Billboard>() == null)
            {
                cell.AddComponent<Billboard>();
            }

            ArrayCell arrayCell = cell.GetComponent<ArrayCell>();
            if (arrayCell != null)
            {
                arrayCell.SetValueOnly(text);
                arrayCell.SetValueFontScale(GetInitialValueFontScale(text));
            }

            return arrayCell;
        }

        // Godot: func create_condition_block(block_position: Vector3)
        private void CreateConditionBlock(float preScaleX)
        {
            if (conditionParent == null) return;

            float height = useTallBlock ? ConditionHeightTall : ConditionHeight;
            int fontSize = useTallBlock ? ConditionFontSizeTall : ConditionFontSize;

            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = "ConditionBlock";

            Collider blockCollider = block.GetComponent<Collider>();
            if (blockCollider != null) Destroy(blockCollider);

            block.transform.SetParent(conditionParent, false);
            block.transform.localPosition = new Vector3(LayoutX(preScaleX), arHeight, arDistance);
            // Phase 45: height grows with CurrentTextScale here too, matching
            // ApplyConditionFontSizeAndWidth() below — this is the block's
            // INITIAL size, before any Set Condition call has run.
            block.transform.localScale = new Vector3(
                ConditionWidth * spacingMultiplier * arScale * cellSizeMultiplier,
                height * arScale * cellSizeMultiplier * LandingPageController.CurrentTextScale,
                ConditionDepth * arScale * cellSizeMultiplier);
            block.transform.localRotation = Quaternion.identity;

            Renderer blockRenderer = block.GetComponent<Renderer>();
            if (blockRenderer != null) blockRenderer.material = BuildBlockMaterial();

            conditionBody = block.transform;

            conditionLabel = AddMarker(conditionText, preScaleX, 0f, fontSize, Color.white, conditionParent);
        }

        /// URP Unlit — see class comment note 5.
        private Material BuildBlockMaterial()
        {
            if (conditionBlockMaterial != null) return new Material(conditionBlockMaterial);

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            Material material = new Material(shader);
            material.color = conditionBlockColor;
            return material;
        }

        private TextMeshPro AddMarker(string text, float preScaleX, float preScaleYOffset, int fontSize, Color color, Transform parent = null)
        {
            Transform target = parent != null ? parent : markersParent;
            if (target == null) return null;

            GameObject markerObj = new GameObject("SelectionMarker_" + text.Replace("\n", "_"));
            markerObj.transform.SetParent(target, false);
            markerObj.transform.localPosition = new Vector3(
                LayoutX(preScaleX),
                LayoutY(preScaleYOffset),
                arDistance - labelForwardOffset);
            markerObj.transform.localScale = Vector3.one * labelBaseScale * arScale * cellSizeMultiplier;
            markerObj.transform.localRotation = Quaternion.identity;

            TextMeshPro tmp = markerObj.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = fontSize * LandingPageController.CurrentTextScale; // Phase 45
            tmp.color = color;
            tmp.fontStyle = FontStyles.Bold;
            tmp.outlineWidth = 0.25f;
            tmp.outlineColor = Color.black;
            tmp.alignment = TextAlignmentOptions.Center;

            // See class comment note 10. A fresh TextMeshPro's RectTransform
            // defaults to 200 units wide, which is narrower than several of
            // this module's labels — "Performance Rule" at font 29 wrapped
            // and clipped inside the CONDITION block. Godot's Label3D has no
            // rect at all, so nothing in source wraps. A generous rect plus
            // Overflow reproduces that: the text lays out on one line and is
            // centred on the transform, so the wider rect changes nothing
            // visually for the short labels.
            tmp.rectTransform.sizeDelta = new Vector2(600f, 100f);
            tmp.overflowMode = TextOverflowModes.Overflow;

            markerObj.AddComponent<Billboard>();

            return tmp;
        }

        private void AddArrow(float preScaleX)
        {
            AddMarker("→", preScaleX, ArrowYOffset, ArrowFontSize, Color.white);
        }

        // ---------------------------------------------------------------
        // The ONE entry point the live app uses — see class comment note 1.
        // Godot: func set_selection_visual_texts(value_text, condition_text,
        // result_text := "?")
        // ---------------------------------------------------------------

        public void SetSelectionVisualTexts(string newValueText, string newConditionText, string newResultText = "?")
        {
            valueText = newValueText ?? "";
            conditionText = newConditionText ?? "";
            resultText = newResultText ?? "?";

            // Source hunts for the three labels by inspecting their text
            // (note 2); here they are held directly, so this just updates
            // them — or redraws if the visual hasn't been built yet.
            if (inputCell == null || outputCell == null || conditionLabel == null)
            {
                DrawSelection();
            }

            if (inputCell == null || outputCell == null || conditionLabel == null) return;

            inputCell.SetValueOnly(valueText);
            ApplyValueFontSize(inputCell, valueText);

            outputCell.SetValueOnly(resultText);
            ApplyResultFontSizeAndWidth(outputCell, resultText);

            conditionLabel.text = conditionText;
            ApplyConditionFontSizeAndWidth(conditionText);
        }

        /// Whether the CONDITION block uses the taller multi-line geometry.
        /// Basic mode = false; else-if / nested = true (note 6). Phase 25
        /// only ever sets false; kept here so Phase 25b needs no change to
        /// this file.
        public void SetBlockTall(bool tall)
        {
            if (useTallBlock == tall) return;

            useTallBlock = tall;
            DrawSelection();
        }

        // Godot: func reset_selection() — reduced to its display half. The
        // value/threshold/rule/mode reset it also performs is dead state
        // (note 1); SelectionController owns the real reset.
        public void ResetSelection()
        {
            valueText = "60";
            conditionText = "value >= 50";
            resultText = "?";
            useTallBlock = false;

            DrawSelection();
        }

        // ----- Text-fitting helpers — see class comment notes 3 and 4 -----

        /// Godot: get_value_font_size() — used only on first draw (note 4).
        /// Returned as a RATIO of the prefab's own font size, not an
        /// absolute — see note 9 and ArrayCell.SetValueFontScale().
        /// Source's table is 84/84/68/60/54; 84 is treated as 1.0.
        private static float GetInitialValueFontScale(string text)
        {
            if (text == "?") return 1.00f;
            if (text.Length <= 2) return 1.00f;
            if (text.Length <= 4) return 68f / 84f;
            if (text.Length <= 5) return 60f / 84f;
            return 54f / 84f;
        }

        /// Godot: _fit_selection_value_box_to_text() — the table the live
        /// app actually exercises (note 4).
        private void ApplyValueFontSize(ArrayCell cell, string text)
        {
            if (cell == null) return;

            // Source's table is 78/58/44/34; 78 is treated as 1.0 (note 9).
            float scale;
            if (text.Length <= 2) scale = 1.00f;
            else if (text.Length <= 5) scale = 58f / 78f;
            else if (text.Length <= 8) scale = 44f / 78f;
            else scale = 34f / 78f;

            cell.SetValueFontScale(scale);
        }

        /// Godot: _fit_selection_result_box_to_text(). Source rebuilds the
        /// BoxMesh and caches the original size in node metadata; here the
        /// same widening is a local X scale (note 3).
        private void ApplyResultFontSizeAndWidth(ArrayCell cell, string text)
        {
            if (cell == null) return;

            // Font scale, and how much bigger the whole box gets. See
            // note 12 for why both are needed and why the box grows
            // UNIFORMLY rather than only in X.
            float scale;
            float desiredWidth;

            if (text.Length <= 1) { scale = 1.00f; desiredWidth = 1.20f; }
            else if (text.Length <= 5) { scale = 58f / 78f; desiredWidth = 1.35f; }
            else if (text.Length <= 10) { scale = 0.56f; desiredWidth = 1.80f; }
            else if (text.Length <= 18) { scale = 0.45f; desiredWidth = 3.10f; }
            else { scale = 0.36f; desiredWidth = 3.60f; }

            cell.SetValueFontScale(scale);

            // Source widens the box mesh in X only (max(original.x,
            // desired)). Reproduced here by scaling the cell's X — but the
            // value label is a CHILD of the cell, so that scale would
            // otherwise smear the glyphs horizontally (see note 12). The
            // label is counter-scaled by the inverse, so the box gets wider
            // while its text stays correctly proportioned.
            //
            // Phase 45 on-device fix: same issue as
            // ApplyConditionFontSizeAndWidth() — desiredWidth only accounts
            // for text length, but cell.SetValueFontScale() above already
            // folds in LandingPageController.CurrentTextScale, so the box
            // needs the same factor or short RESULT text overflows a box
            // sized only for its un-scaled length bucket.
            float growth = Mathf.Max(1f,
                (desiredWidth * LandingPageController.CurrentTextScale) / OutputBoxWidth);
            float baseScale = OutputCellScale * arScale * cellSizeMultiplier;

            cell.transform.localScale = new Vector3(baseScale * growth, baseScale, baseScale);
            cell.SetValueLabelCounterScaleX(growth);
        }

        /// Godot: _fit_selection_condition_box_to_text().
        private void ApplyConditionFontSizeAndWidth(string text)
        {
            if (conditionLabel == null) return;

            int fontSize;
            float desiredWidth;

            // Source's fonts are 32/29/25/22; the two long buckets are
            // reduced here — see note 11. Widths are source's own.
            if (text.Length <= 10) { fontSize = 32; desiredWidth = 2.20f; }
            else if (text.Length <= 16) { fontSize = 29; desiredWidth = 2.85f; }
            else if (text.Length <= 24) { fontSize = 19; desiredWidth = 3.45f; }
            else { fontSize = 16; desiredWidth = 3.80f; }

            // Phase 45: this reassignment runs AFTER AddMarker() already set
            // conditionLabel.fontSize once at creation (see note above this
            // method's use of AddMarker() to build conditionLabel in the first
            // place) — without the multiplier here too, this later call would
            // silently overwrite AddMarker()'s scaled value with an unscaled
            // one every time the condition text changes.
            conditionLabel.fontSize = fontSize * LandingPageController.CurrentTextScale;

            if (conditionBody == null) return;

            // Phase 45 on-device fix: this ratio previously only accounted for
            // TEXT LENGTH (the desiredWidth bucket above), never for the
            // accessibility scale — so short/medium condition text (which
            // fell into a bucket at or below 1x already) got no extra box
            // room at all even though its rendered font was up to 1.3x
            // bigger, and the CONDITION block's neighboring arrow visibly
            // intruded into it. Folding CurrentTextScale into the same ratio
            // means the box now grows for BOTH reasons together.
            float widthRatio = Mathf.Max(1f,
                (desiredWidth * LandingPageController.CurrentTextScale) / ConditionWidth);
            float height = useTallBlock ? ConditionHeightTall : ConditionHeight;

            conditionBody.localScale = new Vector3(
                ConditionWidth * spacingMultiplier * arScale * cellSizeMultiplier * widthRatio,
                height * arScale * cellSizeMultiplier * LandingPageController.CurrentTextScale,
                ConditionDepth * arScale * cellSizeMultiplier);
        }

        // ----- Check animation, driven by SelectionController -----

        /// Godot: the tween half of check_selection() — the condition block
        /// pulses, then the result box.
        ///
        /// **A source bug is NOT reproduced.** In check_selection(),
        /// `is_animating = false` sits INSIDE the `if output_cell != null`
        /// block, so if the output cell were ever missing the module would
        /// stay flagged as animating forever and silently reject every
        /// later Check, Set Value and Reset. Here the flag is cleared
        /// unconditionally.
        public IEnumerator PlayCheckAnimation()
        {
            if (conditionBody != null)
            {
                Vector3 original = conditionBody.localScale;
                yield return ScaleTo(conditionBody, original * 1.10f, 0.20f, true);
                yield return ScaleTo(conditionBody, original, 0.20f, false);
            }

            if (outputCell != null)
            {
                Transform t = outputCell.transform;
                Vector3 original = t.localScale;
                yield return ScaleTo(t, original * 1.14f, 0.18f, true);
                yield return ScaleTo(t, original, 0.18f, false);
            }
        }

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
    }
}
