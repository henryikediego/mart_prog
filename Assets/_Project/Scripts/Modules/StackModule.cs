using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Direct port of stack_module.gd (Phase 18).
///
/// Mirrors ArrayModule.cs's overall shape (coroutine + Action-callback
/// pattern instead of Godot's `await`, the same tweening-helper style, the
/// same shared ArrayCell prefab for value boxes) but differs from it in a
/// few source-driven ways worth calling out up front:
///
/// 1. Layout is VERTICAL, not horizontal. Godot's draw_stack() stacks cells
///    upward along Y (Vector3(0, start_y + i*spacing, 0)) instead of along
///    X like array_module.gd's draw_array(). The AR-scale-down layer below
///    (arScale/arDistance/arHeight) follows the same convention Array
///    established (Migration Spec §5): Godot's absolute world-space
///    constant (`visual_center_y = 2.45`, used there only to clear Godot's
///    own bottom UI panel) is NOT ported as a number — it has no meaning in
///    AR — the stack is simply centered on arHeight instead, exactly like
///    Array centers on X = 0.
///
/// 2. Cells reuse the existing ArrayCell prefab/script. draw_stack()'s
///    value-label styling (font, bold, black outline, blanked index label)
///    is identical to what ArrayCell.SetValueOnly() already does for
///    Array/List, so no new cell script or prefab was needed.
///
/// 3. TOP/BOTTOM markers sit beside the stack (offset in X), not below each
///    cell like Array's index markers. A `stackMarkerForwardOffset` field
///    is included from the start (pulling markers toward the camera) to
///    proactively apply the Phase 15 lesson (Migration Spec §5) about
///    floating markers getting occluded by box geometry — Array only added
///    this after discovering the problem on-device; Stack shouldn't have
///    to rediscover it.
///
/// 4. Two source quirks are preserved deliberately, not fixed, per this
///    project's "port, don't redesign" rule:
///    - search_value()/traverse_stack() do NOT call draw_stack() first and
///      do NOT set/check isAnimating, unlike ArrayModule's SearchValue()/
///      TraverseArray(). This is safe here because StackController disables
///      every button for the duration of any operation (the same
///      Unity-side safety net ArrayListController already uses), so the
///      gap in the source's own guarding is never actually reachable from
///      the UI.
///    - search_value() only includes the found index in its result when
///      the match is at the very top of the stack (position_from_top == 0)
///      — an asymmetry in the original handler, ported as-is.
///
/// 5. Push and Pop both use "parallel" position+scale tweens where Array
///    only ever needed one property at a time. Since both properties share
///    the same duration and easing curve in the source, a single lerp loop
///    driving both from one eased `t` is equivalent to Godot's two
///    parallel tweens and avoids needing a general-purpose tween library.
///
/// 6. Camera-facing fix (found during Phase 18 on-device testing, applies
///    to Array/List too — see ArrayModule.cs / Billboard.cs): cells and
///    the TOP/BOTTOM markers now explicitly get a Billboard component so
///    they actually face the camera. Billboard.cs itself was also fixed
///    at the same time — it previously constrained rotation to yaw-only,
///    which left a visible skew whenever the stack was viewed from a
///    steep angle (e.g. looking down at a tabletop, the normal way to
///    view an AR stack sitting on a surface).
/// </summary>
public class StackModule : MonoBehaviour
{
    [Header("Assign in the Inspector")]
    [Tooltip("Same ArrayCell prefab used by ArrayModule — the value box visuals are identical.")]
    [SerializeField] private GameObject cellPrefab;   // was: @export var cell_scene
    [SerializeField] private Transform cellsParent;   // was: $Cells (also holds TOP/BOTTOM markers, matching Godot)

    [Header("AR placement tuning (Godot units are too big for real-world AR)")]
    [Tooltip("Master multiplier shrinking the whole Godot-scale layout into real-world metres. Lower = smaller/closer together.")]
    [SerializeField] private float arScale = 0.12f;
    [Tooltip("How far in front of the AR origin (metres) the stack sits.")]
    [SerializeField] private float arDistance = 0.5f;
    [Tooltip("Height (metres) of the stack's vertical CENTER relative to the AR origin. Godot's absolute visual_center_y (2.45) is intentionally not ported — see class comment note 1.")]
    [SerializeField] private float arHeight = 0f;
    [Tooltip("Grows/shrinks each box (and its child text) without changing spacing between boxes.")]
    [SerializeField] private float cellSizeMultiplier = 1.4f;
    [Tooltip("Tightens or widens the vertical gap between boxes without changing their size.")]
    [SerializeField] private float spacingMultiplier = 0.6f;

