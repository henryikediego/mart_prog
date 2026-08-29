using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using MartProg.Menu;

namespace MartProg.Modules
{
    /// <summary>
    /// Phase 24: the Loops module's 3D visual, ported from loop_module.gd —
    /// read directly from source for this phase.
    ///
    /// The visual is a left-to-right composition: a DATA group of value
    /// boxes (each with its index below), an arrow, a dark LOOP block whose
    /// text narrates the current iteration, another arrow, and a RESULT box
    /// holding the running total. A CURRENT marker floats above whichever
    /// value the loop is visiting.
    ///
    /// Closest relative so far is FunctionModule (Phase 23) — same
    /// group/block/output composition and the same generated-mesh block —
    /// but unlike Functions this one has a variable number of boxes, so it
    /// carries ArrayModule-style per-count scale/spacing tables.
    ///
    /// Porting decisions and preserved/adapted source quirks:
    ///
    /// 1. **Source's own layout is already centred**, unlike
    ///    function_module.gd's. draw_loop() computes total_width and starts
    ///    from left_edge = -total_width / 2.0, so no layoutXOffset
    ///    equivalent is needed here — Phase 23's note 9 does not apply.
    ///
    /// 2. **The LOOP block builds a URP material at runtime**, same reason
    ///    and same fix as FunctionModule's function panel (Phase 23 note 8):
    ///    GameObject.CreatePrimitive() assigns Unity's built-in Standard
    ///    shader, which this project's URP setup renders as solid magenta.
    ///    Source additionally sets SHADING_MODE_UNSHADED on the block, so
    ///    URP **Unlit** here is a closer match than it even was for
    ///    Functions — this is a faithful port of the shading mode, not just
    ///    an AR-lighting workaround.
    ///
    /// 3. **labelForwardOffset stays small (0.02)** for the reason Phase 23
    ///    note 10 documents: pulling flat text toward the camera also moves
    ///    it down on screen when AR content is viewed from above. Loops has
    ///    two arrows sitting at exactly box height, the same elements that
    ///    made the artifact visible in Functions.
    ///
    /// 4. **get_item_spacing(scale_factor) never reads its argument** — it
    ///    switches on values.size() alone. Ported as a parameterless method,
    ///    exactly as Phase 23 did for get_function_font_size(function_text).
    ///    Two modules in a row with the same dead-parameter pattern.
    ///
    /// 5. **A source typo in the right-hand arrow's position IS corrected,
    ///    after on-device evidence.** Source computes:
    ///        left_arrow_x  = (data_right_edge + loop_left_edge) / 2.0
    ///        right_arrow_x = (loop_right_edge + output_left_edge) / 2.2
    ///    The left arrow is a correct midpoint; the right one divides by
    ///    2.2, which is not a midpoint of anything and pulls the arrow left
    ///    of the gap it belongs in. This was initially preserved as a
    ///    harmless cosmetic quirk — but the first on-device build showed the
    ///    right arrow sitting ON the LOOP block's edge rather than in the
    ///    space between the block and the RESULT box, which reads as a
    ///    rendering fault rather than as an arrow. RightArrowDivisor is
    ///    therefore 2f, matching the left arrow. Set it back to 2.2f to
    ///    reproduce source exactly.
    ///
    /// 6. **Godot's absolute LOOP_VISUAL_Y (1.65) is not ported**, same call
    ///    made for every module since ArrayModule: arHeight/arDistance
    ///    replace it for AR placement. All RELATIVE offsets (headers at
    ///    +1.02, CURRENT at +0.66, index captions at -0.74, the "total"
    ///    caption at -0.68) are ported exactly.
    ///
    /// 7. **A six-value cap lives in set_values_from_text()** — it returns
    ///    false for more than six values, for empty parts, and for
    ///    non-integers. Ported exactly. Note that ar_learning_screen.gd's
    ///    own parse step does NOT enforce the cap, which is the root of the
    ///    Set Values bug documented in LoopsController.cs note 2.
    ///
    /// 8. **step_loop() redraws BEFORE animating, then again after.** The
    ///    first redraw rebuilds every cell (so the animation targets a cell
    ///    created moments earlier), and the trailing redraw clears the
    ///    transient scale. Preserved as-is — it looks redundant but the
    ///    trailing redraw is what restores a clean frame if the coroutine is
    ///    interrupted mid-scale.
    ///
    /// 9. **run_loop() calls step_loop() exactly values.Count times**, with
    ///    a 0.30s lead-in and a 0.35s pause between iterations, then clears
    ///    currentIndex to -1 so the CURRENT marker disappears at the end.
    ///    Ported with the same timings.
    /// </summary>
    public class LoopModule : MonoBehaviour
    {
        [Header("Assign in the Inspector")]
        [Tooltip("Same ArrayCell prefab used by every other module.")]
        [SerializeField] private GameObject cellPrefab;      // was: @export var cell_scene
        [Tooltip("Container for the DATA value boxes.")]
        [SerializeField] private Transform itemsParent;      // was: $Items
        [Tooltip("Container for the LOOP block mesh and its text.")]
        [SerializeField] private Transform loopBlockParent;  // was: $LoopBlock
        [Tooltip("Container for the RESULT box.")]
        [SerializeField] private Transform outputParent;     // was: $Output
        [Tooltip("Container for headers, index captions, arrows and the CURRENT marker.")]
        [SerializeField] private Transform markersParent;    // was: $Markers

