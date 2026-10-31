using System.Collections;
using Loci.AR;
using Loci.Bridge;
using TMPro;
using UnityEngine;

namespace Loci.Topics.Queue
{
    /// <summary>Renders FIFO Queue cells and handles shell actions. All 2D UI stays in React.</summary>
    public sealed class QueueTopicController : MonoBehaviour
    {
        private static readonly Color32 CellColor = new Color32(91, 75, 255, 255);
        private static readonly Color32 FoundColor = new Color32(245, 207, 80, 255);
        private static readonly Color32 FrontColor = new Color32(102, 204, 255, 255);
        private static readonly Color32 RearColor = new Color32(255, 217, 26, 255);
        [Header("3D display")]
        [SerializeField, Min(0.04f)] private float cellSize = 0.12f;
        [SerializeField, Min(0.08f)] private float cellSpacing = 0.14f;
        [SerializeField, Min(0.001f)] private float labelScale = 0.006f;
        [SerializeField, Min(0.01f)] private float spawnDuration = 0.18f;
        [SerializeField, Min(0.1f)] private float actionStepDuration = 0.45f;
        private QueueState state;
        private Transform topicRoot;
        private bool isOpen;
        private bool isSequenceRunning;
        private int highlightedIndex = -1;
        private Material cellMaterial;
        private Material foundMaterial;

        private void OnEnable() => SpikeBridge.ShellCommandReceived += OnShellCommand;
        private void OnDisable()
        {
            SpikeBridge.ShellCommandReceived -= OnShellCommand;
            CloseTopic();
            DestroyMaterials();
        }
        private void OnShellCommand(LociMessageEnvelope message, ILociMessagePayload payload)
        {
            switch (message.Type)
            {
                case LociMessageTypes.TopicOpen: Open(payload as TopicOpenPayload); break;
                case LociMessageTypes.TopicAction: Act(payload as TopicActionPayload); break;
                case LociMessageTypes.TopicClose:
                case LociMessageTypes.PlacementReset:
                case LociMessageTypes.SessionStop: CloseTopic(); break;
            }
        }
        private void Open(TopicOpenPayload payload)
        {
            if (payload == null) return;
            if (payload.TopicId != LociTopicId.Queue) { CloseTopic(); return; }
            LociARController ar = LociARController.Instance;
            if (ar == null || !ar.IsPlaced || ar.ContentRoot == null) return;
            CloseTopic(); state = new QueueState(ScreenNodeCapacity.ForCurrentScreen()); state.Reset(); highlightedIndex = -1;
            topicRoot = new GameObject("Queue 3D Content").transform;
            topicRoot.SetParent(ar.ContentRoot, false);
            isOpen = true; RenderQueue(true);
            SpikeBridge.SendMessageToShell(LociMessageTypes.TopicLoaded,
                new TopicLoadedPayload { TopicId = LociTopicId.Queue });
        }
        private void Act(TopicActionPayload payload)
        {
            if (!isOpen || payload == null || payload.TopicId != LociTopicId.Queue) return;
            if (isSequenceRunning) return;
            if (payload.Action == "Search")
            {
                StartCoroutine(AnimateSearch(payload.Value));
                return;
            }
            if (payload.Action == "Traverse")
            {
                StartCoroutine(AnimateTraverse());
                return;
            }
            QueueActionResult result = PerformAction(payload.Action, payload.Value);
            if (result == null) return;
            RenderQueue(true);
            SendTopicEvent(result);
        }