    [Header("TOP/BOTTOM marker appearance")]
    [Tooltip("Gap (metres, before arScale) BEYOND the box's own right edge where the TOP/BOTTOM markers sit. Fixed (Phase 18 on-device finding) to be relative to the box's actual half-width instead of a raw from-center offset, so markers now track Cell Size Multiplier automatically instead of visually drifting away from bigger boxes. Was 1.75 back when this was a from-center distance -- start much smaller (~0.15-0.3) now.")]
    [SerializeField] private float markerSideOffset = 0.25f;
    [Tooltip("Same gap-beyond-the-box's-edge as Marker Side Offset, used only for the single-item combined 'TOP/BOTTOM' marker (kept slightly wider than Marker Side Offset for the longer label, same reasoning as before the fix).")]
    [SerializeField] private float markerSideOffsetSingleItem = 0.35f;
    [Tooltip("Phase 33, requested on device: place TOP above the top box and " +
             "BOTTOM below the bottom box, instead of beside them. The stack is " +
             "VERTICAL, so a marker offset sideways is pushed out along the axis a " +
             "phone has least room in — in portrait the labels ran off screen. " +
             "Placing them on the stack's own axis uses the room that is actually " +
             "there. Marker Side Offset is ignored while this is on; Marker Vertical " +
             "Gap is used instead. Turn off to restore the beside-the-stack layout.")]
    [SerializeField] private bool placeMarkersVertically = true;
    [Tooltip("Gap (metres, before arScale) BEYOND the top/bottom box's own edge where " +
             "the markers sit when Place Markers Vertically is on. Measured from the " +
             "box edge, not from centre, so it tracks Cell Size Multiplier — the same " +
             "convention as Marker Side Offset.")]
    [SerializeField] private float markerVerticalGap = 0.30f;
    [SerializeField] private float markerBaseScale = 0.06f;
    [SerializeField] private int markerFontSize = 36;
    [Tooltip("Pulls markers toward the camera, clear of box occlusion — applying the Phase 15 Array lesson (Migration Spec §5) from the start instead of discovering it again on-device.")]
    [SerializeField] private float markerForwardOffset = 0.08f;

    private const int MaxStackValues = 6; // module-level cap — see StackController's tighter, controller-enforced STACK_MAX_VALUES (5)

    public List<int> values = new List<int> { 10, 20, 30 };
    public bool isAnimating = false;

    private List<GameObject> currentCells = new List<GameObject>();
    private GameObject currentTopCell = null;
    private string lastActionMessage = "";
    private int lastPushedIndex = -1;

    private readonly Dictionary<Renderer, Color> originalColors = new Dictionary<Renderer, Color>();

    private void Start()
    {
        DrawStack();
    }

    // Godot: func get_stack_scale_factor() -> float
    private float GetStackScaleFactor()
    {
        if (values.Count <= 3) return 0.92f;
        if (values.Count == 4) return 0.80f;
        if (values.Count == 5) return 0.68f;
        return 0.56f;
    }

    // Godot: func get_stack_spacing(scale_factor: float) -> float
    private float GetStackSpacing()
    {
        if (values.Count <= 3) return 1.15f;
        if (values.Count == 4) return 1.00f;
        if (values.Count == 5) return 0.86f;
        return 0.74f;
    }

