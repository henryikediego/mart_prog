using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

// Direct port of array_module.gd.
// Godot's `await tween.finished` becomes Unity coroutines (IEnumerator +
// yield return). Since coroutines can't directly "return" a value the
// way Godot's async functions can, methods that need to hand back a
// result (insert success, delete value, search index) take a callback
// (System.Action) instead, invoked once the coroutine finishes.
public class ArrayModule : MonoBehaviour
{
    [Header("Assign in the Inspector")]
    [SerializeField] private GameObject cellPrefab;   // was: @export var cell_scene
    [SerializeField] private Transform cellsParent;   // was: $Cells
    [SerializeField] private Transform indexMarkersParent; // was: $IndexMarkers

    [Header("AR placement tuning (Godot units are too big for real-world AR)")]
    [Tooltip("Master multiplier shrinking the whole Godot-scale layout into real-world metres. Lower = smaller/closer together.")]
    [SerializeField] private float arScale = 0.12f;
    [Tooltip("How far in front of the AR origin (metres) the array sits.")]
    [SerializeField] private float arDistance = 0.5f;
    [Tooltip("Height (metres) of the array relative to the AR origin. Small, since it should sit in front of you, not overhead.")]
    [SerializeField] private float arHeight = 0f;
    [Tooltip("Grows/shrinks each box (and its child text, since text is a child of the box) without changing spacing between boxes.")]
    [SerializeField] private float cellSizeMultiplier = 1.4f;
    [Tooltip("Tightens or widens the horizontal gap between boxes without changing their size. Lower = closer together, so more cells fit on a portrait screen after inserts.")]
    [SerializeField] private float spacingMultiplier = 0.6f;

    [Header("Index marker appearance")]
    [Tooltip("Gap in metres between the bottom of a box and its index marker below it.")]
    [SerializeField] private float indexMarkerGap = 0.015f;
    [Tooltip("Base size of the index marker text — tune this against ValueLabel's own scale on the prefab so neither dwarfs the other.")]
    [SerializeField] private float indexMarkerBaseScale = 0.06f;
    [SerializeField] private int indexMarkerFontSize = 36;
    [Tooltip("Pulls markers this many metres toward the camera (in front of the boxes) so the box body doesn't overlap/hide the top of each digit.")]
    [SerializeField] private float indexMarkerForwardOffset = 0.08f;

    private const int MaxArrayValues = 10;

    public List<int> values = new List<int> { 10, 20, 30 };
    public bool isAnimating = false;
    private List<GameObject> currentCells = new List<GameObject>();
    public bool isTraversing = false;
    private string lastActionMessage = "";
    private int lastInsertedIndex = -1;

    private readonly Dictionary<Renderer, Color> originalColors = new Dictionary<Renderer, Color>();

    private void Start()
    {
        Debug.Log("ArrayModule Start(). cellPrefab is " +
            (cellPrefab == null ? "NULL" : "assigned") +
            ", cellsParent is " + (cellsParent == null ? "NULL" : "assigned") +
            ", indexMarkersParent is " + (indexMarkersParent == null ? "NULL" : "assigned"));

        DrawArray();

        Debug.Log("ArrayModule after DrawArray(): currentCells.Count = " + currentCells.Count);
    }

    // Godot: func draw_array(force_redraw := false)
    public void DrawArray(bool forceRedraw = false)
    {
        if (isAnimating && !forceRedraw)
        {
            return;
        }

        currentCells.Clear();
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
            Debug.LogError("ArrayModule: cellPrefab or cellsParent is not assigned; " +
                           "nothing can be drawn. Assign both in the Inspector.");
            return;
        }

        ClearContainer(cellsParent);
        ClearContainer(indexMarkersParent);

        float scaleFactor = GetArrayScaleFactor();
        float spacing = GetArraySpacing() * spacingMultiplier;

        float totalWidth = values.Count > 1 ? (values.Count - 1) * spacing : 0f;

        // Godot placed the array at a fixed world height; in AR we
        // scale the whole layout down (arScale) and sit it a short
        // distance in front of the origin instead of metres overhead.
        float startX = -totalWidth / 2f;

        // Box's own local scale (set on the prefab's Box child) is
        // 0.5, so its half-height before our own scaling is 0.25.
        float boxHalfHeight = 0.25f * scaleFactor * arScale * cellSizeMultiplier;
        float markerY = arHeight - (boxHalfHeight + indexMarkerGap);