        private IEnumerator AnimateSearch(string input)
        {
            string value = input == null ? string.Empty : input.Trim();
            if (!int.TryParse(value, out int target))
            {
                SendTopicEvent(Result("Search", value, "Failed", "Enter a valid number to search for."));
                yield break;
            }
            if (state.IsEmpty)
            {
                SendTopicEvent(Result("Search", value, "Failed", "Queue is empty."));
                yield break;
            }

            isSequenceRunning = true;
            for (int i = 0; i < state.Count; i++)
            {
                highlightedIndex = i;
                RenderQueue(false);
                yield return new WaitForSeconds(actionStepDuration);
                if (state.Values[i] == target)
                {
                    // Keep the matching cell highlighted after the visual search completes.
                    SendTopicEvent(Result("Search", value, "Success", $"Found {target} at index {i} from the front."));
                    isSequenceRunning = false;
                    yield break;
                }
            }

            highlightedIndex = -1;
            RenderQueue(false);
            SendTopicEvent(Result("Search", value, "Failed", $"{target} was not found in the Queue."));
            isSequenceRunning = false;
        }

        private IEnumerator AnimateTraverse()
        {
            if (state.IsEmpty)
            {
                SendTopicEvent(Result("Traverse", string.Empty, "Failed", "Queue is empty."));
                yield break;
            }

            isSequenceRunning = true;
            for (int i = 0; i < state.Count; i++)
            {
                highlightedIndex = i;
                RenderQueue(false);
                yield return new WaitForSeconds(actionStepDuration);
            }

            string order = state.GetFrontToRearOrder();
            highlightedIndex = -1;
            RenderQueue(false);
            SendTopicEvent(Result("Traverse", order, "Success", "Traversed Queue values from front to rear."));
            isSequenceRunning = false;
        }

        private void SendTopicEvent(QueueActionResult result)
        {
            SpikeBridge.SendMessageToShell(LociMessageTypes.TopicEvent, new TopicEventPayload
            {
                TopicId = LociTopicId.Queue, Action = result.Action, Value = result.Value,
                Result = result.Result, Details = result.Details
            });
        }
        private QueueActionResult PerformAction(string action, string input)
        {
            string value = input == null ? string.Empty : input.Trim();
            highlightedIndex = -1;
            switch (action)
            {
                case "Enqueue":
                    if (!int.TryParse(value, out int added)) return Result(action, value, "Failed", "Enter a valid number to enqueue.");
                    if (!state.TryEnqueue(added)) return Result(action, value, "Failed", $"This screen's readable limit of {state.Capacity} Queue values has been reached.");
                    return Result(action, added.ToString(), "Success", $"Enqueued {added} at the rear of the Queue.");
                case "Dequeue":
                    if (!state.TryDequeue(out int removed)) return Result(action, string.Empty, "Failed", "Queue is empty.");
                    return Result(action, removed.ToString(), "Success", $"Dequeued {removed} from the front of the Queue.");
                case "Peek":
                    if (!state.TryPeek(out int front)) return Result(action, string.Empty, "Failed", "Queue is empty.");
                    highlightedIndex = 0;
                    return Result(action, front.ToString(), "Success", $"The front value is {front}; Peek does not remove it.");
                case "Search":
                    if (!int.TryParse(value, out int target)) return Result(action, value, "Failed", "Enter a valid number to search for.");
                    highlightedIndex = state.FindIndex(target);
                    return highlightedIndex >= 0 ? Result(action, value, "Success", $"Found {target} at index {highlightedIndex} from the front.") : Result(action, value, "Failed", $"{target} was not found in the Queue.");
                case "Traverse":
                    if (state.IsEmpty) return Result(action, string.Empty, "Failed", "Queue is empty.");
                    return Result(action, state.GetFrontToRearOrder(), "Success", "Traversed Queue values from front to rear.");
                case "Reset":
                    state.Reset();
                    return Result(action, string.Empty, "Success", "Queue reset to its default values.");
                default: Debug.LogWarning("QueueTopicController ignored unsupported action: " + action); return null;
            }
        }
        private static QueueActionResult Result(string action, string value, string result, string details) => new QueueActionResult(action, value, result, details);