        [Header("AR placement tuning (Godot units are too big for real-world AR)")]
        [SerializeField] private float arScale = 0.12f;
        [Tooltip("How far in front of the AR origin (metres) the composition sits.")]
        [SerializeField] private float arDistance = 0.5f;
        [Tooltip("Height (metres) relative to the AR origin. Godot's absolute LOOP_VISUAL_Y (1.65) is intentionally not ported — see class comment note 6.")]
        [SerializeField] private float arHeight = 0f;
        [Tooltip("Grows/shrinks the boxes (and their text) without changing spacing.")]
        [SerializeField] private float cellSizeMultiplier = 1.4f;
        [Tooltip("Tightens or widens the whole left-to-right composition. Loops is wide (DATA group + LOOP block + RESULT box), so it needs a smaller value than the box-only modules.")]
        [SerializeField] private float spacingMultiplier = 0.55f;

        [Header("Label appearance")]
        [Tooltip("Pulls flat text toward the camera, clear of box/block occlusion. Keep SMALL — " +
                 "anything nearer the camera projects lower on screen when content is viewed from " +
                 "above, which makes the arrows appear to sag below the boxes. See class comment note 3.")]
        [SerializeField] private float labelForwardOffset = 0.02f;
        [SerializeField] private float labelBaseScale = 0.06f;

        [Header("LOOP block appearance")]
        [Tooltip("Godot: create_loop_block()'s albedo_color of (0.16, 0.16, 0.16).")]
        [SerializeField] private Color loopBlockColor = new Color(0.16f, 0.16f, 0.16f, 1f);
        [Tooltip("OPTIONAL. Leave empty and a URP Unlit material is built at runtime (class comment note 2).")]
        [SerializeField] private Material loopBlockMaterial;

        // ----- Source constants, ported exactly (pre-arScale Godot units) -----
        private const float ItemBoxWidth = 1.15f;
        private const float OutputBoxWidth = 1.10f;
        private const float SectionGap = 1.55f;
        private const float BlockWidth = 2.85f;
        private const float BlockHeight = 1.10f;
        private const float BlockDepth = 0.18f;

        private const float HeaderYOffset = 1.02f;
        private const float CurrentMarkerYOffset = 0.66f;
        private const float IndexCaptionYOffset = -0.74f;
        private const float TotalCaptionYOffset = -0.68f;

        private const int HeaderFontSize = 36;
        private const int IndexFontSize = 32;
        private const int SmallLabelFontSize = 30;
        private const int CurrentFontSize = 30;
        private const int BoxTextFontSize = 34;
        private const int ArrowFontSize = 40;

        /// See class comment note 5. Source divides by 2.2 here where the
        /// left arrow uses 2.0; that was initially preserved as a harmless
        /// cosmetic quirk, but on-device it visibly pushes the right arrow
        /// onto the LOOP block's edge instead of into the gap. Corrected to
        /// 2f, which is the true midpoint and matches the left arrow.
        private const float RightArrowDivisor = 2f;

        private const int MaxValues = 6;

        private static readonly Color DataColor = new Color(0.4f, 0.8f, 1.0f, 1f);
        private static readonly Color LoopColor = new Color(1.0f, 0.85f, 0.1f, 1f);
        private static readonly Color ResultColor = new Color(0.4f, 1.0f, 0.55f, 1f);

