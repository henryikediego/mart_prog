using System.Collections;
using Loci.AR;
using Loci.Bridge;
using TMPro;
using UnityEngine;

namespace Loci.Topics.Selection
{
    /// <summary>Renders Basic If's value → condition → result in AR; controls stay in React.</summary>
    public sealed class SelectionTopicController : MonoBehaviour
    {
        private static readonly Color32 ValueColor = new Color32(91, 75, 255, 255);
        private static readonly Color32 ValueAccent = new Color32(102, 204, 255, 255);
        private static readonly Color32 ConditionAccent = new Color32(255, 217, 26, 255);
        private static readonly Color32 ResultAccent = new Color32(35, 194, 122, 255);

        [Header("3D display")]
        [SerializeField, Min(0.06f)] private float valueBoxSize = 0.095f;
        [SerializeField, Min(0.10f)] private float conditionBoxWidth = 0.19f;
        [SerializeField, Min(0.05f)] private float conditionBoxHeight = 0.095f;
        [SerializeField, Min(0.06f)] private float resultBoxSize = 0.095f;
        [SerializeField, Min(0.001f)] private float labelScale = 0.006f;
        [SerializeField, Min(0.01f)] private float checkPulseDuration = 0.2f;

        private const string BasicIfActivityId = "basic_if";
        private const float SectionGap = 0.035f;

        private SelectionState state;
        private Transform topicRoot;
        private Transform conditionBox;
        private Transform resultBox;
        private Material valueMaterial;
        private Material conditionMaterial;
        private bool isOpen;
        private bool isAnimating;

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
            if (payload.TopicId != LociTopicId.Selection) { CloseTopic(); return; }

            string activityId = string.IsNullOrEmpty(payload.ActivityId) ? BasicIfActivityId : payload.ActivityId;
            if (activityId != BasicIfActivityId) { CloseTopic(); return; }

            LociARController ar = LociARController.Instance;
            if (ar == null || !ar.IsPlaced || ar.ContentRoot == null) return;

            CloseTopic();
            state = new SelectionState();
            topicRoot = new GameObject("Selection Basic If 3D Content").transform;
            topicRoot.SetParent(ar.ContentRoot, false);
            isOpen = true;
            Render();
            SpikeBridge.SendMessageToShell(LociMessageTypes.TopicLoaded,
                new TopicLoadedPayload { TopicId = LociTopicId.Selection });
        }

        private void Act(TopicActionPayload payload)
        {
            if (!isOpen || payload == null || payload.TopicId != LociTopicId.Selection || isAnimating)
                return;

            string value = payload.Value == null ? string.Empty : payload.Value.Trim();
            switch (payload.Action)
            {
                case "Set Value": SetValue(value); break;
                case "Set Condition": SetCondition(value); break;
                case "Check": StartCoroutine(Check()); break;
                case "Toggle Rule":
                    string rule = state.ToggleRule();
                    Render();
                    SendEvent("Toggle Rule", rule, "Success", "Basic If condition changed to value " + rule + ".");
                    break;
                case "Reset":
                    state.Reset();
                    Render();
                    SendEvent("Reset", string.Empty, "Success", "Basic If reset to value 75, condition >= 50, and pending result.");
                    break;
                default:
                    Debug.LogWarning("SelectionTopicController ignored unsupported action: " + payload.Action);
                    break;
            }
        }

        private void SetValue(string input)
        {
            if (!int.TryParse(input, out int parsed))
            {
                SendEvent("Set Value", input, "Failed", "Enter one whole number, such as 75.");
                return;
            }

            state.SetValue(parsed);
            Render();
            SendEvent("Set Value", input, "Success",
                $"Value set to {parsed}. Click Check to evaluate value {state.ConditionText}.");
        }

        private void SetCondition(string input)
        {
            if (!state.TrySetCondition(input, out string normalized, out string error))
            {
                SendEvent("Set Condition", input, "Failed", error);
                return;
            }

            Render();
            SendEvent("Set Condition", "value " + normalized, "Success",
                "Basic If condition updated to value " + normalized + ". Click Check to evaluate it.");
        }

