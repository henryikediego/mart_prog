using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Direct port of linked_list_module.gd (Phase 21).
///
/// Built directly against QueueModule.cs's already-validated shape (Insert
/// at the tail with no entrance animation, Delete-from-the-front WITH an
/// exit animation, the same coroutine + Action-callback pattern, the same
/// shared ArrayCell prefab, the same fresh-GameObject-plus-TextMeshPro
/// technique for floating labels) since Linked List's Insert/Delete Head
/// operations are structurally the closest match to Queue's Enqueue/
/// Dequeue among the modules already built. Porting notes specific to
/// Linked List — confirmed by reading linked_list_module.gd and
/// ar_learning_screen.gd's linked-list handlers directly, not assumed from
/// Array/Stack/Queue by structural similarity:
///
/// 1. Visual structure is genuinely new, not shared with any sibling
///    module: each node is connected to the next by a "→" arrow label, the
///    tail is followed by one more arrow into a "NULL" marker, and HEAD/
///    TAIL markers sit above the head/tail nodes (combined into a single
///    "HEAD/TAIL" marker for the one-node case, the same combined-marker
///    pattern Stack's TOP/BOTTOM and Queue's FRONT/REAR already use for
///    their own one-value case). Arrows and the NULL marker are built with
///    the same "fresh GameObject + TextMeshPro + Billboard" technique
///    QueueModule.CreateQueueMarker already established — no new component
///    or prefab was needed.
///
/// 2. Insert has NO entrance animation — insert_value() in the source just
///    appends and redraws, exactly like Queue's enqueue_value() (see
///    QueueModule.cs note 1). Confirmed independently by reading
///    insert_value() directly rather than assumed from Queue's precedent.
///
/// 3. Delete Head DOES animate, using the same "single eased-t loop drives
///    a parallel position+scale tween" technique as Queue's Dequeue (head
///    node exits left while shrinking to zero, TRANS_BACK/EASE_IN, 0.35s)
///    — but the exit distance is source-specific: -1.3 Godot units here,
///    NOT Queue's -1.4. Confirmed distinct by reading delete_head()
///    directly; ported as its own tunable (deleteExitDistance) rather than
///    reusing Queue's value.
///
/// 4. Two confirmed-dead code paths, found via full-text search of both
///    ar_learning_screen.gd and this module's own source, and NOT ported:
///    - get_linked_list_scale_factor() / get_linked_list_spacing() /
///      get_linked_list_start_x() — a second, unused set of scale/spacing
///      helpers. draw_linked_list() actually uses get_list_scale_factor()/
///      get_list_spacing() (both ported below as GetListScaleFactor()/
///      GetListSpacing()); the "get_linked_list_*" trio is never called by
///      anything, in this module or in ar_learning_screen.gd.
///    - traverse_list() — a second, unused traversal implementation.
///      ar_learning_screen.gd's handle_linked_list_traverse() only ever
///      calls traverse_linked_list() (via has_method("traverse_linked_list")),
///      never traverse_list().
///    This is a different situation from Stack's/Queue's Peek/HighlightPeek,
///    which — while not wired to any UI button — ARE still live, reachable
///    dispatch targets ar_learning_screen.gd's has_method pattern could call.
///    These four have no such caller anywhere, so they're genuine leftover
///    code from an earlier draft rather than a "built but not yet wired"
///    feature, and porting them would just be dead C# surface area.
///
/// 5. get_list_spacing(scale_factor: float)'s own parameter is unused inside
///    its body (it only switches on values.size()) — dropped here, the same
///    already-established simplification as StackModule.GetStackSpacing()/
///    QueueModule.GetQueueSpacing().
///
/// 6. Depth-offset collapsing: the source places arrows, the NULL marker,
///    and the HEAD/TAIL markers at three slightly different Z depths (0.62,
///    0.60, 0.55 respectively) — but every one of them also sets
///    no_depth_test = true, which disables depth testing entirely, so the
///    exact depth ordering between them has no visible effect. Collapsed to
///    the one shared markerForwardOffset tunable below (matching
///    QueueModule's own markerForwardOffset) rather than three
///    near-duplicate knobs with a difference that was never actually visible.
///
/// 7. Search (search_value()) and Traverse (traverse_linked_list()) both
///    restore each node's color/scale after visiting it (except the node
///    Search actually finds, which stays green) — matching Stack's/Queue's
///    already-established Search/Traverse behavior exactly. Neither calls
///    DrawLinkedList()/sets-or-checks isAnimating first, the same source
///    quirk Stack/Queue already have — safe here for the same reason:
///    LinkedListController disables every button for the duration of any
///    operation, so the gap in the source's own guarding is never actually
///    reachable from the UI.
///
/// 8. Dual-cap pattern — same as Array/Stack/Queue: MaxLinkedListValues (6)
///    here is a redundant backstop; LinkedListController enforces the
///    tighter, actually-effective cap of 5 (matching LINKED_LIST_MAX_VALUES
///    in ar_learning_screen.gd) before ever calling InsertValue().
///
/// 9. Camera-facing: nodes, arrows, and every marker get an explicit
///    Billboard component, applying the Phase 18 fix proactively from the
///    start (as Stack/Queue already do) rather than rediscovering it
///    on-device.
/// </summary>
public class LinkedListModule : MonoBehaviour
{
    [Header("Assign in the Inspector")]
    [Tooltip("Same ArrayCell prefab used by ArrayModule/StackModule/QueueModule — the value box visuals are identical.")]
    [SerializeField] private GameObject cellPrefab;   // was: @export var cell_scene
    [SerializeField] private Transform cellsParent;   // was: $Cells
    [SerializeField] private Transform linksParent;   // was: $Links (arrow labels between nodes, and the tail -> NULL arrow)
    [SerializeField] private Transform markersParent; // was: $Markers (HEAD/TAIL/NULL labels)