        // ----- Runtime state -----
        private List<int> values = new List<int> { 10, 20, 30 };
        private int currentIndex = -1;
        private int runningTotal;
        /// Unity-only. currentIndex is deliberately cleared to -1 when
        /// RunLoop() finishes (source does this to remove the CURRENT
        /// marker), so it cannot be used to report how much of the loop has
        /// been completed — after a full run it reads -1, the same as before
        /// the loop started. This tracks visited count separately so the
        /// Progress panel can tell "not started" from "finished".
        private int visitedCount;
        private string loopMessage = "for value\nin numbers";
        private bool isAnimating;

        private readonly List<Transform> currentCells = new List<Transform>();
        private Transform outputCell;
        private Transform loopBody;

        // ----- Read-only accessors for LoopsController -----
        public IReadOnlyList<int> Values => values;
        public int CurrentIndex => currentIndex;
        public int RunningTotal => runningTotal;
        /// How many values the loop has visited. Unlike CurrentIndex this
        /// survives RunLoop()'s end-of-run marker cleanup — see the field.
        public int VisitedCount => visitedCount;
        public bool IsAnimating => isAnimating;
        public int MaxValueCount => MaxValues;

        private void Start()
        {
            ResetLoop();
        }

        // Godot: func clear_visuals()
        private void ClearVisuals()
        {
            currentCells.Clear();
            outputCell = null;
            loopBody = null;

            ClearContainer(itemsParent);
            ClearContainer(loopBlockParent);
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

        // Godot: func get_item_scale_factor() -> float
        private float GetItemScaleFactor()
        {
            if (values.Count <= 3) return 0.50f;
            if (values.Count == 4) return 0.46f;
            if (values.Count == 5) return 0.42f;
            return 0.38f;
        }

        // Godot: func get_item_spacing(scale_factor: float) -> float
        // The scale_factor argument is never read — see class comment note 4.
        private float GetItemSpacing()
        {
            if (values.Count <= 3) return 1.35f;
            if (values.Count == 4) return 1.18f;
            if (values.Count == 5) return 1.05f;
            return 0.92f;
        }

        /// Converts a source X coordinate into a local Unity X.
        private float LayoutX(float preScaleX)
        {
            return preScaleX * spacingMultiplier * arScale;
        }

        private float LayoutY(float preScaleYOffset)
        {
            return arHeight + preScaleYOffset * arScale;
        }

        // Godot: func draw_loop(force_redraw := false)
        public void DrawLoop(bool forceRedraw = false)
        {
            if (isAnimating && !forceRedraw)
            {
                return;
            }

            ClearVisuals();

            float itemScale = GetItemScaleFactor();
            float itemSpacing = GetItemSpacing();

            // Width of the DATA group.
            float dataWidth = values.Count > 1
                ? (values.Count - 1) * itemSpacing + ItemBoxWidth
                : ItemBoxWidth;

            float totalWidth = dataWidth + SectionGap + BlockWidth + SectionGap + OutputBoxWidth;
            float leftEdge = -totalWidth / 2f;

            // DATA group
            float dataLeftEdge = leftEdge;
            float dataRightEdge = dataLeftEdge + dataWidth;
            float dataCenterX = dataLeftEdge + dataWidth / 2f;
            float startX = dataCenterX - ((values.Count - 1) * itemSpacing) / 2f;

            // LOOP block
            float loopLeftEdge = dataRightEdge + SectionGap;
            float loopCenterX = loopLeftEdge + BlockWidth / 2f;
            float loopRightEdge = loopCenterX + BlockWidth / 2f;

            // RESULT box
            float outputLeftEdge = loopRightEdge + SectionGap;
            float outputCenterX = outputLeftEdge + OutputBoxWidth / 2f;

            // Arrows — note the asymmetric divisor, see class comment note 5.
            float leftArrowX = (dataRightEdge + loopLeftEdge) / 2f;
            float rightArrowX = (loopRightEdge + outputLeftEdge) / RightArrowDivisor;

            // ----- Data items -----
            for (int i = 0; i < values.Count; i++)
            {
                float cellX = startX + i * itemSpacing;
                Transform cell = CreateValueCell("LoopItem_" + i, values[i], cellX, itemScale, itemsParent);
                if (cell != null) currentCells.Add(cell);

                // Index caption below each box.
                AddMarker(i.ToString(), cellX, IndexCaptionYOffset, IndexFontSize, DataColor);
            }

            // ----- LOOP block -----
            CreateLoopBlock(loopCenterX);

            // ----- RESULT box -----
            outputCell = CreateValueCell("LoopResult", runningTotal, outputCenterX, 0.75f, outputParent);
            AddMarker("total", outputCenterX, TotalCaptionYOffset, SmallLabelFontSize, ResultColor);

            // ----- Section headers -----
            AddMarker("DATA", dataCenterX, HeaderYOffset, HeaderFontSize, DataColor);
            AddMarker("LOOP", loopCenterX, HeaderYOffset, HeaderFontSize, LoopColor);
            AddMarker("RESULT", outputCenterX, HeaderYOffset, HeaderFontSize, ResultColor);

            // ----- Arrows -----
            AddArrow(leftArrowX);
            AddArrow(rightArrowX);

            // ----- CURRENT marker above the active box -----
            if (currentIndex >= 0 && currentIndex < values.Count)
            {
                float activeX = startX + currentIndex * itemSpacing;
                AddMarker("CURRENT", activeX, CurrentMarkerYOffset, CurrentFontSize, LoopColor);
            }
        }

        // Godot: create_value_cell() / create_output_cell() — identical apart
        // from font size and the caption each adds, and the captions are added
        // by the caller here, so one method covers both.
        private Transform CreateValueCell(string objectName, int value, float preScaleX, float cellScale, Transform parent)
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
                arrayCell.SetValueOnly(value.ToString());
            }