    // Godot: func draw_stack()
    public void DrawStack(bool forceRedraw = false)
    {
        if (isAnimating && !forceRedraw)
        {
            return;
        }

        // Phase 30 fix (F10): Instantiate(null, parent) throws
        // ArgumentException. Start() draws immediately against the default
        // values, so an unassigned prefab crashed on the FIRST frame this
        // module was active, before any button was pressed.
        //
        // FunctionModule, LoopModule and SelectionModule already guarded this
        // at their own Instantiate sites; the four earlier modules never got
        // it. Guarding here rather than at the Instantiate call so the loop
        // cannot spin once per value throwing each time.
        if (cellPrefab == null || cellsParent == null)
        {
            Debug.LogError("StackModule: cellPrefab or cellsParent is not assigned; " +
                           "nothing can be drawn. Assign both in the Inspector.");
            return;
        }

        ClearContainer(cellsParent);
        currentCells.Clear();
        currentTopCell = null;

        float scaleFactor = GetStackScaleFactor();
        float spacing = GetStackSpacing() * spacingMultiplier;

        float totalHeight = values.Count > 1 ? (values.Count - 1) * spacing : 0f;

        // Centered on arHeight instead of Godot's absolute visual_center_y —
        // see class comment note 1.
        float startOffset = -totalHeight / 2f;

        for (int i = 0; i < values.Count; i++)
        {
            GameObject cell = Instantiate(cellPrefab, cellsParent);
            float cellOffset = startOffset + i * spacing;

            cell.name = "StackCell_" + i;
            cell.transform.localPosition = new Vector3(
                0f,
                arHeight + cellOffset * arScale,
                arDistance
            );
            cell.transform.localScale = Vector3.one * scaleFactor * arScale * cellSizeMultiplier;
            cell.transform.localRotation = Quaternion.identity;

            // Fix (Phase 18 on-device finding, applies to Array/List too
            // — see ArrayModule.cs / Billboard.cs): guarantees the cell
            // is camera-facing regardless of whether the shared
            // ArrayCell prefab already carries a Billboard component.
            if (cell.GetComponent<Billboard>() == null)
            {
                cell.AddComponent<Billboard>();
            }

            ArrayCell cellScript = cell.GetComponent<ArrayCell>();
            if (cellScript != null)
            {
                cellScript.SetValueOnly(values[i].ToString());
            }

            currentCells.Add(cell);

            if (i == values.Count - 1)
            {
                currentTopCell = cell;
            }
        }

        // Stack markers. Source places them beside the stack (class comment
        // note 3); Phase 33 added the option to place them above and below
        // instead — see placeMarkersVertically.
        if (values.Count == 1)
        {
            CreateStackMarker(
                "TOP/BOTTOM",
                scaleFactor,
                markerSideOffsetSingleItem,
                startOffset,
                +1,
                new Color(1.0f, 0.85f, 0.1f, 1f));
        }
        else if (values.Count > 1)
        {
            CreateStackMarker(
                "TOP",
                scaleFactor,
                markerSideOffset,
                startOffset + (values.Count - 1) * spacing,
                +1,
                new Color(1.0f, 0.85f, 0.1f, 1f));

            CreateStackMarker(
                "BOTTOM",
                scaleFactor,
                markerSideOffset,
                startOffset,
                -1,
                new Color(0.4f, 0.8f, 1.0f, 1f));
        }
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

    // Godot: func add_stack_marker(text_value, marker_position, marker_color)
    //
    // Fix (Phase 18 on-device finding): sideGap is now added to the box's
    // OWN half-width (same pattern ArrayModule.cs already uses for its
    // index markers' vertical offset via boxHalfHeight) instead of being
    // used as a raw absolute offset from the stack's center. Previously
    // the marker's X position only scaled with Ar Scale, never with Cell
    // Size Multiplier -- so once boxes were enlarged via Cell Size
    // Multiplier for legibility, the marker no longer tracked the box's
    // actual edge and visually drifted away from it. The sideGap
    // parameter keeps the same field names in the Inspector (Marker Side
    // Offset / Marker Side Offset Single Item), but now means "gap
    // beyond the box's edge" rather than "distance from center" -- their
    // old 1.75/1.95 values were calibrated as from-center distances, so
    // expect to dial them down to something much smaller (start around
    // 0.15–0.3) after picking up this fix.
    /// <param name="verticalDirection">
    /// +1 places the marker above its box, -1 below. Only used when
    /// placeMarkersVertically is on.
    /// </param>
    private void CreateStackMarker(
        string text, float scaleFactor, float sideGap, float verticalOffset,
        int verticalDirection, Color color)
    {
        // The prefab's Box child has a local scale of 0.5, so its half-extent
        // before this module's own scaling is 0.25 — the same figure the
        // side-offset maths already used.
        float boxHalfExtent = 0.25f * scaleFactor * arScale * cellSizeMultiplier;

        float markerX;
        float markerY;

        if (placeMarkersVertically)
        {
            // Phase 33, requested on device. The stack grows along Y, so a
            // marker offset along X is pushed out on the axis a phone in
            // portrait has LEAST room in — and because the offset is a fixed
            // gap beyond the box edge, no amount of scaling brings it back.
            // Exactly the shape of problem Memory's address marker had, and
            // the same answer: move it onto an axis that has room.
            markerX = 0f;
            markerY = arHeight
                      + verticalOffset * arScale
                      + verticalDirection * (boxHalfExtent + markerVerticalGap * arScale);
        }
        else
        {
            markerX = boxHalfExtent + sideGap * arScale;
            markerY = arHeight + verticalOffset * arScale;
        }

        GameObject markerObj = new GameObject("StackMarker_" + text.Replace("/", "_"));
        markerObj.transform.SetParent(cellsParent, false);
        markerObj.transform.localPosition = new Vector3(
            markerX,
            markerY,
            arDistance - markerForwardOffset
        );
        markerObj.transform.localScale = Vector3.one * markerBaseScale * arScale * cellSizeMultiplier;
        markerObj.transform.localRotation = Quaternion.identity;

        TextMeshPro tmp = markerObj.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = markerFontSize;
        tmp.color = color;
        tmp.fontStyle = FontStyles.Bold;
        tmp.outlineWidth = 0.25f;
        tmp.outlineColor = Color.black;
        tmp.alignment = TextAlignmentOptions.Center;

        // Fix (Phase 18 on-device finding): TOP/BOTTOM markers are built
        // fresh in code and were never given a Billboard component, so
        // they sat at a fixed world rotation — same root cause as
        // Array/List's index markers (see ArrayModule.cs), just found
        // here first since Stack was the module actually being
        // on-device-tuned this round.
        markerObj.AddComponent<Billboard>();
    }

    // Godot: func push_value(new_value: int) -> bool
    public IEnumerator PushValue(int newValue, Action<bool> onComplete)
    {
        if (isAnimating)
        {
            lastActionMessage = "Please wait. An animation is still running.";
            onComplete?.Invoke(false);
            yield break;
        }

        if (IsStackFull())
        {
            lastActionMessage =
                "Push blocked. The stack is full. Maximum allowed values: " +
                MaxStackValues + ". Pop a value before pushing another one.";
            DrawStack(true);
            onComplete?.Invoke(false);
            yield break;
        }

        values.Add(newValue);
        lastPushedIndex = values.Count - 1;

        lastActionMessage = "Value " + newValue + " was pushed to the top of the stack.";

        // Redraw first so currentTopCell points at the newly added cell.
        DrawStack(true);

        if (currentTopCell != null)
        {
            isAnimating = true;
            yield return StartCoroutine(AnimatePushedCell(currentTopCell));
            isAnimating = false;
        }

        lastPushedIndex = -1;
        onComplete?.Invoke(true);
    }

    // Godot: func pop_value()
    public IEnumerator PopValue(Action<int?> onComplete)
    {
        if (isAnimating || values.Count == 0)
        {
            onComplete?.Invoke(null);
            yield break;
        }

        int removedValue = values[values.Count - 1];

        if (currentTopCell == null)
        {
            // Edge case matching the source: no cell reference to animate,
            // just remove and redraw.
            values.RemoveAt(values.Count - 1);
            DrawStack();
            onComplete?.Invoke(removedValue);
            yield break;
        }

        isAnimating = true;

        GameObject topCell = currentTopCell;
        yield return StartCoroutine(AnimatePoppedCell(topCell));

        values.RemoveAt(values.Count - 1);
        isAnimating = false;

        DrawStack();

        onComplete?.Invoke(removedValue);
    }

    // Godot: func peek_value() — was not wired to any button in the
    // original app (see Migration Spec §11), so this was ported for parity
    // only. **Phase 28 changed that:** StackController now has a Peek button
    // that calls HighlightPeek() below. See StackController porting note 9.
    public int? PeekValue()
    {
        if (values.Count == 0) return null;
        return values[values.Count - 1];
    }

    // Godot: func highlight_peek() — dead in source alongside PeekValue().
    // Phase 28 gave it its first caller: StackController.HandlePeek().
    public IEnumerator HighlightPeek(Action<int?> onComplete)
    {
        if (isAnimating || values.Count == 0)
        {
            onComplete?.Invoke(null);
            yield break;
        }

        if (currentTopCell == null)
        {
            DrawStack();
        }

        if (currentTopCell == null)
        {
            onComplete?.Invoke(values[values.Count - 1]);
            yield break;
        }

        // Phase 32 fix, reported on device: Search deliberately LEAVES its
        // found cell green — source breaks out of the loop without restoring
        // it, and only redraws when the value was NOT found. That was
        // harmless while Peek was unreachable. Now that Peek is wired and
        // uses the same green, a Search followed by a Peek showed TWO green
        // cells and the student could not tell which one Peek was pointing
        // at.
        //
        // Peek clears every existing highlight before applying its own, so
        // the green on screen always means "this is the cell Peek is showing
        // you". Search's own behaviour is untouched.
        RestoreAllCellColors();

        isAnimating = true;

        Transform topTransform = currentTopCell.transform;
        Vector3 originalScale = topTransform.localScale;
        ArrayCell cellScript = currentTopCell.GetComponent<ArrayCell>();

        // DEVIATION from source, at the project owner's request (Phase 28b).
        // Godot's highlight_peek() only scales the top cell — no colour change
        // at all. On device the scale pulse alone was not enough to say WHICH
        // cell was peeked, so the peeked cell is now tinted green for the
        // duration of the animation and restored afterwards.
        //
        // The green is deliberately the SAME literal this module already uses
        // to mean "found" at the end of SearchValue() — Color(0.1, 0.85, 0.25)
        // — rather than a new shade. Both modules and ArrayModule use it, and
        // in Godot too, so "green = this is the cell you asked about" stays
        // one idea across the app instead of becoming three similar greens.
        SetCellColor(currentTopCell, new Color(0.1f, 0.85f, 0.25f, 1.0f));
        cellScript?.SetValueLabelColor(Color.white);

        yield return StartCoroutine(ScaleTo(topTransform, originalScale * 1.25f, 0.2f, true));
        yield return new WaitForSeconds(0.25f);
        yield return StartCoroutine(ScaleTo(topTransform, originalScale, 0.2f, false));

        RestoreCellColor(currentTopCell);
        cellScript?.ResetValueLabelColor();

        isAnimating = false;

        onComplete?.Invoke(values[values.Count - 1]);
    }

    // Godot: func search_value(search_value: int) -> int
    // Deliberately does NOT call DrawStack() first and does NOT set/check
    // isAnimating — matches stack_module.gd exactly (see class comment
    // note 4). Iterates TOP to BOTTOM, i.e. from the last stack index down
    // to 0 — since that's already the real stack index (bottom = 0), no
    // extra "position from top" math is needed here the way Godot's
    // sorted-label-list approach required; StackController computes
    // position-from-top itself from the returned index.
    public IEnumerator SearchValue(int searchValue, Action<int> onComplete)
    {
        if (currentCells.Count == 0)
        {
            onComplete?.Invoke(-1);
            yield break;
        }

        int foundIndex = -1;

        for (int i = values.Count - 1; i >= 0; i--)
        {
            if (i >= currentCells.Count) continue;

            GameObject cell = currentCells[i];
            ArrayCell cellScript = cell.GetComponent<ArrayCell>();
            Vector3 originalScale = cell.transform.localScale;

            SetCellColor(cell, new Color(1.0f, 0.75f, 0.15f, 1.0f));
            cellScript?.SetValueLabelColor(new Color(1.0f, 0.9f, 0.1f, 1.0f));

            yield return StartCoroutine(ScaleTo(cell.transform, originalScale * 1.20f, 0.18f, true));
            yield return new WaitForSeconds(0.12f);
            yield return StartCoroutine(ScaleTo(cell.transform, originalScale, 0.18f, false));

            if (values[i] == searchValue)
            {
                foundIndex = i;
                SetCellColor(cell, new Color(0.1f, 0.85f, 0.25f, 1.0f));
                cellScript?.SetValueLabelColor(Color.white);
                yield return new WaitForSeconds(0.45f);
                break;
            }

            RestoreCellColor(cell);
            cellScript?.ResetValueLabelColor();
        }

        onComplete?.Invoke(foundIndex);
    }

    // Godot: func traverse_stack()
    // Same "no DrawStack() first, no isAnimating guard" note as
    // SearchValue() — see class comment note 4. No early exit: every cell
    // is visited, top to bottom.
    public IEnumerator TraverseStack(Action onComplete = null)
    {
        if (currentCells.Count == 0)
        {
            onComplete?.Invoke();
            yield break;
        }

        for (int i = values.Count - 1; i >= 0; i--)
        {
            if (i >= currentCells.Count) continue;

            GameObject cell = currentCells[i];
            ArrayCell cellScript = cell.GetComponent<ArrayCell>();
            Vector3 originalScale = cell.transform.localScale;

            SetCellColor(cell, new Color(1.0f, 0.75f, 0.15f, 1.0f));
            cellScript?.SetValueLabelColor(new Color(1.0f, 0.9f, 0.1f, 1.0f));

            yield return StartCoroutine(ScaleTo(cell.transform, originalScale * 1.20f, 0.18f, true));
            yield return new WaitForSeconds(0.12f);
            yield return StartCoroutine(ScaleTo(cell.transform, originalScale, 0.18f, false));

            RestoreCellColor(cell);
            cellScript?.ResetValueLabelColor();
        }

        onComplete?.Invoke();
    }

    // Godot: func reset_stack()
    public void ResetStack()
    {
        if (isAnimating) return;

        values = new List<int> { 10, 20, 30 };
        DrawStack();
    }

    // Godot: func is_stack_full() -> bool
    public bool IsStackFull()
    {
        return values.Count >= MaxStackValues;
    }

    // Godot: func get_last_action_message() -> String
    public string GetLastActionMessage()
    {
        return lastActionMessage;
    }

    // Godot: func animate_pushed_cell(cell: Node3D)
    // Same math as ArrayModule.AnimateInsertedCell — see class comment
    // note 5.
    private IEnumerator AnimatePushedCell(GameObject cell)
    {
        if (cell == null) yield break;

        Vector3 originalScale = cell.transform.localScale;
        Vector3 originalPosition = cell.transform.localPosition;

        cell.transform.localScale = originalScale * 0.15f;
        cell.transform.localPosition = originalPosition + new Vector3(0, 0.45f * arScale, 0);

        Vector3 startScale = cell.transform.localScale;
        Vector3 startPosition = cell.transform.localPosition;

        float duration = 0.35f;
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duration);

            cell.transform.localScale = Vector3.LerpUnclamped(startScale, originalScale, EaseOutBack(p));
            cell.transform.localPosition = Vector3.Lerp(startPosition, originalPosition, EaseOutSine(p));

            yield return null;
        }

