using System.Collections;
using Loci.AR;
using Loci.Bridge;
using TMPro;
using UnityEngine;

namespace Loci.Topics.Selection
{
    /// <summary>AR presentation and bridge actions for the Else-if Ladder activity.</summary>
    public sealed class ElseIfLadderController : MonoBehaviour
    {
        private static readonly Color32 ValueColor = new Color32(91, 75, 255, 255);
        private static readonly Color32 ValueAccent = new Color32(102, 204, 255, 255);
        private static readonly Color32 RuleAccent = new Color32(255, 217, 26, 255);
        private static readonly Color32 ResultAccent = new Color32(35, 194, 122, 255);

        [Header("3D display")]
        [SerializeField, Min(0.06f)] private float valueBoxSize = 0.095f;
        [SerializeField, Min(0.10f)] private float ruleBoxWidth = 0.19f;
        [SerializeField, Min(0.05f)] private float ruleBoxHeight = 0.095f;
        [SerializeField, Min(0.06f)] private float resultBoxSize = 0.095f;
        [SerializeField, Min(0.001f)] private float labelScale = 0.006f;
        [SerializeField, Min(0.01f)] private float checkPulseDuration = 0.2f;

        private const float SectionGap = 0.035f;
        private ElseIfLadderState state;
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
            bool isLadder = payload.TopicId == LociTopicId.Selection && payload.ActivityId == "else_if_ladder";
            if (!isLadder) { CloseTopic(); return; }

            LociARController ar = LociARController.Instance;
            if (ar == null || !ar.IsPlaced || ar.ContentRoot == null) return;

            CloseTopic();
            state = new ElseIfLadderState();
            topicRoot = new GameObject("Selection Else-if Ladder 3D Content").transform;
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
                case "Set Score": SetScore(value); break;
                case "Rule Info":
                    SendEvent("Rule Info", state.RuleName, "Success", "Current preset ladder: " + state.RuleName + ". Use Toggle Rule to switch examples.");
                    break;
                case "Toggle Rule":
                    string rule = state.ToggleRule();
                    Render();
                    SendEvent("Toggle Rule", rule, "Success", "Else-if Ladder rule changed to " + rule + ".");
                    break;
                case "Check": StartCoroutine(Check()); break;
                case "Reset":
                    state.Reset();
                    Render();
                    SendEvent("Reset", string.Empty, "Success", "Else-if Ladder reset to score 75, Grade Rule, and pending result.");
                    break;
                default:
                    Debug.LogWarning("ElseIfLadderController ignored unsupported action: " + payload.Action);
                    break;
            }
        }

        private void SetScore(string input)
        {
            if (!int.TryParse(input, out int parsed))
            {
                SendEvent("Set Score", input, "Failed", "Enter one whole-number score, such as 75.");
                return;
            }

            state.SetScore(parsed);
            Render();
            SendEvent("Set Score", parsed.ToString(), "Success", "Score set to " + parsed + ". Click Check to evaluate it using the " + state.RuleName + ".");
        }

        private IEnumerator Check()
        {
            isAnimating = true;
            string result = state.Evaluate(out string trace);
            Render();
            yield return Pulse(ruleBox, 1.10f);
            yield return Pulse(resultBox, 1.14f);
            SendEvent("Check", state.Score.ToString(), "Success",
                "Score " + state.Score + " was checked using the " + state.RuleName + ". Conditions evaluated in order: " + trace + ". Result = " + result + ". The first true branch determines the result.");
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

            float totalWidth = valueBoxSize + SectionGap + ruleBoxWidth + SectionGap + resultBoxSize;
            float left = -totalWidth * 0.5f;
            float valueX = left + valueBoxSize * 0.5f;
            float ruleX = left + valueBoxSize + SectionGap + ruleBoxWidth * 0.5f;
            float resultX = left + valueBoxSize + SectionGap + ruleBoxWidth + SectionGap + resultBoxSize * 0.5f;

            CreateBox("Else-if Score", new Vector3(valueX, 0f, 0f), new Vector3(valueBoxSize, valueBoxSize, valueBoxSize * 0.2f), valueMaterial);
            CreateLabel(state.Score.ToString(), new Vector3(valueX, 0f, -valueBoxSize * 0.12f), 42f, Color.white, valueBoxSize * 0.88f);
            ruleBox = CreateBox("Else-if Rule", new Vector3(ruleX, 0f, 0f), new Vector3(ruleBoxWidth, ruleBoxHeight, 0.025f), ruleMaterial);
            CreateLabel(state.RuleName, new Vector3(ruleX, 0f, -0.016f), 25f, Color.white, ruleBoxWidth * 0.92f, FontStyles.Bold);
            resultBox = CreateBox("Else-if Result", new Vector3(resultX, 0f, 0f), new Vector3(resultBoxSize, resultBoxSize, resultBoxSize * 0.2f), valueMaterial);
            CreateLabel(state.Result, new Vector3(resultX, 0f, -resultBoxSize * 0.12f), 42f, Color.white, resultBoxSize * 0.88f);

            CreateLabel("SCORE", new Vector3(valueX, 0.14f, -0.006f), 25f, ValueAccent, 0.12f, FontStyles.Bold);
            CreateLabel("LADDER", new Vector3(ruleX, 0.14f, -0.006f), 25f, RuleAccent, 0.14f, FontStyles.Bold);
            CreateLabel("RESULT", new Vector3(resultX, 0.14f, -0.006f), 25f, ResultAccent, 0.13f, FontStyles.Bold);
            CreateLabel("→", new Vector3(valueX + valueBoxSize * 0.5f + SectionGap * 0.5f, 0f, -0.006f), 36f, Color.white, 0.035f, FontStyles.Bold);
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
            GameObject labelObject = new GameObject("Else-if Label - " + text);
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
