using System.Collections;
using Loci.AR;
using Loci.Bridge;
using TMPro;
using UnityEngine;

namespace Loci.Topics.Selection
{
    /// <summary>AR presentation and bridge actions for the Nested If activity.</summary>
    public sealed class NestedIfController : MonoBehaviour
    {
        private static readonly Color32 ValueColor = new Color32(91, 75, 255, 255);
        private static readonly Color32 ValueAccent = new Color32(102, 204, 255, 255);
        private static readonly Color32 RuleAccent = new Color32(255, 217, 26, 255);
        private static readonly Color32 ResultAccent = new Color32(35, 194, 122, 255);

        [Header("3D display")]
        [SerializeField, Min(0.12f)] private float valueBoxWidth = 0.17f;
        [SerializeField, Min(0.06f)] private float valueBoxHeight = 0.095f;
        [SerializeField, Min(0.10f)] private float ruleBoxWidth = 0.19f;
        [SerializeField, Min(0.05f)] private float ruleBoxHeight = 0.095f;
        [SerializeField, Min(0.06f)] private float resultBoxHeight = 0.095f;
        [SerializeField, Min(0.001f)] private float labelScale = 0.006f;
        [SerializeField, Min(0.01f)] private float checkPulseDuration = 0.2f;

        private const float SectionGap = 0.035f;
        private NestedIfState state;
        private Transform topicRoot;
        private Transform ruleBox;
        private Transform resultBox;
        private Material valueMaterial;
        private Material ruleMaterial;
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
            bool isNested = payload.TopicId == LociTopicId.Selection && payload.ActivityId == "nested_if";
            if (!isNested) { CloseTopic(); return; }

            LociARController ar = LociARController.Instance;
            if (ar == null || !ar.IsPlaced || ar.ContentRoot == null) return;

            CloseTopic();
            state = new NestedIfState();
            topicRoot = new GameObject("Selection Nested If 3D Content").transform;
            topicRoot.SetParent(ar.ContentRoot, false);
            isOpen = true;
            Render();
            SpikeBridge.SendMessageToShell(LociMessageTypes.TopicLoaded,
                new TopicLoadedPayload { TopicId = LociTopicId.Selection });
        }

        private void Act(TopicActionPayload payload)
        {
            if (!isOpen || payload == null || payload.TopicId != LociTopicId.Selection || isAnimating) return;
            string value = payload.Value == null ? string.Empty : payload.Value.Trim();
            switch (payload.Action)
            {
                case "Set Values": SetValues(value); break;
                case "Rule Info":
                    SendEvent("Rule Info", state.RuleName, "Success", "Current nested rule is " + state.RuleName + ". Outer condition: " + state.OuterCondition + ". Inner condition: " + state.InnerCondition + ". The inner condition is evaluated only when the outer condition is true.");
                    break;
                case "Toggle Rule":
                    string rule = state.ToggleRule();
                    Render();
                    SendEvent("Toggle Rule", rule, "Success", "Nested If rule changed to " + rule + ". The inner condition is checked only when the outer condition is true.");
                    break;
                case "Check": StartCoroutine(Check()); break;
                case "Reset":
                    state.Reset();
                    Render();
                    SendEvent("Reset", string.Empty, "Success", "Nested If reset to 65,80, Pass Rule, and pending result.");
                    break;
                default:
                    Debug.LogWarning("NestedIfController ignored unsupported action: " + payload.Action);
                    break;
            }
        }

        private void SetValues(string input)
        {
            if (!state.TrySetValues(input, out string normalized, out string error))
            {
                SendEvent("Set Values", input, "Failed", error);
                return;
            }

            Render();
            SendEvent("Set Values", normalized, "Success",
                state.FirstName + " = " + state.FirstValue + ", " + state.SecondName + " = " + state.SecondValue + ". Click Check to evaluate them using " + state.RuleName + ".");
        }

        private IEnumerator Check()
        {
            isAnimating = true;
            string result = state.Evaluate(out string explanation);
            Render();
            yield return Pulse(ruleBox, 1.10f);
            yield return Pulse(resultBox, 1.14f);
            SendEvent("Check", state.FirstValue + "," + state.SecondValue, "Success",
                "Nested If checked the current values using " + state.RuleName + ". " + explanation + " Final result: " + result + ".");
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
            for (int i = topicRoot.childCount - 1; i >= 0; i--) Destroy(topicRoot.GetChild(i).gameObject);
            EnsureMaterials();

            string resultText = state.Result;
            float resultWidth = Mathf.Max(0.095f, resultText.Length * 0.0085f);
            float totalWidth = valueBoxWidth + SectionGap + ruleBoxWidth + SectionGap + resultWidth;
            float left = -totalWidth * 0.5f;
            float valueX = left + valueBoxWidth * 0.5f;
            float ruleX = left + valueBoxWidth + SectionGap + ruleBoxWidth * 0.5f;
            float resultX = left + valueBoxWidth + SectionGap + ruleBoxWidth + SectionGap + resultWidth * 0.5f;

            CreateBox("Nested If Values", new Vector3(valueX, 0f, 0f), new Vector3(valueBoxWidth, valueBoxHeight, 0.019f), valueMaterial);
            CreateLabel(state.FirstValue + " / " + state.SecondValue, new Vector3(valueX, 0f, -0.012f), 35f, Color.white, valueBoxWidth * 0.9f);
            ruleBox = CreateBox("Nested If Rule", new Vector3(ruleX, 0f, 0f), new Vector3(ruleBoxWidth, ruleBoxHeight, 0.025f), ruleMaterial);
            CreateLabel(state.RuleName, new Vector3(ruleX, 0f, -0.016f), 25f, Color.white, ruleBoxWidth * 0.92f, FontStyles.Bold);
            resultBox = CreateBox("Nested If Result", new Vector3(resultX, 0f, 0f), new Vector3(resultWidth, resultBoxHeight, 0.019f), valueMaterial);
            CreateLabel(resultText, new Vector3(resultX, 0f, -0.012f), 29f, Color.white, resultWidth * 0.9f);

            CreateLabel("VALUES", new Vector3(valueX, 0.14f, -0.006f), 25f, ValueAccent, 0.15f, FontStyles.Bold);
            CreateLabel("NESTED RULE", new Vector3(ruleX, 0.14f, -0.006f), 25f, RuleAccent, 0.19f, FontStyles.Bold);
            CreateLabel("RESULT", new Vector3(resultX, 0.14f, -0.006f), 25f, ResultAccent, resultWidth, FontStyles.Bold);
            CreateLabel("→", new Vector3(valueX + valueBoxWidth * 0.5f + SectionGap * 0.5f, 0f, -0.006f), 36f, Color.white, 0.035f, FontStyles.Bold);
            CreateLabel("→", new Vector3(ruleX + ruleBoxWidth * 0.5f + SectionGap * 0.5f, 0f, -0.006f), 36f, Color.white, 0.035f, FontStyles.Bold);
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

        private void CreateLabel(string text, Vector3 position, float fontSize, Color color, float width, FontStyles style = FontStyles.Normal)
        {
            GameObject labelObject = new GameObject("Nested If Label - " + text);
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
            ruleMaterial = CreateMaterial(new Color32(18, 20, 43, 255));
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
            ruleBox = resultBox = null;
            if (topicRoot != null) { Destroy(topicRoot.gameObject); topicRoot = null; }
        }

        private void DestroyMaterials()
        {
            if (valueMaterial != null) Destroy(valueMaterial);
            if (ruleMaterial != null) Destroy(ruleMaterial);
            valueMaterial = ruleMaterial = null;
        }
    }
}