    [Header("AR placement tuning (Godot units are too big for real-world AR)")]
    [Tooltip("Master multiplier shrinking the whole Godot-scale layout into real-world metres. Lower = smaller/closer together.")]
    [SerializeField] private float arScale = 0.12f;
    [Tooltip("How far in front of the AR origin (metres) the linked list sits.")]
    [SerializeField] private float arDistance = 0.5f;
    [Tooltip("Height (metres) of the linked list relative to the AR origin. Godot's absolute visual_center_y (2.15) is intentionally not ported — same reasoning as ArrayModule/StackModule/QueueModule.")]
    [SerializeField] private float arHeight = 0f;
    [Tooltip("Grows/shrinks each node box (and its child text) without changing spacing between nodes.")]
    [SerializeField] private float cellSizeMultiplier = 1.4f;
    [Tooltip("Tightens or widens the horizontal gap between nodes without changing their size.")]
    [SerializeField] private float spacingMultiplier = 0.6f;

    [Header("HEAD/TAIL marker appearance")]
    [Tooltip("Gap (metres, before arScale) ABOVE the box's own top edge where the HEAD/TAIL markers sit — same box-half-height-relative technique as QueueModule's FRONT/REAR markers (Phase 18 on-device fix, applied proactively here).")]
    [SerializeField] private float markerVerticalGap = 0.3f;
    [SerializeField] private float markerBaseScale = 0.06f;
    [SerializeField] private int markerFontSize = 36;
    [Tooltip("Pulls arrows/markers toward the camera, clear of node-box occlusion. Shared by arrows, the NULL marker, and the HEAD/TAIL markers alike — see class comment note 6 for why the source's three slightly different Z depths collapse to one tunable here.")]
    [SerializeField] private float markerForwardOffset = 0.08f;

    [Header("Arrow + NULL marker appearance")]
    [SerializeField] private Color arrowColor = Color.white;
    [SerializeField] private Color nullMarkerColor = new Color(0.90f, 0.90f, 0.90f, 1f);
    [Tooltip("Gap (Godot units, before arScale/spacingMultiplier) between the tail node and the NULL marker — matches source's null_gap = spacing * 0.72 ratio exactly.")]
    [SerializeField] private float nullGapRatio = 0.72f;
    [Tooltip("Vertical offset (metres, before arScale) applied to arrows and the NULL marker, on top of arHeight. Added after Phase 21 on-device testing: nodes/arrows shared the exact same Y by default (both at plain arHeight), but the node prefab's value text doesn't visually sit at the box's true geometric center, so arrows/NULL read as sitting too low relative to the visible numbers. This is intentionally separate from HEAD/TAIL's own boxHalfHeight-based offset (which sits ABOVE the box, not at its center) — start with small values (e.g. 0.1-0.2) and tune on-device; unvalidated guess like every other AR tunable in this project.")]
    [SerializeField] private float linkVerticalOffset = 0.15f;