        for (int i = 0; i < values.Count; i++)
        {
            GameObject cell = Instantiate(cellPrefab, cellsParent);
            float cellX = startX + i * spacing;

            cell.name = "ArrayCell_" + i;
            cell.transform.localPosition = new Vector3(
                cellX * arScale,
                arHeight,
                arDistance
            );
            cell.transform.localScale = Vector3.one * scaleFactor * arScale * cellSizeMultiplier;
            cell.transform.localRotation = Quaternion.identity;

            // Fix (Phase 18 on-device finding): guarantees every cell is
            // camera-facing regardless of whether the prefab itself
            // already carries a Billboard component -- see Billboard.cs
            // for why the previous yaw-only version still showed a
            // visible skew from steep viewing angles.
            if (cell.GetComponent<Billboard>() == null)
            {
                cell.AddComponent<Billboard>();
            }

            ArrayCell cellScript = cell.GetComponent<ArrayCell>();
            if (cellScript != null)
            {
                // Matches the original: value shown, index label blanked
                // (separate floating markers are used instead).
                cellScript.SetValueOnly(values[i].ToString());
            }

            currentCells.Add(cell);

            CreateIndexMarker(
                i.ToString(),
                new Vector3(
                    cellX * arScale,
                    markerY,
                    arDistance - indexMarkerForwardOffset
                )
            );
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

    // Godot: func get_array_scale_factor() -> float
    private float GetArrayScaleFactor()
    {
        if (values.Count <= 3) return 0.78f;
        if (values.Count == 4) return 0.70f;
        if (values.Count == 5) return 0.65f;
        return 0.60f;
    }

    // Godot: func get_array_spacing(scale_factor: float) -> float
    private float GetArraySpacing()
    {
        if (values.Count <= 3) return 2.15f;
        if (values.Count == 4) return 1.90f;
        if (values.Count == 5) return 1.68f;
        return 1.50f;
    }

    // Godot: func add_array_index_marker(...)
    private void CreateIndexMarker(string text, Vector3 position)
    {
        GameObject markerObj = new GameObject("IndexMarker_" + text);
        markerObj.transform.SetParent(indexMarkersParent, false);
        markerObj.transform.localPosition = position;
        // Starting scale — same fix we found for ValueLabel. Adjust
        // visually if markers look too big/small next to the cells.
        markerObj.transform.localScale = Vector3.one * indexMarkerBaseScale * arScale * cellSizeMultiplier;
        markerObj.transform.localRotation = Quaternion.identity;

        TextMeshPro tmp = markerObj.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = indexMarkerFontSize;
        tmp.color = new Color(0.4f, 0.85f, 1.0f, 1f);
        tmp.fontStyle = FontStyles.Bold;
        tmp.outlineWidth = 0.25f;
        tmp.outlineColor = Color.black;
        tmp.alignment = TextAlignmentOptions.Center;

        // Fix (Phase 18 on-device finding): index markers are built
        // fresh in code on every redraw and were never given a
        // Billboard component at all, so they've always sat at a fixed
        // world rotation. This was masked during Phase 15 validation
        // because the marker sits directly below its cell and reads
        // fine from a roughly head-on angle -- it's only obviously wrong
        // once viewed from a steep angle (e.g. looking down at a table).
        markerObj.AddComponent<Billboard>();
    }

    // Godot: func add_array_end_marker(...) — ported for parity,
    // not currently called anywhere (same as the original file).
    private void CreateEndMarker(string text, Vector3 position, Color color)
    {
        GameObject markerObj = new GameObject("EndMarker_" + text);
        markerObj.transform.SetParent(indexMarkersParent, false);
        markerObj.transform.localPosition = position;
        markerObj.transform.localScale = Vector3.one * 0.032f;
        markerObj.transform.localRotation = Quaternion.identity;

        TextMeshPro tmp = markerObj.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = 30;
        tmp.color = color;
        tmp.outlineWidth = 0.2f;
        tmp.alignment = TextAlignmentOptions.Center;

        // Same Billboard fix as CreateIndexMarker() above, kept
        // consistent even though this helper is currently unused.
        markerObj.AddComponent<Billboard>();
    }

    // Godot: func insert_value(new_value: int) -> bool
    public IEnumerator InsertValue(int newValue, Action<bool> onComplete)
    {
        if (isAnimating)
        {
            lastActionMessage = "Please wait. An animation is still running.";
            onComplete?.Invoke(false);
            yield break;
        }

        if (IsArrayFull())
        {
            lastActionMessage =
                "Insert blocked. The array/list is full. Maximum allowed values: " +
                MaxArrayValues + ". Delete a value before inserting another one.";
            DrawArray(true);
            onComplete?.Invoke(false);
            yield break;
        }

        isAnimating = true;

        values.Add(newValue);
        lastInsertedIndex = values.Count - 1;

        lastActionMessage =
            "Value " + newValue + " was inserted at index " + lastInsertedIndex + ".";

        DrawArray(true);

        yield return StartCoroutine(AnimateInsertedCell(lastInsertedIndex));

        lastInsertedIndex = -1;
        isAnimating = false;

        onComplete?.Invoke(true);
    }

    // Godot: func delete_value()
    public IEnumerator DeleteValue(Action<int?> onComplete)
    {
        if (isTraversing)
        {
            onComplete?.Invoke(null);
            yield break;
        }

        if (values.Count == 0)
        {
            onComplete?.Invoke(null);
            yield break;
        }

        int removedValue = values[values.Count - 1];

        if (currentCells.Count > 0)
        {
            GameObject last = currentCells[currentCells.Count - 1];
            yield return StartCoroutine(ScaleToLinear(last.transform, Vector3.zero, 0.3f));
        }

        values.RemoveAt(values.Count - 1);

        DrawArray();

        onComplete?.Invoke(removedValue);
    }

    // Godot: func reset_array()
    public void ResetArray()
    {
        if (isTraversing) return;

        values = new List<int> { 10, 20, 30 };
        DrawArray();
    }

    // Godot: func search_value(target_value: int) -> int
    public IEnumerator SearchValue(int targetValue, Action<int> onComplete)
    {
        if (isAnimating)
        {
            onComplete?.Invoke(-1);
            yield break;
        }

        if (values.Count == 0)
        {
            onComplete?.Invoke(-1);
            yield break;
        }

        DrawArray(true);

        if (currentCells.Count == 0)
        {
            onComplete?.Invoke(-1);
            yield break;
        }

        isAnimating = true;
        int foundIndex = -1;

        for (int i = 0; i < values.Count; i++)
        {
            if (i >= currentCells.Count) continue;

            GameObject cell = currentCells[i];
            Vector3 originalScale = cell.transform.localScale;

            SetCellColor(cell, new Color(1.0f, 0.75f, 0.15f, 1.0f));

            yield return StartCoroutine(ScaleTo(cell.transform, originalScale * 1.20f, 0.18f, true));
            yield return new WaitForSeconds(0.12f);
            yield return StartCoroutine(ScaleTo(cell.transform, originalScale, 0.18f, false));

            if (values[i] == targetValue)
            {
                foundIndex = i;
                SetCellColor(cell, new Color(0.1f, 0.85f, 0.25f, 1.0f));
                yield return new WaitForSeconds(0.45f);
                break;
            }

            RestoreCellColor(cell);
        }

        isAnimating = false;

        if (foundIndex == -1)
        {
            DrawArray(true);
        }

        onComplete?.Invoke(foundIndex);
    }

    // Godot: func traverse_array()
    public IEnumerator TraverseArray(Action onComplete = null)
    {
        if (isAnimating)
        {
            onComplete?.Invoke();
            yield break;
        }

        if (values.Count == 0)
        {
            onComplete?.Invoke();
            yield break;
        }

        DrawArray(true);

        if (currentCells.Count == 0)
        {
            onComplete?.Invoke();
            yield break;
        }

        isAnimating = true;

        for (int i = 0; i < values.Count; i++)
        {
            if (i >= currentCells.Count) continue;

            GameObject cell = currentCells[i];
            Vector3 originalScale = cell.transform.localScale;

            SetCellColor(cell, new Color(1.0f, 0.75f, 0.15f, 1.0f));

            yield return StartCoroutine(ScaleTo(cell.transform, originalScale * 1.20f, 0.18f, true));
            yield return new WaitForSeconds(0.12f);
            yield return StartCoroutine(ScaleTo(cell.transform, originalScale, 0.18f, false));

            RestoreCellColor(cell);
        }

        isAnimating = false;

        DrawArray(true);
        onComplete?.Invoke();
    }

    // Godot: func is_array_full() -> bool
    public bool IsArrayFull()
    {
        return values.Count >= MaxArrayValues;
    }

    // Godot: func get_last_action_message() -> String
    public string GetLastActionMessage()
    {
        return lastActionMessage;
    }

    // Godot: func animate_inserted_cell(inserted_index: int)
    private IEnumerator AnimateInsertedCell(int insertedIndex)
    {
        if (insertedIndex < 0 || insertedIndex >= currentCells.Count) yield break;

        GameObject newCell = currentCells[insertedIndex];
        if (newCell == null) yield break;

        Vector3 originalScale = newCell.transform.localScale;
        Vector3 originalPosition = newCell.transform.localPosition;

        newCell.transform.localScale = originalScale * 0.15f;
        newCell.transform.localPosition = originalPosition + new Vector3(0, 0.45f * arScale, 0);

        Vector3 startScale = newCell.transform.localScale;
        Vector3 startPosition = newCell.transform.localPosition;

        float duration = 0.35f;
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duration);

            newCell.transform.localScale = Vector3.LerpUnclamped(startScale, originalScale, EaseOutBack(p));
            newCell.transform.localPosition = Vector3.Lerp(startPosition, originalPosition, EaseOutSine(p));

            yield return null;
        }

        newCell.transform.localScale = originalScale;
        newCell.transform.localPosition = originalPosition;
    }

    // ----- Color helpers (replace Godot's material_override + set_meta/get_meta) -----

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

    // ----- Tweening helpers (replace Godot's Tween system) -----

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

    private IEnumerator ScaleToLinear(Transform target, Vector3 endScale, float duration)
    {
        Vector3 startScale = target.localScale;
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            target.localScale = Vector3.Lerp(startScale, endScale, t / duration);
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
        isTraversing = false;
    }
}
