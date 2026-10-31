using System.Collections;
using Loci.AR;
using Loci.Bridge;
using TMPro;
using UnityEngine;

namespace Loci.Topics.LinkedList
{
    /// <summary>Renders singly linked nodes in AR and handles topic actions; all 2D UI stays in React.</summary>
    public sealed class LinkedListTopicController : MonoBehaviour
    {
        private static readonly Color32 NodeColor = new Color32(91, 75, 255, 255);
        private static readonly Color32 HighlightColor = new Color32(245, 207, 80, 255);
        private static readonly Color32 HeadColor = new Color32(102, 204, 255, 255);
        private static readonly Color32 TailColor = new Color32(255, 217, 26, 255);

        [Header("3D display")]
        [SerializeField, Min(0.04f)] private float nodeSize = 0.12f;
        [SerializeField, Min(0.08f)] private float nodeSpacing = 0.14f;
        [SerializeField, Min(0.001f)] private float labelScale = 0.006f;
        [SerializeField, Min(0.1f)] private float actionStepDuration = 0.45f;
        [SerializeField, Min(0f)] private float markerVerticalGap = 0.09f;
        [SerializeField, Min(18f)] private float markerFontSize = 38f;
        [SerializeField, Min(0.01f)] private float deleteHeadDuration = 0.35f;
        [SerializeField, Min(0.05f)] private float deleteHeadExitDistance = 0.16f;

        private readonly LinkedListState state = new LinkedListState();
        private Transform topicRoot;
        private bool isOpen;
        private bool isSequenceRunning;
        private int highlightedIndex = -1;
        private float currentLayoutScale = 1f;
        private Material nodeMaterial;
        private Material highlightMaterial;

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
            if (payload.TopicId != LociTopicId.LinkedList) { CloseTopic(); return; }

            LociARController ar = LociARController.Instance;
            if (ar == null || !ar.IsPlaced || ar.ContentRoot == null) return;

            CloseTopic();
            state.Reset();
            topicRoot = new GameObject("Linked List 3D Content").transform;
            topicRoot.SetParent(ar.ContentRoot, false);
            isOpen = true;
            RenderNodes();

            SpikeBridge.SendMessageToShell(LociMessageTypes.TopicLoaded,
                new TopicLoadedPayload { TopicId = LociTopicId.LinkedList });
        }

        private void Act(TopicActionPayload payload)
        {
            if (!isOpen || payload == null || payload.TopicId != LociTopicId.LinkedList || isSequenceRunning) return;
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
            if (payload.Action == "Delete Head" || payload.Action == "Delete")
            {
                StartCoroutine(AnimateDeleteHead());
                return;
            }

            LinkedListActionResult result = PerformAction(payload.Action, payload.Value);
            if (result == null) return;
            RenderNodes();
            SendTopicEvent(result);
        }

        private LinkedListActionResult PerformAction(string action, string input)
        {
            string value = input == null ? string.Empty : input.Trim();
            highlightedIndex = -1;
            switch (action)
            {
                case "Insert":
                    if (!int.TryParse(value, out int inserted)) return Result(action, value, "Failed", "Enter a valid number to insert at the tail.");
                    if (!state.TryAppend(inserted)) return Result(action, value, "Failed", "Linked List reached its five-node capacity.");
                    return Result(action, inserted.ToString(), "Success", $"Inserted {inserted} as the new tail node.");
                case "Delete Head":
                case "Delete":
                    if (!state.TryRemoveHead(out int removed)) return Result("Delete Head", string.Empty, "Failed", "Linked List is empty.");
                    return Result("Delete Head", removed.ToString(), "Success", $"Deleted head node {removed}; the next node is now the head.");
                case "Reset":
                    state.Reset();
                    return Result(action, string.Empty, "Success", "Linked List reset to 10 → 20 → 30 → NULL.");
                default:
                    Debug.LogWarning("LinkedListTopicController ignored unsupported action: " + action);
                    return null;
            }
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
                SendTopicEvent(Result("Search", value, "Failed", "Value was not found in the empty Linked List."));
                yield break;
            }

