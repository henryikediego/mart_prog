using System.Collections;
using System.Collections.Generic;
using Loci.AR;
using Loci.Bridge;
using TMPro;
using UnityEngine;

namespace Loci.Topics.Loops
{
    /// <summary>Renders the loop's DATA → LOOP → RESULT flow; all 2D controls stay in React.</summary>
    public sealed class LoopsTopicController : MonoBehaviour
    {
        private static readonly Color32 DataColor = new Color32(102, 204, 255, 255);
        private static readonly Color32 LoopColor = new Color32(255, 217, 26, 255);
        private static readonly Color32 ResultColor = new Color32(35, 194, 122, 255);
        private static readonly Color32 ValueColor = new Color32(91, 75, 255, 255);

        [Header("3D display")]
        [SerializeField, Min(0.05f)] private float valueBoxSize = 0.09f;
        [SerializeField, Min(0.08f)] private float loopBlockWidth = 0.15f;
        [SerializeField, Min(0.05f)] private float loopBlockHeight = 0.105f;
        [SerializeField, Min(0.06f)] private float resultBoxSize = 0.09f;
        [SerializeField, Min(0.001f)] private float labelScale = 0.006f;
        [SerializeField, Min(0.01f)] private float stepPulseDuration = 0.2f;
        [SerializeField, Min(0.05f)] private float runIterationPause = 0.35f;

        private LoopsState state;
        private Transform topicRoot;
        private readonly List<Transform> valueBoxes = new List<Transform>();
        private Transform resultBox;
        private Transform loopBlock;
        private Material valueMaterial;
        private Material currentMaterial;
        private Material loopMaterial;
        private Material resultMaterial;
        private bool isOpen;
        private bool isSequenceRunning;

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
            if (payload.TopicId != LociTopicId.Loops) { CloseTopic(); return; }

            LociARController ar = LociARController.Instance;
            if (ar == null || !ar.IsPlaced || ar.ContentRoot == null) return;

            CloseTopic();
            state = new LoopsState();
            topicRoot = new GameObject("Loops 3D Content").transform;
            topicRoot.SetParent(ar.ContentRoot, false);
            isOpen = true;
            Render();
            SpikeBridge.SendMessageToShell(LociMessageTypes.TopicLoaded,
                new TopicLoadedPayload { TopicId = LociTopicId.Loops });
        }

        private void Act(TopicActionPayload payload)
        {
            if (!isOpen || payload == null || payload.TopicId != LociTopicId.Loops || isSequenceRunning)
                return;

            string value = payload.Value == null ? string.Empty : payload.Value.Trim();
            switch (payload.Action)
            {
                case "Set Values": SetValues(value); break;
                case "Step": StartCoroutine(StepOnce()); break;
                case "Run Loop": StartCoroutine(RunToCompletion()); break;
                case "Reset":
                    state.Reset();
                    Render();
                    SendEvent("Reset", string.Empty, "Success", "Loops reset to values 10, 20, 30 with total 0.");
                    break;
                default:
                    Debug.LogWarning("LoopsTopicController ignored unsupported action: " + payload.Action);
                    break;
            }
        }

        private void SetValues(string input)
        {
            if (input.Length == 0)
            {
                SendEvent("Set Values", input, "Failed", "Enter one to six whole numbers separated by commas.");
                return;
            }

            string[] parts = input.Split(',');
            if (parts.Length > LoopsState.MaximumValueCount)
            {
                SendEvent("Set Values", input, "Failed",
                    $"Received {parts.Length} values; Loops accepts at most {LoopsState.MaximumValueCount}.");
                return;
            }

            List<int> parsedValues = new List<int>(parts.Length);
            foreach (string part in parts)
            {
                string cleaned = part.Trim();
                if (cleaned.Length == 0)
                {
                    SendEvent("Set Values", input, "Failed", "Each comma-separated position must contain a whole number.");
                    return;
                }
                if (!int.TryParse(cleaned, out int parsed))
                {
                    SendEvent("Set Values", input, "Failed", "Every loop value must be a whole number.");
                    return;
                }
                parsedValues.Add(parsed);
            }

            if (!state.TrySetValues(parsedValues))
            {
                SendEvent("Set Values", input, "Failed", "Loops accepts one to six whole numbers.");
                return;
            }

            Render();
            SendEvent("Set Values", input, "Success", $"Loop values set to {state.FormatValues()} and the running total was reset to 0.");
        }