        cell.transform.localScale = originalScale;
        cell.transform.localPosition = originalPosition;
    }

    // Godot: pop_value()'s parallel position+scale tween (rise + shrink to
    // zero, both TRANS_BACK/EASE_IN, both 0.35s). Since both properties
    // share the same duration/easing, one loop driving both from a single
    // eased t is equivalent to Godot's two parallel tweens — see class
    // comment note 5.
    private IEnumerator AnimatePoppedCell(GameObject cell)
    {
        if (cell == null) yield break;

        Vector3 originalScale = cell.transform.localScale;
        Vector3 originalPosition = cell.transform.localPosition;
        Vector3 targetPosition = originalPosition + new Vector3(0, 1.2f * arScale, 0);

        float duration = 0.35f;
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duration);
            float eased = EaseInBack(p);

            cell.transform.localPosition = Vector3.LerpUnclamped(originalPosition, targetPosition, eased);
            cell.transform.localScale = Vector3.LerpUnclamped(originalScale, Vector3.zero, eased);

            yield return null;
        }

        cell.transform.localPosition = targetPosition;
        cell.transform.localScale = Vector3.zero;
    }

    // ----- Color helpers (same pattern as ArrayModule) -----

    private Renderer FindRenderer(GameObject cell)
    {
        return cell.GetComponentInChildren<Renderer>();
    }

    private void SetCellColor(GameObject cell, Color color)
    {
        Renderer r = FindRenderer(cell);
        if (r == null) return;

        if (!originalColors.ContainsKey(r))
        {
            originalColors[r] = r.material.color;
        }

        r.material.color = color;
    }

    /// <summary>
    /// Restores every currently-drawn cell to its original material and label
    /// colour. Used by Peek to clear a stale Search highlight — see the note
    /// in HighlightPeek(). Safe to call when nothing is highlighted.
    /// </summary>
    private void RestoreAllCellColors()
    {
        foreach (GameObject c in currentCells)
        {
            if (c == null) continue;

            RestoreCellColor(c);
            c.GetComponent<ArrayCell>()?.ResetValueLabelColor();
        }
    }

    private void RestoreCellColor(GameObject cell)
    {
        Renderer r = FindRenderer(cell);
        if (r == null) return;

        if (originalColors.TryGetValue(r, out Color original))
        {
            r.material.color = original;
        }
    }

    // ----- Tweening helpers (same pattern as ArrayModule) -----

    private IEnumerator ScaleTo(Transform target, Vector3 endScale, float duration, bool easeOut)
    {
        Vector3 startScale = target.localScale;
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duration);
            float eased = easeOut ? EaseOutBack(p) : EaseInBack(p);
            target.localScale = Vector3.LerpUnclamped(startScale, endScale, eased);
            yield return null;
        }

        target.localScale = endScale;
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

    // Approximates Godot's TRANS_SINE easing.
    private float EaseOutSine(float x)
    {
        return Mathf.Sin((x * Mathf.PI) / 2f);
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