        private void RenderQueue(bool animate)
        {
            if (topicRoot == null) return;
            for (int i = topicRoot.childCount - 1; i >= 0; i--) Destroy(topicRoot.GetChild(i).gameObject);
            EnsureMaterials();
            float layoutScale = state.Count <= 3 ? 1f : 3f / state.Count;
            topicRoot.localScale = Vector3.one * layoutScale;
            float spacing = cellSpacing;
            float startX = -(state.Count - 1) * spacing * 0.5f;
            for (int i = 0; i < state.Count; i++)
            {
                float x = startX + i * spacing;
                GameObject cell = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cell.name = "Queue Cell " + i;
                cell.transform.SetParent(topicRoot, false);
                cell.transform.localPosition = new Vector3(x, 0f, 0f);
                cell.transform.localScale = new Vector3(cellSize, cellSize, cellSize * 0.22f);
                Collider collider = cell.GetComponent<Collider>(); if (collider != null) Destroy(collider);
                cell.GetComponent<MeshRenderer>().sharedMaterial = i == highlightedIndex ? foundMaterial : cellMaterial;
                CreateLabel(state.Values[i].ToString(), new Vector3(x, 0f, -cellSize * 0.12f), 64f, Color.white);
                if (animate) { Vector3 finalScale = cell.transform.localScale; cell.transform.localScale = Vector3.zero; StartCoroutine(AnimateIn(cell.transform, finalScale)); }
            }
            float markerY = cellSize * 0.5f + 0.09f;
            float frontX = state.Count == 0 ? -cellSpacing : startX;
            float rearX = state.Count == 0 ? cellSpacing : startX + (state.Count - 1) * spacing;
            CreateLabel("FRONT", new Vector3(frontX, markerY, -cellSize * 0.2f), 38f, FrontColor);
            CreateLabel("REAR", new Vector3(rearX, markerY, -cellSize * 0.2f), 38f, RearColor);
        }
        private void CreateLabel(string text, Vector3 position, float fontSize, Color color)
        {
            GameObject go = new GameObject("Queue Label"); go.transform.SetParent(topicRoot, false);
            go.transform.localPosition = position; go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            go.transform.localScale = new Vector3(-labelScale, labelScale, labelScale);
            TextMeshPro label = go.AddComponent<TextMeshPro>(); label.text = text; label.fontSize = fontSize;
            label.color = color; label.alignment = TextAlignmentOptions.Center; label.enableAutoSizing = true;
            label.fontSizeMin = fontSize * 0.75f; label.fontSizeMax = fontSize;
            label.outlineWidth = 0.18f;
            label.outlineColor = new Color32(20, 24, 45, 255);
            label.rectTransform.sizeDelta = new Vector2(100f, 45f);
            if (TMP_Settings.defaultFontAsset != null) label.font = TMP_Settings.defaultFontAsset;
        }
        private IEnumerator AnimateIn(Transform target, Vector3 finalScale)
        {
            if (target == null) yield break;
            float elapsed = 0f;
            while (elapsed < spawnDuration && target != null)
            {
                elapsed += Time.deltaTime; float t = Mathf.Clamp01(elapsed / spawnDuration);
                target.localScale = Vector3.LerpUnclamped(Vector3.zero, finalScale, 1f - Mathf.Pow(1f - t, 3f)); yield return null;
            }
            if (target != null) target.localScale = finalScale;
        }
        private void EnsureMaterials()
        {
            if (cellMaterial != null && foundMaterial != null) return;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            cellMaterial = new Material(shader) { color = CellColor }; foundMaterial = new Material(shader) { color = FoundColor };
        }
        private void CloseTopic()
        {
            StopAllCoroutines();
            isSequenceRunning = false;
            isOpen = false; highlightedIndex = -1;
            if (topicRoot != null) { Destroy(topicRoot.gameObject); topicRoot = null; }
        }
        private void DestroyMaterials()
        {
            if (cellMaterial != null) Destroy(cellMaterial); if (foundMaterial != null) Destroy(foundMaterial);
            cellMaterial = null; foundMaterial = null;
        }
        private sealed class QueueActionResult
        {
            public string Action { get; } public string Value { get; } public string Result { get; } public string Details { get; }
            public QueueActionResult(string action, string value, string result, string details) { Action = action; Value = value; Result = result; Details = details; }
        }
    }
}
