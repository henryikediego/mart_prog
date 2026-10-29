using System.Collections;
using Loci.AR;
using Loci.Bridge;
using TMPro;
using UnityEngine;

namespace Loci.Topics.ArrayList
{
    /// <summary>
    /// Renders the Array/List as 3D objects and receives operations from the
    /// shell. All 2D controls and explanatory panels belong to the shell.
    /// </summary>
    public sealed class ArrayListTopicController : MonoBehaviour
    {
        private static readonly Color32 CellColor = new Color32(91, 75, 255, 255);
        private static readonly Color32 FoundColor = new Color32(245, 207, 80, 255);
        private static readonly Color32 LabelColor = new Color32(255, 255, 255, 255);

        [Header("3D display")]
        [SerializeField, Min(0.04f)] private float cellSize = 0.12f;
        [SerializeField, Min(0.08f)] private float cellSpacing = 0.14f;
        [SerializeField, Min(0.01f)] private float labelScale = 0.05f;
        [SerializeField, Min(0.01f)] private float spawnDuration = 0.18f;

        private readonly ArrayListState state = new ArrayListState();
        private Transform topicRoot;
        private bool isOpen;
        private int highlightedIndex = -1;
        private Material cellMaterial;
        private Material foundMaterial;

        private void OnEnable()
        {
            SpikeBridge.ShellCommandReceived += OnShellCommand;
        }

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
                case LociMessageTypes.TopicOpen:
                    HandleTopicOpen(payload as TopicOpenPayload);
                    break;
                case LociMessageTypes.TopicAction:
                    HandleTopicAction(payload as TopicActionPayload);
                    break;
                case LociMessageTypes.TopicClose:
                case LociMessageTypes.PlacementReset:
                case LociMessageTypes.SessionStop:
                    CloseTopic();
                    break;
            }
        }

        private void HandleTopicOpen(TopicOpenPayload payload)
        {
            if (payload == null)
                return;

            if (payload.TopicId != LociTopicId.ArrayList)
            {
                CloseTopic();
                return;
            }

            LociARController arController = LociARController.Instance;
            if (arController == null || !arController.IsPlaced || arController.ContentRoot == null)
                return; // LociARController reports E-106 before this subscriber handles the command.

            CloseTopic();
            state.Reset();
            highlightedIndex = -1;

            GameObject rootObject = new GameObject("Array List 3D Content");
            topicRoot = rootObject.transform;
            topicRoot.SetParent(arController.ContentRoot, false);
            isOpen = true;
            RenderValues(true);

            SpikeBridge.SendMessageToShell(
                LociMessageTypes.TopicLoaded,
                new TopicLoadedPayload { TopicId = LociTopicId.ArrayList });
        }

        private void HandleTopicAction(TopicActionPayload payload)
        {
            if (!isOpen || payload == null || payload.TopicId != LociTopicId.ArrayList)
                return;

            ArrayListActionResult result = PerformAction(payload.Action, payload.Value);
            if (result == null)
                return;

            RenderValues(true);
            SpikeBridge.SendMessageToShell(
                LociMessageTypes.TopicEvent,
                new TopicEventPayload
                {
                    TopicId = LociTopicId.ArrayList,
                    Action = result.Action,
                    Value = result.Value,
                    Result = result.Result,
                    Details = result.Details
                });
        }

        private ArrayListActionResult PerformAction(string action, string input)
        {
            string value = input == null ? string.Empty : input.Trim();
            highlightedIndex = -1;

            switch (action)
            {
                case "Insert":
                    if (value.Length == 0)
                        return Result("Insert", value, "Failed", "No value was entered for Array/List insert.");
                    if (!int.TryParse(value, out int insertValue))
                        return Result("Insert", value, "Failed", "The entered value is not a valid number.");
                    if (!state.TryInsert(insertValue, out int insertedIndex))
                        return Result("Insert", value, "Failed", "Array/List reached its five-value capacity.");
                    return Result("Array Insert", insertValue.ToString(), "Success", $"Inserted value {insertValue} at index {insertedIndex}.");

                case "Delete":
                    if (!state.TryDeleteLast(out int deletedValue))
                        return Result("Delete", string.Empty, "Failed", "Array/List was empty.");
                    return Result("Delete", deletedValue.ToString(), "Success", "Value removed from the end of Array/List.");

                case "Search":
                    if (value.Length == 0)
                        return Result("Search", value, "Failed", "No value was entered for Array/List search.");
                    if (!int.TryParse(value, out int searchValue))
                        return Result("Search", value, "Failed", "The entered value is not a valid number.");
                    if (state.Values.Count == 0)
                        return Result("Search", value, "Failed", "Array/List is empty.");
                    highlightedIndex = state.FindIndex(searchValue);
                    return highlightedIndex >= 0
                        ? Result("Search", value, "Success", $"Value {searchValue} was found at index {highlightedIndex}.")
                        : Result("Search", value, "Failed", "Value was not found in Array/List.");

                case "Traverse":
                    if (state.Values.Count == 0)
                        return Result("Traverse", string.Empty, "Failed", "Array/List is empty.");
                    return Result("Traverse", state.GetTraversalOrder(), "Success", "Array/List values were traversed from index 0 to the last index.");

                case "Reset":
                    state.Reset();
                    return Result("Reset", string.Empty, "Success", "Array/List reset to its default values.");

                default:
                    Debug.LogWarning("ArrayListTopicController ignored an unsupported action: " + action);
                    return null;
            }
        }

        private static ArrayListActionResult Result(string action, string value, string result, string details)
        {
            return new ArrayListActionResult(action, value, result, details);
        }

        private void RenderValues(bool animate)
        {
            if (topicRoot == null)
                return;

            for (int i = topicRoot.childCount - 1; i >= 0; i--)
                Destroy(topicRoot.GetChild(i).gameObject);

            EnsureMaterials();
            float spacing = state.Values.Count <= 3
                ? cellSpacing
                : Mathf.Lerp(cellSpacing, cellSpacing * 0.78f, (state.Values.Count - 3) / 2f);
            float startX = -(state.Values.Count - 1) * spacing * 0.5f;

            for (int i = 0; i < state.Values.Count; i++)
            {
                float x = startX + i * spacing;
                GameObject cell = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cell.name = "Array Cell " + i;
                cell.transform.SetParent(topicRoot, false);
                cell.transform.localPosition = new Vector3(x, 0f, 0f);
                cell.transform.localScale = new Vector3(cellSize, cellSize, cellSize * 0.22f);
                Collider collider = cell.GetComponent<Collider>();
                if (collider != null)
                    Destroy(collider);

                MeshRenderer renderer = cell.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = i == highlightedIndex ? foundMaterial : cellMaterial;

                CreateLabel(topicRoot, state.Values[i].ToString(), new Vector3(x, 0f, cellSize * 0.12f), 64f, LabelColor);
                CreateLabel(topicRoot, i.ToString(), new Vector3(x, -cellSize * 0.72f, cellSize * 0.1f), 38f, LabelColor);

                if (animate)
                {
                    Vector3 finalScale = cell.transform.localScale;
                    cell.transform.localScale = Vector3.zero;
                    StartCoroutine(AnimateIn(cell.transform, finalScale));
                }
            }
        }

        private void CreateLabel(Transform parent, string text, Vector3 localPosition, float fontSize, Color color)
        {
            GameObject labelObject = new GameObject("Array Label");
            labelObject.transform.SetParent(parent, false);
            labelObject.transform.localPosition = localPosition;
            labelObject.transform.localScale = Vector3.one * labelScale;

            TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.enableAutoSizing = true;
            label.fontSizeMin = fontSize * 0.75f;
            label.fontSizeMax = fontSize;
            label.rectTransform.sizeDelta = new Vector2(100f, 45f);
            if (TMP_Settings.defaultFontAsset != null)
                label.font = TMP_Settings.defaultFontAsset;
        }

        private IEnumerator AnimateIn(Transform target, Vector3 finalScale)
        {
            if (target == null)
                yield break;

            float elapsed = 0f;
            while (elapsed < spawnDuration && target != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / spawnDuration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                target.localScale = Vector3.LerpUnclamped(Vector3.zero, finalScale, eased);
                yield return null;
            }

            if (target != null)
                target.localScale = finalScale;
        }

        private void EnsureMaterials()
        {
            if (cellMaterial != null && foundMaterial != null)
                return;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            cellMaterial = new Material(shader) { color = CellColor };
            foundMaterial = new Material(shader) { color = FoundColor };
        }

        private void CloseTopic()
        {
            isOpen = false;
            highlightedIndex = -1;
            if (topicRoot != null)
            {
                Destroy(topicRoot.gameObject);
                topicRoot = null;
            }
        }

        private void DestroyMaterials()
        {
            if (cellMaterial != null)
                Destroy(cellMaterial);
            if (foundMaterial != null)
                Destroy(foundMaterial);
            cellMaterial = null;
            foundMaterial = null;
        }

        private sealed class ArrayListActionResult
        {
            public string Action { get; }
            public string Value { get; }
            public string Result { get; }
            public string Details { get; }

            public ArrayListActionResult(string action, string value, string result, string details)
            {
                Action = action;
                Value = value;
                Result = result;
                Details = details;
            }
        }
    }
}