    [Tooltip("Exit distance (Godot units, before arScale) the head node travels left during Delete Head's animation. Matches source's -1.3 exactly — see class comment note 3 (NOT the same as Queue's Dequeue exit distance of -1.4).")]
    [SerializeField] private float deleteExitDistance = 1.3f;

    // Module-level cap — see LinkedListController's tighter, controller-enforced
    // LinkedListMaxValues (5). See class comment note 8.
    private const int MaxLinkedListValues = 6;

    public List<int> values = new List<int> { 10, 20, 30 };
    public bool isAnimating = false;

    private readonly List<GameObject> currentNodes = new List<GameObject>();
    private string lastActionMessage = "";

    private readonly Dictionary<Renderer, Color> originalColors = new Dictionary<Renderer, Color>();

    private void Start()
    {
        DrawLinkedList();
    }

    // Godot: func get_list_scale_factor() -> float
    private float GetListScaleFactor()
    {
        if (values.Count <= 3) return 0.68f;
        if (values.Count == 4) return 0.60f;
        if (values.Count == 5) return 0.53f;
        if (values.Count == 6) return 0.48f;
        return 0.43f;
    }

    // Godot: func get_list_spacing(scale_factor: float) -> float
    // scale_factor param dropped — unused in the source body too. See
    // class comment note 5.
    private float GetListSpacing()
    {
        if (values.Count <= 3) return 2.00f;
        if (values.Count == 4) return 1.75f;
        if (values.Count == 5) return 1.55f;
        if (values.Count == 6) return 1.42f;
        return 1.28f;
    }

