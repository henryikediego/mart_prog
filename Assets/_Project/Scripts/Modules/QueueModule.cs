using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Direct port of queue_module.gd (Phase 19).
///
/// Horizontal layout (like ArrayModule, unlike StackModule's vertical one)
/// — draw_queue() places cells along X exactly the way draw_array() does,
/// so this was built directly against ArrayModule.cs's already-validated
/// shape rather than StackModule.cs's. Reuses the same shared ArrayCell
/// prefab (identical value-box styling) and the same
/// SetValueLabelColor()/ResetValueLabelColor() methods StackModule already
/// added to ArrayCell.cs for its Search/Traverse label-tinting highlight —
/// queue_module.gd's search_value()/traverse_queue() tint the label the
/// same way, so no further ArrayCell.cs changes were needed for Queue.
///
/// Porting notes specific to Queue — confirmed by reading queue_module.gd
/// directly, not assumed from Array/Stack by structural similarity:
///
/// 1. Enqueue has NO entrance animation. enqueue_value() in the source just
///    appends and redraws — no tween is ever started, and is_animating is
///    never set true by this function (there's nothing to guard). This is
///    a genuine, deliberate difference from both Array's Insert and
///    Stack's Push, which both animate their new cell — not a gap to "fix"
///    by adding one to match its siblings.
/// 2. Dequeue DOES animate: the front cell exits to the LEFT (negative X)
///    while shrinking to zero, both parallel TRANS_BACK/EASE_IN tweens
///    over 0.35s — same "single eased-t loop drives both properties"
///    technique already used for StackModule.AnimatePoppedCell, just
///    horizontal instead of vertical.
/// 3. FRONT/REAR markers sit directly ABOVE their respective cell (FRONT
///    above the leftmost/index-0 cell, REAR above the rightmost/last-index
///    cell, sharing that cell's own X position exactly) — not beside the
///    whole structure the way Stack's TOP/BOTTOM markers sit, and not
///    below each cell the way Array's index markers sit. A third, distinct
///    marker-placement pattern. Because each marker's X is literally the
///    same as its cell's X (not a separately-tuned offset), this shape
///    can't develop the same "offset doesn't scale with Cell Size
///    Multiplier" drift Stack's markers had (Phase 18 on-device finding)
///    — but the vertical gap above the box still uses that same
///    now-established fix proactively (see markerVerticalGap below) rather
///    than waiting to rediscover the lesson on-device. Colors are also
///    swapped from Stack's convention: REAR (the insertion end) is amber
///    (1.0, 0.85, 0.1), FRONT is blue (0.4, 0.8, 1.0) — ported exactly as
///    given in source, not assumed to mirror Stack's TOP/BOTTOM colors.
/// 4. Search/Traverse iterate FRONT to REAR, which is simply forward
///    (index 0 to Count-1) through `values` — values[0] IS the front, so
///    unlike StackModule (which has to reverse-iterate because values[^1]
///    is the top), no index math or reversal is needed here. The Godot
///    source actually collects labels via a recursive tree search and
///    sorts them by X position (_collect_queue_search_labels) rather than
///    using its own already-tracked current_queue_cells array — Unity's
///    port simplifies this to iterating currentCells directly in order,
///    which produces the same observable front-to-rear behavior without
///    re-discovering an order the module already knows.
/// 5. Same source quirks as Stack's Search/Traverse: no draw/redraw first,
///    no isAnimating set/check — matches handle_queue_search()/
///    traverse_queue() exactly, safe for the same reason (QueueController
///    disables every button for the duration of any operation).
/// 6. Godot's absolute visual_center_y (2.45) is dropped for the same
///    reason as Stack's — centers on this module's own arHeight instead.
/// 7. PeekValue()/HighlightPeek() are ported for parity only — confirmed
///    via source search that handle_queue_peek() has no caller anywhere
///    in ar_learning_screen.gd, same as Stack's peek.
/// </summary>
public class QueueModule : MonoBehaviour
{
    [Header("Assign in the Inspector")]
    [Tooltip("Same ArrayCell prefab used by ArrayModule/StackModule — the value box visuals are identical.")]
    [SerializeField] private GameObject cellPrefab;   // was: @export var cell_scene
    [SerializeField] private Transform cellsParent;   // was: $Cells (also holds FRONT/REAR markers, matching Godot)