            return cell.transform;
        }

        // Godot: func create_loop_block(block_position: Vector3)
        private void CreateLoopBlock(float preScaleX)
        {
            if (loopBlockParent == null) return;

            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = "LoopBlock";

            Collider blockCollider = block.GetComponent<Collider>();
            if (blockCollider != null) Destroy(blockCollider);

            // Phase 45 fix: the block itself was never part of the
            // accessibility scale, only loopMessage's fontSize was — so at
            // Extra Large the (often two-line, e.g. "value = 10\ntotal = 10")
            // narration text grew past the block's fixed size and spilled
            // outside it, found on-device. Width and height now grow with
            // LandingPageController.CurrentTextScale too; depth is left
            // alone since it's the block's thickness, not text space.
            float textScale = LandingPageController.CurrentTextScale;
            block.transform.SetParent(loopBlockParent, false);
            block.transform.localPosition = new Vector3(LayoutX(preScaleX), arHeight, arDistance);
            block.transform.localScale = new Vector3(
                BlockWidth * spacingMultiplier * arScale * cellSizeMultiplier * textScale,
                BlockHeight * arScale * cellSizeMultiplier * textScale,
                BlockDepth * arScale * cellSizeMultiplier);
            block.transform.localRotation = Quaternion.identity;

            Renderer blockRenderer = block.GetComponent<Renderer>();
            if (blockRenderer != null)
            {
                blockRenderer.material = BuildBlockMaterial();
            }

            loopBody = block.transform;

            // The block's narration text — multi-line, e.g. "value = 10\ntotal = 10".
            AddMarker(loopMessage, preScaleX, 0f, BoxTextFontSize, Color.white, loopBlockParent);
        }