    // Godot: func clear_visuals()
    private void ClearVisuals()
    {
        ClearContainer(cellsParent);
        ClearContainer(linksParent);
        ClearContainer(markersParent);
        currentNodes.Clear();
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

    // Godot: func draw_linked_list(force_redraw := false)
    public void DrawLinkedList(bool forceRedraw = false)
    {
        if (isAnimating && !forceRedraw)
        {
            return;
        }

        ClearVisuals();

        float scaleFactor = GetListScaleFactor();
        float spacing = GetListSpacing() * spacingMultiplier;
        float nullGap = spacing * nullGapRatio;

        int nodeCount = values.Count;

        if (nodeCount == 0)
        {
            AddNullMarker(0f);
            return;
        }

        float nodeWidth = nodeCount > 1 ? (nodeCount - 1) * spacing : 0f;
        float startX = -nodeWidth / 2f;

        List<float> nodeXs = new List<float>(nodeCount);

        for (int i = 0; i < nodeCount; i++)
        {
            GameObject cell = Instantiate(cellPrefab, cellsParent);
            float cellX = startX + i * spacing;
            nodeXs.Add(cellX);

            cell.name = "ListNode_" + i;
            cell.transform.localPosition = new Vector3(cellX * arScale, arHeight, arDistance);
            cell.transform.localScale = Vector3.one * scaleFactor * arScale * cellSizeMultiplier;
            cell.transform.localRotation = Quaternion.identity;

            // Fix (Phase 18 on-device finding, applied proactively — see
            // class comment note 9): guarantees the cell is camera-facing
            // regardless of whether the shared ArrayCell prefab already
            // carries a Billboard component.
            if (cell.GetComponent<Billboard>() == null)
            {
                cell.AddComponent<Billboard>();
            }

            ArrayCell cellScript = cell.GetComponent<ArrayCell>();
            if (cellScript != null)
            {
                cellScript.SetValueOnly(values[i].ToString());
            }

            currentNodes.Add(cell);
        }

        // Arrows between neighboring nodes.
        for (int i = 0; i < nodeXs.Count - 1; i++)
        {
            CreateArrowBetween(nodeXs[i], nodeXs[i + 1]);
        }

        // Final arrow + NULL marker after the tail.
        float tailX = nodeXs[nodeXs.Count - 1];
        float nullX = tailX + nullGap;
        CreateArrowBetween(tailX, nullX);
        AddNullMarker(nullX);

        // HEAD/TAIL markers — directly above their own node, not beside the
        // whole structure (same placement idea as Queue's FRONT/REAR).
        if (nodeCount == 1)
        {
            AddHeadTailMarker("HEAD/TAIL", startX, new Color(1.0f, 0.85f, 0.1f, 1f));
        }
        else
        {
            AddHeadTailMarker("HEAD", startX, new Color(0.4f, 0.8f, 1.0f, 1f));
            AddHeadTailMarker("TAIL", tailX, new Color(1.0f, 0.85f, 0.1f, 1f));
        }
    }

    // Godot: func add_link_arrow_between(left_pos, right_pos)
    private void CreateArrowBetween(float leftX, float rightX)
    {
        float midX = (leftX + rightX) / 2f * arScale;

        GameObject arrowObj = new GameObject("ListArrow");
        arrowObj.transform.SetParent(linksParent, false);
        arrowObj.transform.localPosition = new Vector3(midX, arHeight + linkVerticalOffset * arScale, arDistance - markerForwardOffset);
        arrowObj.transform.localScale = Vector3.one * markerBaseScale * arScale * cellSizeMultiplier;
        arrowObj.transform.localRotation = Quaternion.identity;

        TextMeshPro tmp = arrowObj.AddComponent<TextMeshPro>();
        tmp.text = "→"; // →
        tmp.fontSize = GetArrowFontSize();
        tmp.color = arrowColor;
        tmp.fontStyle = FontStyles.Bold;
        tmp.outlineWidth = 0.25f;
        tmp.outlineColor = Color.black;
        tmp.alignment = TextAlignmentOptions.Center;

        arrowObj.AddComponent<Billboard>();
    }

    // Godot: arrow font_size/pixel_size tiers inside add_link_arrow_between().
    private int GetArrowFontSize()
    {
        if (values.Count >= 6) return 38;
        if (values.Count == 5) return 40;
        return 44;
    }

    // Godot: func add_null_marker(null_position)
    private void AddNullMarker(float preScaleX)
    {
        GameObject markerObj = new GameObject("ListMarker_NULL");
        markerObj.transform.SetParent(markersParent, false);
        markerObj.transform.localPosition = new Vector3(preScaleX * arScale, arHeight + linkVerticalOffset * arScale, arDistance - markerForwardOffset);
        markerObj.transform.localScale = Vector3.one * markerBaseScale * arScale * cellSizeMultiplier;
        markerObj.transform.localRotation = Quaternion.identity;

        TextMeshPro tmp = markerObj.AddComponent<TextMeshPro>();
        tmp.text = "NULL";
        tmp.fontSize = values.Count >= 6 ? 24 : 28;
        tmp.color = nullMarkerColor;
        tmp.fontStyle = FontStyles.Bold;
        tmp.outlineWidth = 0.25f;
        tmp.outlineColor = Color.black;
        tmp.alignment = TextAlignmentOptions.Center;

        markerObj.AddComponent<Billboard>();
    }

    // Godot: func add_marker(text_value, marker_position, marker_color)
    private void AddHeadTailMarker(string text, float preScaleX, Color color)
    {
        float scaleFactor = GetListScaleFactor();
        float boxHalfHeight = 0.25f * scaleFactor * arScale * cellSizeMultiplier;
        float markerY = arHeight + boxHalfHeight + markerVerticalGap * arScale;

        GameObject markerObj = new GameObject("ListMarker_" + text.Replace("/", "_"));
        markerObj.transform.SetParent(markersParent, false);
        markerObj.transform.localPosition = new Vector3(preScaleX * arScale, markerY, arDistance - markerForwardOffset);
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

        markerObj.AddComponent<Billboard>();
    }

    // Godot: func insert_value(new_value: int) -> bool
    // No entrance animation — see class comment note 2.
    public bool InsertValue(int newValue)
    {
        if (isAnimating)
        {
            lastActionMessage = "Please wait. An animation is still running.";
            return false;
        }

        if (IsLinkedListFull())
        {
            lastActionMessage =
                "Insert blocked. The linked list is full. Maximum allowed nodes: " +
                MaxLinkedListValues + ". Delete the head before inserting another node.";
            DrawLinkedList();
            return false;
        }

        values.Add(newValue);

        lastActionMessage = "Value " + newValue + " was inserted at the tail of the linked list.";

        DrawLinkedList();

        return true;
    }

    // Godot: func delete_head()
    // DOES animate — head cell exits left while shrinking (see class
    // comment note 3).
    public IEnumerator DeleteHead(Action<int?> onComplete)
    {
        if (isAnimating || values.Count == 0)
        {
            onComplete?.Invoke(null);
            yield break;
        }

        int removedValue = values[0];

        if (currentNodes.Count == 0)
        {
            // Edge case matching the source: no cell reference to animate,
            // just remove and redraw.
            values.RemoveAt(0);
            DrawLinkedList();
            onComplete?.Invoke(removedValue);
            yield break;
        }

        isAnimating = true;

        GameObject headNode = currentNodes[0];
        yield return StartCoroutine(AnimateHeadExit(headNode));

        values.RemoveAt(0);

        isAnimating = false;

        DrawLinkedList();

        onComplete?.Invoke(removedValue);
    }

    // Godot: delete_head()'s parallel position+scale tween (exit left +
    // shrink to zero, both TRANS_BACK/EASE_IN, both 0.35s). Same "one loop,
    // one eased t drives both properties" technique as
    // QueueModule.AnimateDequeuedCell — see class comment note 3 for the
    // distinct exit distance.
    private IEnumerator AnimateHeadExit(GameObject cell)
    {
        if (cell == null) yield break;

        Vector3 originalScale = cell.transform.localScale;
        Vector3 originalPosition = cell.transform.localPosition;
        Vector3 targetPosition = originalPosition + new Vector3(-deleteExitDistance * arScale, 0, 0);

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

    // Godot: func search_value(search_value: int) -> int
    // Iterates head (index 0) to tail (last index) — already the natural
    // forward order of `values`/`currentNodes`, so no reversal or extra
    // index math is needed. See class comment note 7.
    public IEnumerator SearchValue(int searchValue, Action<int> onComplete)
    {
        if (currentNodes.Count == 0)
        {
            onComplete?.Invoke(-1);
            yield break;
        }

        int foundIndex = -1;

        for (int i = 0; i < currentNodes.Count; i++)
        {
            GameObject cell = currentNodes[i];
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

    // Godot: func traverse_linked_list()
    // The traverse function actually invoked by ar_learning_screen.gd (see
    // class comment note 4 — NOT traverse_list(), which is dead code).
    // Restores each node's color/scale before moving to the next, matching
    // the source's own "# Restore before moving to the next node." step.
    public IEnumerator TraverseLinkedList(Action onComplete = null)
    {
        if (currentNodes.Count == 0)
        {
            onComplete?.Invoke();
            yield break;
        }

        for (int i = 0; i < currentNodes.Count; i++)
        {
            GameObject cell = currentNodes[i];
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

    // Godot: func reset_list()
    public void ResetList()
    {
        if (isAnimating) return;

        values = new List<int> { 10, 20, 30 };
        DrawLinkedList();
    }

    // Godot: func is_linked_list_full() -> bool
    public bool IsLinkedListFull()
    {
        return values.Count >= MaxLinkedListValues;
    }

    // Godot: func get_last_action_message() -> String
    public string GetLastActionMessage()
    {
        return lastActionMessage;
    }

    // Godot: func get_status_text() -> String
    // Used only after Insert — see LinkedListController's porting notes for
    // the confirmed dual-status-format quirk (Delete Head and Reset use a
    // different, ar_learning_screen.gd-local format instead).
    public string GetStatusText()
    {
        if (values.Count == 0) return "Empty list.";

        return "Head = " + values[0] + " | Tail = " + values[values.Count - 1] + " | Size = " + values.Count;
    }

    // ----- Color helpers (same pattern as ArrayModule/StackModule/QueueModule) -----

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

    // ----- Tweening helpers (same pattern as ArrayModule/StackModule/QueueModule) -----

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