    [Header("AR placement tuning (Godot units are too big for real-world AR)")]
    [Tooltip("Master multiplier shrinking the whole Godot-scale layout into real-world metres. Lower = smaller/closer together.")]
    [SerializeField] private float arScale = 0.12f;
    [Tooltip("How far in front of the AR origin (metres) the queue sits.")]
    [SerializeField] private float arDistance = 0.5f;
    [Tooltip("Height (metres) of the queue relative to the AR origin. Godot's absolute visual_center_y (2.45) is intentionally not ported — see class comment note 6.")]
    [SerializeField] private float arHeight = 0f;
    [Tooltip("Grows/shrinks each box (and its child text) without changing spacing between boxes.")]
    [SerializeField] private float cellSizeMultiplier = 1.4f;
    [Tooltip("Tightens or widens the horizontal gap between boxes without changing their size.")]
    [SerializeField] private float spacingMultiplier = 0.6f;

    [Header("FRONT/REAR marker appearance")]
    [Tooltip("Gap (metres, before arScale) ABOVE the box's own top edge where the FRONT/REAR markers sit — computed relative to the box's actual half-height, applying the Phase 18 Stack marker-scaling fix proactively instead of waiting to rediscover it on-device.")]
    [SerializeField] private float markerVerticalGap = 0.3f;
    [SerializeField] private float markerBaseScale = 0.06f;
    [SerializeField] private int markerFontSize = 36;
    [Tooltip("Pulls markers toward the camera, clear of box occlusion — applying the Phase 15 Array lesson (Migration Spec §5) from the start instead of discovering it again on-device.")]
    [SerializeField] private float markerForwardOffset = 0.08f;

    private const int MaxQueueValues = 6; // module-level cap — see QueueController's tighter, controller-enforced QUEUE_MAX_VALUES (5)

    public List<int> values = new List<int> { 10, 20, 30 };
    public bool isAnimating = false;

    private List<GameObject> currentCells = new List<GameObject>();
    private GameObject currentFrontCell;
    private GameObject currentRearCell;
    private string lastActionMessage = "";

    private readonly Dictionary<Renderer, Color> originalColors = new Dictionary<Renderer, Color>();

    private void Start()
    {
        DrawQueue();
    }

    // Godot: func get_queue_scale_factor() -> float
    private float GetQueueScaleFactor()
    {
        if (values.Count <= 3) return 0.90f;
        if (values.Count == 4) return 0.80f;
        if (values.Count == 5) return 0.75f;
        return 0.60f;
    }

    // Godot: func get_queue_spacing(scale_factor: float) -> float
    private float GetQueueSpacing()
    {
        if (values.Count <= 3) return 2.45f;
        if (values.Count == 4) return 2.15f;
        if (values.Count == 5) return 1.85f;
        return 1.60f;
    }