            isSequenceRunning = true;
            int index = 0;
            for (LinkedListState.Node node = state.Head; node != null; node = node.Next, index++)
            {
                highlightedIndex = index;
                RenderNodes();
                yield return new WaitForSeconds(actionStepDuration);
                if (node.Value == target)
                {
                    SendTopicEvent(Result("Search", value, "Success", $"Found {target} at node index {index} from the head."));
                    isSequenceRunning = false;
                    yield break;
                }
            }

            highlightedIndex = -1;
            RenderNodes();
            SendTopicEvent(Result("Search", value, "Failed", $"{target} was not found in the Linked List."));
            isSequenceRunning = false;
        }

        private IEnumerator AnimateDeleteHead()
        {
            if (state.IsEmpty)
            {
                SendTopicEvent(Result("Delete Head", string.Empty, "Failed", "Linked List is empty."));
                yield break;
            }

            isSequenceRunning = true;
            Transform headVisual = topicRoot.Find($"Linked List Node 0 (value {state.Head.Value})");
            if (headVisual != null)
            {
                Vector3 startPosition = headVisual.localPosition;
                Vector3 startScale = headVisual.localScale;
                Vector3 endPosition = startPosition + Vector3.left * deleteHeadExitDistance;
                float elapsed = 0f;
                while (elapsed < deleteHeadDuration && headVisual != null)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / deleteHeadDuration);
                    float eased = t * t;
                    headVisual.localPosition = Vector3.LerpUnclamped(startPosition, endPosition, eased);
                    headVisual.localScale = Vector3.LerpUnclamped(startScale, Vector3.zero, eased);
                    yield return null;
                }
            }

            state.TryRemoveHead(out int removed);
            highlightedIndex = -1;
            RenderNodes();
            SendTopicEvent(Result("Delete Head", removed.ToString(), "Success", $"Deleted head node {removed}; the next node is now the head."));
            isSequenceRunning = false;
        }

        private IEnumerator AnimateTraverse()
        {
            if (state.IsEmpty)
            {
                SendTopicEvent(Result("Traverse", string.Empty, "Failed", "Linked List is empty."));
                yield break;
            }

            isSequenceRunning = true;
            for (int index = 0; index < state.Count; index++)
            {
                highlightedIndex = index;
                RenderNodes();
                yield return new WaitForSeconds(actionStepDuration);
            }

            string order = state.GetHeadToTailOrder();
            highlightedIndex = -1;
            RenderNodes();
            SendTopicEvent(Result("Traverse", order, "Success", "Traversed Linked List nodes from head to tail, ending at NULL."));
            isSequenceRunning = false;
        }

        private void RenderNodes()
        {
            if (topicRoot == null) return;
            for (int i = topicRoot.childCount - 1; i >= 0; i--) Destroy(topicRoot.GetChild(i).gameObject);
            EnsureMaterials();

            currentLayoutScale = state.Count <= 3
                ? 1f
                : Mathf.Lerp(1f, 0.6f, (state.Count - 3) / 2f);
            float spacing = nodeSpacing * currentLayoutScale;
            float scaledNodeSize = nodeSize * currentLayoutScale;
            float trailingLinkWidth = state.IsEmpty ? 0f : spacing * 0.62f;
            float startX = -((state.Count - 1) * spacing + trailingLinkWidth) * 0.5f;
            float localLabelZ = -nodeSize * 0.14f;
            float rootLabelZ = -scaledNodeSize * 0.14f;
            int index = 0;
            for (LinkedListState.Node node = state.Head; node != null; node = node.Next, index++)
            {
                float x = startX + index * spacing;
                GameObject nodeObject = new GameObject($"Linked List Node {index} (value {node.Value})");
                nodeObject.transform.SetParent(topicRoot, false);
                nodeObject.transform.localPosition = new Vector3(x, 0f, 0f);
                nodeObject.transform.localScale = Vector3.one * currentLayoutScale;

                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.transform.SetParent(nodeObject.transform, false);
                cube.transform.localScale = new Vector3(nodeSize, nodeSize, nodeSize * 0.22f);
                Collider collider = cube.GetComponent<Collider>();
                if (collider != null) Destroy(collider);
                cube.GetComponent<MeshRenderer>().sharedMaterial = index == highlightedIndex ? highlightMaterial : nodeMaterial;

                CreateLabel(nodeObject.transform, node.Value.ToString(), new Vector3(0f, 0f, localLabelZ), 60f, Color.white);
                if (index < state.Count - 1)
                    CreateLabel(topicRoot, "→", new Vector3(x + spacing * 0.5f, 0f, rootLabelZ), 42f, Color.white);
            }

            if (state.IsEmpty)
            {
                CreateLabel(topicRoot, "NULL", Vector3.zero, 38f, Color.white);
                return;
            }

            float headX = startX;
            float tailX = startX + (state.Count - 1) * spacing;
            float markerY = scaledNodeSize * 0.5f + markerVerticalGap * currentLayoutScale;
            if (state.Count == 1)
            {
                CreateLabel(topicRoot, "HEAD / TAIL", new Vector3(headX, markerY, rootLabelZ), markerFontSize - 2f, TailColor);
            }
            else
            {
                CreateLabel(topicRoot, "HEAD", new Vector3(headX, markerY, rootLabelZ), markerFontSize, HeadColor);
                CreateLabel(topicRoot, "TAIL", new Vector3(tailX, markerY, rootLabelZ), markerFontSize, TailColor);
            }

            float nullX = tailX + spacing * 0.62f;
            CreateLabel(topicRoot, "→", new Vector3(tailX + spacing * 0.31f, 0f, rootLabelZ), 42f, Color.white);
            CreateLabel(topicRoot, "NULL", new Vector3(nullX, 0f, rootLabelZ), 28f, Color.white);
        }

        private void CreateLabel(Transform parent, string text, Vector3 position, float fontSize, Color color)
        {
            GameObject labelObject = new GameObject("Linked List Label - " + text);
            labelObject.transform.SetParent(parent, false);
            labelObject.transform.localPosition = position;
            labelObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            float labelLayoutScale = parent == topicRoot ? currentLayoutScale : 1f;
            labelObject.transform.localScale = new Vector3(-labelScale * labelLayoutScale, labelScale * labelLayoutScale, labelScale * labelLayoutScale);

            TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.outlineWidth = 0.18f;
            label.outlineColor = new Color32(20, 24, 45, 255);
            label.alignment = TextAlignmentOptions.Center;
            label.enableAutoSizing = true;
            label.fontSizeMin = fontSize * 0.75f;
            label.fontSizeMax = fontSize;
            label.enableWordWrapping = false;
            label.rectTransform.sizeDelta = new Vector2(text == "HEAD / TAIL" ? 170f : 100f, 45f);
            if (TMP_Settings.defaultFontAsset != null) label.font = TMP_Settings.defaultFontAsset;
        }

        private void EnsureMaterials()
        {
            if (nodeMaterial != null && highlightMaterial != null) return;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            nodeMaterial = new Material(shader) { color = NodeColor };
            highlightMaterial = new Material(shader) { color = HighlightColor };
        }

        private void SendTopicEvent(LinkedListActionResult result)
        {
            SpikeBridge.SendMessageToShell(LociMessageTypes.TopicEvent, new TopicEventPayload
            {
                TopicId = LociTopicId.LinkedList,
                Action = result.Action,
                Value = result.Value,
                Result = result.Result,
                Details = result.Details
            });
        }

        private static LinkedListActionResult Result(string action, string value, string outcome, string details) =>
            new LinkedListActionResult(action, value, outcome, details);

        private void CloseTopic()
        {
            StopAllCoroutines();
            isOpen = false;
            isSequenceRunning = false;
            highlightedIndex = -1;
            if (topicRoot != null) { Destroy(topicRoot.gameObject); topicRoot = null; }
        }

        private void DestroyMaterials()
        {
            if (nodeMaterial != null) Destroy(nodeMaterial);
            if (highlightMaterial != null) Destroy(highlightMaterial);
            nodeMaterial = null;
            highlightMaterial = null;
        }

        private sealed class LinkedListActionResult
        {
            public string Action { get; }
            public string Value { get; }
            public string Result { get; }
            public string Details { get; }
            public LinkedListActionResult(string action, string value, string result, string details)
            { Action = action; Value = value; Result = result; Details = details; }
        }
    }
}