        private IEnumerator StepOnce()
        {
            isSequenceRunning = true;
            bool advanced = state.Step();
            Render();
            if (advanced)
            {
                yield return Pulse(state.CurrentIndex >= 0 && state.CurrentIndex < valueBoxes.Count
                    ? valueBoxes[state.CurrentIndex] : null);
                yield return Pulse(resultBox);
            }

            string value = advanced ? state.Values[state.CurrentIndex].ToString() : string.Empty;
            string details = advanced
                ? $"Visited index {state.CurrentIndex} with value {value}; running total is {state.RunningTotal}."
                : $"The loop is complete; running total remains {state.RunningTotal}.";
            SendEvent("Step", value, "Success", details);
            isSequenceRunning = false;
        }

        private IEnumerator RunToCompletion()
        {
            isSequenceRunning = true;
            state.BeginRun();
            Render();
            yield return new WaitForSeconds(0.3f);

            while (state.Step())
            {
                Render();
                if (state.CurrentIndex >= 0 && state.CurrentIndex < valueBoxes.Count)
                    yield return Pulse(valueBoxes[state.CurrentIndex]);
                yield return Pulse(resultBox);
                yield return new WaitForSeconds(runIterationPause);
            }

            state.CompleteRun();
            Render();
            SendEvent("Run Loop", state.RunningTotal.ToString(), "Success",
                $"Visited {state.VisitedCount} values ({state.FormatValues()}) and returned a final total of {state.RunningTotal}.");
            isSequenceRunning = false;
        }

        private IEnumerator Pulse(Transform target)
        {
            if (target == null) yield break;
            Vector3 original = target.localScale;
            Vector3 enlarged = original * 1.18f;
            float halfDuration = stepPulseDuration * 0.5f;
            float elapsed = 0f;
            while (elapsed < halfDuration && target != null)
            {
                elapsed += Time.deltaTime;
                target.localScale = Vector3.Lerp(original, enlarged, Mathf.Clamp01(elapsed / halfDuration));
                yield return null;
            }
            elapsed = 0f;
            while (elapsed < halfDuration && target != null)
            {
                elapsed += Time.deltaTime;
                target.localScale = Vector3.Lerp(enlarged, original, Mathf.Clamp01(elapsed / halfDuration));
                yield return null;
            }
            if (target != null) target.localScale = original;
        }

        private void Render()
        {
            if (topicRoot == null) return;
            for (int i = topicRoot.childCount - 1; i >= 0; i--)
                Destroy(topicRoot.GetChild(i).gameObject);
            valueBoxes.Clear();
            EnsureMaterials();

            int count = state.Values.Count;
            float size = count <= 3 ? valueBoxSize : valueBoxSize * Mathf.Lerp(1f, 0.82f, (count - 3) / 3f);
            float spacing = count <= 3 ? 0.115f : Mathf.Lerp(0.105f, 0.078f, (count - 3) / 3f);
            float dataWidth = size + (count - 1) * spacing;
            float sectionGap = 0.04f;
            float totalWidth = dataWidth + sectionGap + loopBlockWidth + sectionGap + resultBoxSize;
            topicRoot.localScale = Vector3.one * Mathf.Min(1f, 0.65f / totalWidth);

            float leftEdge = -totalWidth * 0.5f;
            float dataCenter = leftEdge + dataWidth * 0.5f;
            float firstCellX = dataCenter - (count - 1) * spacing * 0.5f;
            float loopLeft = leftEdge + dataWidth + sectionGap;
            float loopCenter = loopLeft + loopBlockWidth * 0.5f;
            float outputLeft = loopLeft + loopBlockWidth + sectionGap;
            float outputCenter = outputLeft + resultBoxSize * 0.5f;

            for (int i = 0; i < count; i++)
            {
                float x = firstCellX + i * spacing;
                bool current = i == state.CurrentIndex;
                Transform cell = CreateBox("Loop Value " + i, new Vector3(x, 0f, 0f),
                    new Vector3(size, size, size * 0.2f), current ? currentMaterial : valueMaterial);
                valueBoxes.Add(cell);
                CreateLabel(i.ToString(), new Vector3(x, -size * 0.75f, -size * 0.1f), 30f, Color.white, 0.055f);
                if (current)
                    CreateLabel("CURRENT", new Vector3(x, size * 0.78f + 0.035f, -0.006f), 23f, LoopColor, 0.12f, FontStyles.Bold);
            }

            CreateLabel("DATA", new Vector3(dataCenter, 0.14f, -0.006f), 25f, DataColor, 0.15f, FontStyles.Bold);
            CreateLabel("LOOP", new Vector3(loopCenter, 0.14f, -0.006f), 25f, LoopColor, 0.12f, FontStyles.Bold);
            CreateLabel("RESULT", new Vector3(outputCenter, 0.14f, -0.006f), 25f, ResultColor, 0.14f, FontStyles.Bold);
            CreateLabel("→", new Vector3(dataCenter + dataWidth * 0.5f + sectionGap * 0.5f, 0f, -0.006f), 36f, Color.white, 0.04f, FontStyles.Bold);
            CreateLabel("→", new Vector3(loopLeft + loopBlockWidth + sectionGap * 0.5f, 0f, -0.006f), 36f, Color.white, 0.04f, FontStyles.Bold);

            loopBlock = CreateBox("Loop Block", new Vector3(loopCenter, 0f, 0f),
                new Vector3(loopBlockWidth, loopBlockHeight, 0.025f), loopMaterial);
            CreateLabel(state.LoopMessage, new Vector3(loopCenter, 0f, -0.016f), 20f, Color.white,
                loopBlockWidth * 0.94f, FontStyles.Bold);

            resultBox = CreateBox("Loop Running Total", new Vector3(outputCenter, 0f, 0f),
                new Vector3(resultBoxSize, resultBoxSize, resultBoxSize * 0.2f), resultMaterial);
            CreateLabel(state.RunningTotal.ToString(), new Vector3(outputCenter, 0f, -resultBoxSize * 0.12f),
                42f, Color.white, resultBoxSize * 0.88f);
            CreateLabel("total", new Vector3(outputCenter, -resultBoxSize * 0.82f, -0.006f), 22f, ResultColor, 0.09f);
        }