        private IEnumerator Check()
        {
            isAnimating = true;
            bool result = state.Evaluate();
            Render();
            yield return Pulse(conditionBox, 1.10f);
            yield return Pulse(resultBox, 1.14f);
            string resultWord = result ? "true" : "false";
            SendEvent("Check", "value " + state.ConditionText, "Success",
                $"Basic If checked value {state.Value}. Result = {resultWord}.");
            isAnimating = false;
        }

        private IEnumerator Pulse(Transform target, float multiplier)
        {
            if (target == null) yield break;
            Vector3 original = target.localScale;
            Vector3 enlarged = original * multiplier;
            float duration = checkPulseDuration * 0.5f;
            float elapsed = 0f;
            while (elapsed < duration && target != null)
            {
                elapsed += Time.deltaTime;
                target.localScale = Vector3.Lerp(original, enlarged, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            elapsed = 0f;
            while (elapsed < duration && target != null)
            {
                elapsed += Time.deltaTime;
                target.localScale = Vector3.Lerp(enlarged, original, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            if (target != null) target.localScale = original;
        }

        private void Render()
        {
            if (topicRoot == null || state == null) return;
            for (int i = topicRoot.childCount - 1; i >= 0; i--)
                Destroy(topicRoot.GetChild(i).gameObject);
            EnsureMaterials();

            float totalWidth = valueBoxSize + SectionGap + conditionBoxWidth + SectionGap + resultBoxSize;
            float left = -totalWidth * 0.5f;
            float valueX = left + valueBoxSize * 0.5f;
            float conditionX = left + valueBoxSize + SectionGap + conditionBoxWidth * 0.5f;
            float resultX = left + valueBoxSize + SectionGap + conditionBoxWidth + SectionGap + resultBoxSize * 0.5f;

            CreateBox("Selection Value", new Vector3(valueX, 0f, 0f),
                new Vector3(valueBoxSize, valueBoxSize, valueBoxSize * 0.2f), valueMaterial);
            CreateLabel(state.Value.ToString(), new Vector3(valueX, 0f, -valueBoxSize * 0.12f),
                42f, Color.white, valueBoxSize * 0.88f);

            conditionBox = CreateBox("Selection Condition", new Vector3(conditionX, 0f, 0f),
                new Vector3(conditionBoxWidth, conditionBoxHeight, 0.025f), conditionMaterial);
            CreateLabel("value " + state.ConditionText, new Vector3(conditionX, 0f, -0.016f),
                24f, Color.white, conditionBoxWidth * 0.92f, FontStyles.Bold);

            resultBox = CreateBox("Selection Result", new Vector3(resultX, 0f, 0f),
                new Vector3(resultBoxSize, resultBoxSize, resultBoxSize * 0.2f), valueMaterial);
            CreateLabel(state.ResultText, new Vector3(resultX, 0f, -resultBoxSize * 0.12f),
                42f, Color.white, resultBoxSize * 0.88f);

            CreateLabel("VALUE", new Vector3(valueX, 0.14f, -0.006f), 25f, ValueAccent, 0.12f, FontStyles.Bold);
            CreateLabel("CONDITION", new Vector3(conditionX, 0.14f, -0.006f), 25f, ConditionAccent, 0.17f, FontStyles.Bold);
            CreateLabel("RESULT", new Vector3(resultX, 0.14f, -0.006f), 25f, ResultAccent, 0.13f, FontStyles.Bold);
            CreateLabel("→", new Vector3(valueX + valueBoxSize * 0.5f + SectionGap * 0.5f, 0f, -0.006f), 36f, Color.white, 0.035f, FontStyles.Bold);
            CreateLabel("→", new Vector3(conditionX + conditionBoxWidth * 0.5f + SectionGap * 0.5f, 0f, -0.006f), 36f, Color.white, 0.035f, FontStyles.Bold);
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
            GameObject labelObject = new GameObject("Selection Label - " + text);
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
            label.enableWordWrapping = false;
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
            conditionMaterial = CreateMaterial(new Color32(18, 20, 43, 255));
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
                TopicId = LociTopicId.Selection,
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
            isAnimating = false;
            conditionBox = resultBox = null;
            if (topicRoot != null)
            {
                Destroy(topicRoot.gameObject);
                topicRoot = null;
            }
        }

        private void DestroyMaterials()
        {
            if (valueMaterial != null) Destroy(valueMaterial);
            if (conditionMaterial != null) Destroy(conditionMaterial);
            valueMaterial = conditionMaterial = null;
        }
    }
}