        /// URP material for the LOOP block — see class comment note 2.
        /// Unlit specifically, matching source's SHADING_MODE_UNSHADED.
        private Material BuildBlockMaterial()
        {
            if (loopBlockMaterial != null)
            {
                return new Material(loopBlockMaterial);
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            Material material = new Material(shader);
            material.color = loopBlockColor;
            return material;
        }

        // Godot: func add_marker(text_value, marker_position, marker_color, font_size, pixel_size)
        private void AddMarker(string text, float preScaleX, float preScaleYOffset, int fontSize, Color color, Transform parent = null)
        {
            Transform target = parent != null ? parent : markersParent;
            if (target == null) return;

            GameObject markerObj = new GameObject("LoopMarker_" + text.Replace("\n", "_"));
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
            tmp.enableWordWrapping = false; // Phase 45 fix — see ArrayModule.CreateIndexMarker()

            markerObj.AddComponent<Billboard>();
        }

        // Godot: func add_arrow(marker_position: Vector3)
        private void AddArrow(float preScaleX)
        {
            AddMarker("→", preScaleX, 0f, ArrowFontSize, Color.white);
        }

        /// Godot: func set_values_from_text(text_value: String)
        /// Returns false for empty input, an empty comma part, a
        /// non-integer part, or more than six values — see note 7.
        public bool SetValuesFromText(string textValue)
        {
            if (isAnimating) return false;
            if (textValue == null) return false;

            string[] parts = textValue.Split(',');
            if (parts.Length == 0) return false;

            List<int> newValues = new List<int>();

            foreach (string part in parts)
            {
                string cleaned = part.Trim();

                if (string.IsNullOrEmpty(cleaned)) return false;
                if (!int.TryParse(cleaned, out int parsed)) return false;

                newValues.Add(parsed);
            }

            if (newValues.Count == 0) return false;
            if (newValues.Count > MaxValues) return false;

            values = newValues;
            currentIndex = -1;
            runningTotal = 0;
            visitedCount = 0;
            loopMessage = "for value\nin numbers";

            DrawLoop(true);

            return true;
        }

        /// Godot: func step_loop()
        /// onComplete receives the running total, or null when the loop was
        /// already finished (source returns null in that case).
        public IEnumerator StepLoop(Action<int?> onComplete = null)
        {
            if (isAnimating || values.Count == 0)
            {
                onComplete?.Invoke(null);
                yield break;
            }

            if (currentIndex >= values.Count - 1)
            {
                loopMessage = "done\ntotal = " + runningTotal;
                DrawLoop(true);
                onComplete?.Invoke(null);
                yield break;
            }

            currentIndex++;
            visitedCount = currentIndex + 1;

            int currentValue = values[currentIndex];
            runningTotal += currentValue;

            loopMessage = "value = " + currentValue + "\ntotal = " + runningTotal;

            // Redraw first — see class comment note 8. This rebuilds every
            // cell, so the animation below targets a freshly created one.
            DrawLoop(true);

            if (currentIndex >= currentCells.Count)
            {
                onComplete?.Invoke(runningTotal);
                yield break;
            }

            Transform activeCell = currentCells[currentIndex];

            if (activeCell == null)
            {
                onComplete?.Invoke(runningTotal);
                yield break;
            }

            isAnimating = true;

            Vector3 originalScale = activeCell.localScale;

            yield return ScaleTo(activeCell, originalScale * 1.20f, 0.20f, true);
            yield return new WaitForSeconds(0.15f);
            yield return ScaleTo(activeCell, originalScale, 0.20f, false);

            // Briefly highlight the result box so the changing total is visible.
            if (outputCell != null)
            {
                Vector3 outputOriginalScale = outputCell.localScale;

                yield return ScaleTo(outputCell, outputOriginalScale * 1.16f, 0.18f, true);
                yield return ScaleTo(outputCell, outputOriginalScale, 0.18f, false);
            }

            isAnimating = false;

            DrawLoop(true);

            onComplete?.Invoke(runningTotal);
        }

        /// Godot: func run_loop()
        public IEnumerator RunLoop(Action<int> onComplete = null)
        {
            if (isAnimating)
            {
                onComplete?.Invoke(runningTotal);
                yield break;
            }

            if (values.Count == 0)
            {
                currentIndex = -1;
                runningTotal = 0;
                visitedCount = 0;
                loopMessage = "done\ntotal = 0";
                DrawLoop(true);
                onComplete?.Invoke(0);
                yield break;
            }

            currentIndex = -1;
            runningTotal = 0;
            visitedCount = 0;
            loopMessage = "starting\ntotal = 0";

            DrawLoop(true);

            yield return new WaitForSeconds(0.30f);

            int finalTotal = 0;
            int iterations = values.Count;

            for (int i = 0; i < iterations; i++)
            {
                yield return StepLoop(t => { if (t.HasValue) finalTotal = t.Value; });

                // Pause so learners can see each loop update.
                yield return new WaitForSeconds(0.35f);
            }

            // Remove the CURRENT marker once the full loop finishes.
            currentIndex = -1;
            loopMessage = "done\ntotal = " + runningTotal;

            DrawLoop(true);

            onComplete?.Invoke(finalTotal);
        }

        // Godot: func reset_loop()
        public void ResetLoop()
        {
            if (isAnimating) return;

            values = new List<int> { 10, 20, 30 };
            currentIndex = -1;
            runningTotal = 0;
            visitedCount = 0;
            loopMessage = "for value\nin numbers";

            DrawLoop(true);
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