        private Transform CreateBox(string objectName, Vector3 position, Vector3 size, Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = objectName;
            box.transform.SetParent(topicRoot, false);
            box.transform.localPosition = position;
            box.transform.localScale = size;
            Collider collider = box.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            box.GetComponent<Renderer>().sharedMaterial = material;
            return box.transform;
        }

        private void CreateLabel(string text, Vector3 position, float fontSize, Color color, float width,
            FontStyles style = FontStyles.Normal)
        {
            GameObject labelObject = new GameObject("Loops Label - " + text);
            labelObject.transform.SetParent(topicRoot, false);
            labelObject.transform.localPosition = position;
            labelObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            labelObject.transform.localScale = new Vector3(-labelScale, labelScale, labelScale);
            TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.fontStyle = style;
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = true;
            label.enableAutoSizing = true;
            label.fontSizeMin = fontSize * 0.62f;
            label.fontSizeMax = fontSize;
            label.rectTransform.sizeDelta = new Vector2(width / labelScale, 50f);
            if (TMP_Settings.defaultFontAsset != null) label.font = TMP_Settings.defaultFontAsset;
        }

        private void EnsureMaterials()
        {
            if (valueMaterial != null) return;
            valueMaterial = CreateMaterial(ValueColor);
            currentMaterial = CreateMaterial(LoopColor);
            loopMaterial = CreateMaterial(new Color32(18, 20, 43, 255));
            resultMaterial = CreateMaterial(ResultColor);
        }

        private static Material CreateMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            return new Material(shader) { color = color };
        }

        private void SendEvent(string action, string value, string result, string details)
        {
            SpikeBridge.SendMessageToShell(LociMessageTypes.TopicEvent, new TopicEventPayload
            {
                TopicId = LociTopicId.Loops,
                Action = action,
                Value = value,
                Result = result,
                Details = details
            });
        }

        private void CloseTopic()
        {
            StopAllCoroutines();
            isOpen = false;
            isSequenceRunning = false;
            valueBoxes.Clear();
            resultBox = loopBlock = null;
            if (topicRoot != null)
            {
                Destroy(topicRoot.gameObject);
                topicRoot = null;
            }
        }

        private void DestroyMaterials()
        {
            if (valueMaterial != null) Destroy(valueMaterial);
            if (currentMaterial != null) Destroy(currentMaterial);
            if (loopMaterial != null) Destroy(loopMaterial);
            if (resultMaterial != null) Destroy(resultMaterial);
            valueMaterial = currentMaterial = loopMaterial = resultMaterial = null;
        }
    }
}