    // Godot: func draw_queue(force_redraw := false)
    public void DrawQueue(bool forceRedraw = false)
    {
        if (isAnimating && !forceRedraw)
        {
            return;
        }

        ClearContainer(cellsParent);
        currentCells.Clear();
        currentFrontCell = null;
        currentRearCell = null;

        float scaleFactor = GetQueueScaleFactor();
        float spacing = GetQueueSpacing() * spacingMultiplier;

        float totalWidth = values.Count > 1 ? (values.Count - 1) * spacing : 0f;

        // Centered on arHeight/X=0 instead of Godot's absolute
        // visual_center_y — see class comment note 6.
        float startX = -totalWidth / 2f;

        for (int i = 0; i < values.Count; i++)
        {
            GameObject cell = Instantiate(cellPrefab, cellsParent);
            float cellX = startX + i * spacing;

            cell.name = "QueueCell_" + i;
            cell.transform.localPosition = new Vector3(
                cellX * arScale,
                arHeight,
                arDistance
            );
            cell.transform.localScale = Vector3.one * scaleFactor * arScale * cellSizeMultiplier;
            cell.transform.localRotation = Quaternion.identity;

            // Fix (Phase 18 on-device finding, applied proactively here —
            // see ArrayModule.cs/StackModule.cs/Billboard.cs): guarantees
            // the cell is camera-facing regardless of whether the shared
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

            if (i == 0)
            {
                currentFrontCell = cell;
            }

            if (i == values.Count - 1)
            {
                currentRearCell = cell;
            }
        }

        // FRONT/REAR markers — directly above their own cell, not beside
        // the whole row (see class comment note 3).
        if (values.Count == 1)
        {
            CreateQueueMarker(
                "FRONT/REAR",
                scaleFactor,
                startX,
                new Color(1.0f, 0.85f, 0.1f, 1f));
        }
        else if (values.Count > 1)
        {
            CreateQueueMarker(
                "FRONT",
                scaleFactor,
                startX,
                new Color(0.4f, 0.8f, 1.0f, 1f));

            CreateQueueMarker(
                "REAR",
                scaleFactor,
                startX + (values.Count - 1) * spacing,
                new Color(1.0f, 0.85f, 0.1f, 1f));
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

    // Godot: func add_queue_marker(text_value, marker_position, marker_color)
    private void CreateQueueMarker(string text, float scaleFactor, float cellOffsetX, Color color)
    {
        // Marker shares its cell's own X exactly (matches source) — see
        // class comment note 3 for why this can't drift the way Stack's
        // side-offset markers did.
        float markerX = cellOffsetX * arScale;

        float boxHalfHeight = 0.25f * scaleFactor * arScale * cellSizeMultiplier;
        float markerY = arHeight + boxHalfHeight + markerVerticalGap * arScale;

        GameObject markerObj = new GameObject("QueueMarker_" + text.Replace("/", "_"));
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

        // Fix (Phase 18 on-device finding, applied proactively): markers
        // built fresh in code need an explicit Billboard component — there
        // is no prefab for them to inherit one from.
        markerObj.AddComponent<Billboard>();
    }

    // Godot: func enqueue_value(new_value: int) -> bool
    // No entrance animation — see class comment note 1. isAnimating is
    // never set true here because there is nothing to guard.
    public bool EnqueueValue(int newValue)
    {
        if (isAnimating)
        {
            lastActionMessage = "Please wait. An animation is still running.";
            return false;
        }

        if (IsQueueFull())
        {
            lastActionMessage =
                "Enqueue blocked. The queue is full. Maximum allowed values: " +
                MaxQueueValues + ". Dequeue a value before adding another one.";
            DrawQueue();
            return false;
        }

        values.Add(newValue);

        lastActionMessage = "Value " + newValue + " was added to the rear of the queue.";

        DrawQueue();

        return true;
    }

    // Godot: func dequeue_value()
    // DOES animate — front cell exits left while shrinking (see class
    // comment note 2).
    public IEnumerator DequeueValue(Action<int?> onComplete)
    {
        if (isAnimating || values.Count == 0)
        {
            onComplete?.Invoke(null);
            yield break;
        }

        int removedValue = values[0];

        if (currentFrontCell == null)
        {
            // Edge case matching the source: no cell reference to animate,
            // just remove and redraw.
            values.RemoveAt(0);
            DrawQueue();
            onComplete?.Invoke(removedValue);
            yield break;
        }

        isAnimating = true;

        GameObject frontCell = currentFrontCell;
        yield return StartCoroutine(AnimateDequeuedCell(frontCell));

        values.RemoveAt(0);

        isAnimating = false;

        DrawQueue();

        onComplete?.Invoke(removedValue);
    }

    // Godot: func peek_front() — commented out entirely in queue_module.gd,
    // and the handler that would have used it was never called (see class
    // comment note 7), so this was ported for parity only. **Phase 28 changed
    // that:** QueueController now has a Peek button. See its porting note 8.
    public int? PeekValue()
    {
        if (values.Count == 0) return null;
        return values[0];
    }

    // Godot: func highlight_front() — this is what source's dead
    // handle_queue_peek() actually called, since peek_front() is commented
    // out. Phase 28 gave it its first live caller: QueueController.HandlePeek().
    public IEnumerator HighlightPeek(Action<int?> onComplete)
    {
        if (isAnimating || values.Count == 0)
        {
            onComplete?.Invoke(null);
            yield break;
        }

        if (currentFrontCell == null)
        {
            DrawQueue();
        }

        if (currentFrontCell == null)
        {
            onComplete?.Invoke(values[0]);
            yield break;
        }

        isAnimating = true;

        Transform frontTransform = currentFrontCell.transform;
        Vector3 originalScale = frontTransform.localScale;
        ArrayCell cellScript = currentFrontCell.GetComponent<ArrayCell>();

        // DEVIATION from source, at the project owner's request (Phase 28b) —
        // same change as StackModule.HighlightPeek(), same reasoning. Godot's
        // highlight_front() only scales; the peeked cell is now tinted green
        // for the duration and restored afterwards, using the same
        // Color(0.1, 0.85, 0.25) this module already uses to mean "found" at
        // the end of SearchValue().
        SetCellColor(currentFrontCell, new Color(0.1f, 0.85f, 0.25f, 1.0f));
        cellScript?.SetValueLabelColor(Color.white);

        yield return StartCoroutine(ScaleTo(frontTransform, originalScale * 1.25f, 0.2f, true));
        yield return new WaitForSeconds(0.25f);
        yield return StartCoroutine(ScaleTo(frontTransform, originalScale, 0.2f, false));

        RestoreCellColor(currentFrontCell);
        cellScript?.ResetValueLabelColor();

        isAnimating = false;

        onComplete?.Invoke(values[0]);
    }

    // Godot: func search_value(search_value: int) -> int
    // Deliberately does NOT call DrawQueue() first and does NOT set/check
    // isAnimating — matches queue_module.gd exactly (see class comment
    // note 5). Iterates front (index 0) to rear (last index) — already the
    // natural forward order of `values` and `currentCells`, so no reversal
    // or extra index math is needed (contrast StackModule.SearchValue,
    // which iterates backward) — see class comment note 4.
    public IEnumerator SearchValue(int searchValue, Action<int> onComplete)
    {
        if (currentCells.Count == 0)
        {
            onComplete?.Invoke(-1);
            yield break;
        }

        int foundIndex = -1;

        for (int i = 0; i < values.Count; i++)
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

    // Godot: func traverse_queue()
    // Same "no DrawQueue() first, no isAnimating guard" note as
    // SearchValue() — see class comment note 5. No early exit: every cell
    // is visited, front to rear (natural forward order).
    public IEnumerator TraverseQueue(Action onComplete = null)
    {
        if (currentCells.Count == 0)
        {
            onComplete?.Invoke();
            yield break;
        }

        for (int i = 0; i < values.Count; i++)
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

    // Godot: func reset_queue()
    public void ResetQueue()
    {
        if (isAnimating) return;

        values = new List<int> { 10, 20, 30 };
        DrawQueue();
    }

    // Godot: func is_queue_full() -> bool
    public bool IsQueueFull()
    {
        return values.Count >= MaxQueueValues;
    }

    // Godot: func get_last_action_message() -> String
    public string GetLastActionMessage()
    {
        return lastActionMessage;
    }

    // Godot: dequeue_value()'s parallel position+scale tween (exit left +
    // shrink to zero, both TRANS_BACK/EASE_IN, both 0.35s). Same "one loop,
    // one eased t drives both properties" technique as
    // StackModule.AnimatePoppedCell — see class comment note 2.
    private IEnumerator AnimateDequeuedCell(GameObject cell)
    {
        if (cell == null) yield break;

        Vector3 originalScale = cell.transform.localScale;
        Vector3 originalPosition = cell.transform.localPosition;
        Vector3 targetPosition = originalPosition + new Vector3(-1.4f * arScale, 0, 0);

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

    // ----- Color helpers (same pattern as ArrayModule/StackModule) -----

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

    private void RestoreCellColor(GameObject cell)
    {
        Renderer r = FindRenderer(cell);
        if (r == null) return;

        if (originalColors.TryGetValue(r, out Color original))
        {
            r.material.color = original;
        }
    }

    // ----- Tweening helpers (same pattern as ArrayModule/StackModule) -----

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
}
