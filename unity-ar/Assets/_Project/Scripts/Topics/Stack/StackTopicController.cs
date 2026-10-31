using System.Collections;
using Loci.AR;
using Loci.Bridge;
using TMPro;
using UnityEngine;

namespace Loci.Topics.Stack
{
    /// <summary>Renders Stack cells in 3D and handles shell actions; 2D UI stays in React.</summary>
    public sealed class StackTopicController : MonoBehaviour
    {
        private static readonly Color32 CellColor = new Color32(91, 75, 255, 255);
        private static readonly Color32 FoundColor = new Color32(245, 207, 80, 255);
        private static readonly Color32 LabelColor = new Color32(255, 255, 255, 255);

        [Header("3D display")]
        [SerializeField, Min(0.04f)] private float cellSize = 0.12f;
        [SerializeField, Min(0.08f)] private float cellSpacing = 0.14f;
        [SerializeField, Min(0.001f)] private float labelScale = 0.006f;
        [SerializeField, Min(0.01f)] private float spawnDuration = 0.18f;
        [SerializeField, Min(0.1f)] private float actionStepDuration = 0.45f;

        private StackState state;
        private Transform topicRoot;
        private bool isOpen;
        private bool isSequenceRunning;
        private int highlightedDepth = -1;
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
                case LociMessageTypes.TopicOpen: HandleTopicOpen(payload as TopicOpenPayload); break;
                case LociMessageTypes.TopicAction: HandleTopicAction(payload as TopicActionPayload); break;
                case LociMessageTypes.TopicClose:
                case LociMessageTypes.PlacementReset:
                case LociMessageTypes.SessionStop: CloseTopic(); break;
            }
        }

        private void HandleTopicOpen(TopicOpenPayload payload)
        {
            if (payload == null) return;
            if (payload.TopicId != LociTopicId.Stack) { CloseTopic(); return; }
            LociARController ar = LociARController.Instance;
            if (ar == null || !ar.IsPlaced || ar.ContentRoot == null) return;

            CloseTopic();
            state = new StackState(ScreenNodeCapacity.ForCurrentScreen());
            state.Reset();
            highlightedDepth = -1;
            GameObject root = new GameObject("Stack 3D Content");
            topicRoot = root.transform;
            topicRoot.SetParent(ar.ContentRoot, false);
            isOpen = true;
            RenderStack(true);
            SpikeBridge.SendMessageToShell(LociMessageTypes.TopicLoaded,
                new TopicLoadedPayload { TopicId = LociTopicId.Stack });
        }

        private void HandleTopicAction(TopicActionPayload payload)
        {
            if (!isOpen || payload == null || payload.TopicId != LociTopicId.Stack) return;
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
            StackActionResult result = PerformAction(payload.Action, payload.Value);
            if (result == null) return;
            RenderStack(true);
            SpikeBridge.SendMessageToShell(LociMessageTypes.TopicEvent, new TopicEventPayload
            {
                TopicId = LociTopicId.Stack, Action = result.Action, Value = result.Value,
                Result = result.Result, Details = result.Details
            });
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
                SendTopicEvent(Result("Search", value, "Failed", "Stack is empty."));
                yield break;
            }

            isSequenceRunning = true;
            for (int depth = 0; depth < state.Count; depth++)
            {
                highlightedDepth = depth;
                RenderStack(false);
                yield return new WaitForSeconds(actionStepDuration);
                int valueIndex = state.Count - 1 - depth;
                if (state.Values[valueIndex] == target)
                {
                    SendTopicEvent(Result("Search", value, "Success", $"Found {target} at depth {depth} from the top."));
                    isSequenceRunning = false;
                    yield break;
                }
            }

            highlightedDepth = -1;
            RenderStack(false);
            SendTopicEvent(Result("Search", value, "Failed", $"{target} was not found in the Stack."));
            isSequenceRunning = false;
        }

        private IEnumerator AnimateTraverse()
        {
            if (state.IsEmpty)
            {
                SendTopicEvent(Result("Traverse", string.Empty, "Failed", "Stack is empty."));
                yield break;
            }

            isSequenceRunning = true;
            for (int depth = 0; depth < state.Count; depth++)
            {
                highlightedDepth = depth;
                RenderStack(false);
                yield return new WaitForSeconds(actionStepDuration);
            }

            string order = state.GetTopToBottomOrder();
            highlightedDepth = -1;
            RenderStack(false);
            SendTopicEvent(Result("Traverse", order, "Success", "Traversed Stack values from top to bottom."));
            isSequenceRunning = false;
        }

        private void SendTopicEvent(StackActionResult result)
        {
            SpikeBridge.SendMessageToShell(LociMessageTypes.TopicEvent, new TopicEventPayload
            {
                TopicId = LociTopicId.Stack,
                Action = result.Action,
                Value = result.Value,
                Result = result.Result,
                Details = result.Details
            });
        }

        private StackActionResult PerformAction(string action, string input)
        {
            string value = input == null ? string.Empty : input.Trim();
            highlightedDepth = -1;
            switch (action)
            {
                case "Push":
                    if (value.Length == 0) return Result(action, value, "Failed", "Enter a value to push onto the Stack.");
                    if (!int.TryParse(value, out int pushed)) return Result(action, value, "Failed", "The Stack value must be a number.");
                    if (!state.TryPush(pushed)) return Result(action, value, "Failed", $"This screen's readable limit of {state.Capacity} Stack values has been reached.");
                    return Result(action, pushed.ToString(), "Success", $"Pushed {pushed} onto the top of the Stack.");
                case "Pop":
                    if (!state.TryPop(out int popped)) return Result(action, string.Empty, "Failed", "Stack is empty.");
                    return Result(action, popped.ToString(), "Success", $"Popped {popped} from the top of the Stack.");
                case "Peek":
                    if (!state.TryPeek(out int top)) return Result(action, string.Empty, "Failed", "Stack is empty.");
                    highlightedDepth = 0;
                    return Result(action, top.ToString(), "Success", $"The top value is {top}.");
                case "Search":
                    if (!int.TryParse(value, out int target)) return Result(action, value, "Failed", "Enter a valid number to search for.");
                    highlightedDepth = state.FindIndexFromTop(target);
                    return highlightedDepth >= 0
                        ? Result(action, value, "Success", $"Found {target} at depth {highlightedDepth} from the top.")
                        : Result(action, value, "Failed", $"{target} was not found in the Stack.");
                case "Traverse":
                    if (state.IsEmpty) return Result(action, string.Empty, "Failed", "Stack is empty.");
                    return Result(action, state.GetTopToBottomOrder(), "Success", "Traversed Stack values from top to bottom.");
                case "Reset":
                    state.Reset();
                    return Result(action, string.Empty, "Success", "Stack reset to its default values.");
                default:
                    Debug.LogWarning("StackTopicController ignored unsupported action: " + action);
                    return null;
            }
        }

        private static StackActionResult Result(string action, string value, string result, string details) =>
            new StackActionResult(action, value, result, details);

        private void RenderStack(bool animate)
        {
            if (topicRoot == null) return;
            for (int i = topicRoot.childCount - 1; i >= 0; i--) Destroy(topicRoot.GetChild(i).gameObject);
            EnsureMaterials();
            float layoutScale = state.Count <= 3 ? 1f : 3f / state.Count;
            topicRoot.localScale = Vector3.one * layoutScale;
            float spacing = cellSpacing;
            float bottomY = -(state.Count - 1) * spacing * 0.5f;
            for (int i = 0; i < state.Count; i++)
            {
                float y = bottomY + i * spacing;
                int depth = state.Count - 1 - i;
                GameObject cell = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cell.name = "Stack Cell " + i;
                cell.transform.SetParent(topicRoot, false);
                cell.transform.localPosition = new Vector3(0f, y, 0f);
                cell.transform.localScale = new Vector3(cellSize, cellSize, cellSize * 0.22f);
                Collider collider = cell.GetComponent<Collider>();
                if (collider != null) Destroy(collider);
                cell.GetComponent<MeshRenderer>().sharedMaterial = depth == highlightedDepth ? foundMaterial : cellMaterial;
                CreateLabel(topicRoot, state.Values[i].ToString(), new Vector3(0f, y, -cellSize * 0.12f), 64f);
                if (animate)
                {
                    Vector3 finalScale = cell.transform.localScale;
                    cell.transform.localScale = Vector3.zero;
                    StartCoroutine(AnimateIn(cell.transform, finalScale));
                }
            }
            float markerX = cellSize * 0.95f;
            CreateLabel(topicRoot, "BOTTOM", new Vector3(markerX, bottomY, -cellSize * 0.2f), 24f);
            float topY = state.Count == 0 ? 0f : bottomY + (state.Count - 1) * spacing;
            CreateLabel(topicRoot, "TOP", new Vector3(markerX, topY, -cellSize * 0.2f), 24f);
        }

        private void CreateLabel(Transform parent, string text, Vector3 position, float fontSize)
        {
            GameObject go = new GameObject("Stack Label");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            go.transform.localScale = new Vector3(-labelScale, labelScale, labelScale);
            TextMeshPro label = go.AddComponent<TextMeshPro>();
            label.text = text;
            label.fontSize = fontSize;
            label.color = LabelColor;
            label.alignment = TextAlignmentOptions.Center;
            label.enableAutoSizing = true;
            label.fontSizeMin = fontSize * 0.75f;
            label.fontSizeMax = fontSize;
            label.rectTransform.sizeDelta = new Vector2(100f, 45f);
            if (TMP_Settings.defaultFontAsset != null) label.font = TMP_Settings.defaultFontAsset;
        }

        private IEnumerator AnimateIn(Transform target, Vector3 finalScale)
        {
            if (target == null) yield break;
            float elapsed = 0f;
            while (elapsed < spawnDuration && target != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / spawnDuration);
                target.localScale = Vector3.LerpUnclamped(Vector3.zero, finalScale, 1f - Mathf.Pow(1f - t, 3f));
                yield return null;
            }
            if (target != null) target.localScale = finalScale;
        }

        private void EnsureMaterials()
        {
            if (cellMaterial != null && foundMaterial != null) return;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            cellMaterial = new Material(shader) { color = CellColor };
            foundMaterial = new Material(shader) { color = FoundColor };
        }

        private void CloseTopic()
        {
            StopAllCoroutines();
            isOpen = false;
            isSequenceRunning = false;
            highlightedDepth = -1;
            if (topicRoot != null) { Destroy(topicRoot.gameObject); topicRoot = null; }
        }

        private void DestroyMaterials()
        {
            if (cellMaterial != null) Destroy(cellMaterial);
            if (foundMaterial != null) Destroy(foundMaterial);
            cellMaterial = null;
            foundMaterial = null;
        }

        private sealed class StackActionResult
        {
            public string Action { get; }
            public string Value { get; }
            public string Result { get; }
            public string Details { get; }
            public StackActionResult(string action, string value, string result, string details)
            { Action = action; Value = value; Result = result; Details = details; }
        }
    }
}
