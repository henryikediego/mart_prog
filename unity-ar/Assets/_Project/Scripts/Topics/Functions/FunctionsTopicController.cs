using System.Collections;
using Loci.AR;
using Loci.Bridge;
using TMPro;
using UnityEngine;

namespace Loci.Topics.Functions
{
    /// <summary>Renders the Functions data flow in AR; all 2D controls stay in React.</summary>
    public sealed class FunctionsTopicController : MonoBehaviour
    {
        private static readonly Color32 ValueColor = new Color32(91, 75, 255, 255);
        private static readonly Color32 InputAccent = new Color32(102, 204, 255, 255);
        private static readonly Color32 FunctionAccent = new Color32(255, 217, 26, 255);
        private static readonly Color32 OutputAccent = new Color32(35, 194, 122, 255);

        [Header("3D display")]
        [SerializeField, Min(0.06f)] private float inputBoxSize = 0.08f;
        [SerializeField, Min(0.08f)] private float outputBoxSize = 0.095f;
        [SerializeField, Min(0.08f)] private float functionPanelWidth = 0.125f;
        [SerializeField, Min(0.04f)] private float functionPanelHeight = 0.08f;
        [SerializeField, Min(0.001f)] private float labelScale = 0.006f;
        [SerializeField, Min(0.01f)] private float actionPulseDuration = 0.18f;

        private FunctionsState state;
        private Transform topicRoot;
        private Transform inputABox;
        private Transform inputBBox;
        private Transform functionPanel;
        private Transform outputBox;
        private Material valueMaterial;
        private Material panelMaterial;
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
            if (payload.TopicId != LociTopicId.Functions) { CloseTopic(); return; }

            LociARController ar = LociARController.Instance;
            if (ar == null || !ar.IsPlaced || ar.ContentRoot == null) return;

            CloseTopic();
            state = new FunctionsState();
            topicRoot = new GameObject("Functions 3D Content").transform;
            topicRoot.SetParent(ar.ContentRoot, false);
            isOpen = true;
            Render();
            SpikeBridge.SendMessageToShell(LociMessageTypes.TopicLoaded,
                new TopicLoadedPayload { TopicId = LociTopicId.Functions });
        }

        private void Act(TopicActionPayload payload)
        {
            if (!isOpen || payload == null || payload.TopicId != LociTopicId.Functions || isAnimating)
                return;

            string value = payload.Value == null ? string.Empty : payload.Value.Trim();
            switch (payload.Action)
            {
                case "Set Input": SetInput(value); return;
                case "Add":
                case "Subtract":
                case "Multiply":
                    if (!state.Call(payload.Action)) return;
                    Render();
                    StartCoroutine(AnimateFunction(payload.Action));
                    return;
                case "Reset":
                    state.Reset();
                    Render();
                    SendEvent("Reset", string.Empty, "Success", "Function parameters and return value reset to 10, 20, and 30.");
                    return;
                default:
                    Debug.LogWarning("FunctionsTopicController ignored unsupported action: " + payload.Action);
                    return;
            }
        }

        private void SetInput(string value)
        {
            string[] parts = value.Split(',');
            if (value.Length == 0)
            {
                SendEvent("Set Input", value, "Failed", "Enter two numbers separated by a comma. For example, 15,25.");
                return;
            }
            if (parts.Length != 2)
            {
                SendEvent("Set Input", value, "Failed", "Enter exactly two numbers separated by a comma. For example, 15,25.");
                return;
            }
            if (!int.TryParse(parts[0].Trim(), out int a) || !int.TryParse(parts[1].Trim(), out int b))
            {
                SendEvent("Set Input", value, "Failed", "Both function inputs must be whole numbers.");
                return;
            }

            state.SetInput(a, b);
            Render();
            SendEvent("Set Input", $"{a},{b}", "Success",
                $"Set a = {a} and b = {b}. The return value remains {state.ReturnValue} until a function is called.");
        }

        private IEnumerator AnimateFunction(string action)
        {
            isAnimating = true;
            yield return Pulse(inputABox);
            yield return Pulse(inputBBox);
            yield return Pulse(functionPanel);
            yield return Pulse(outputBox);
            SendEvent(action, $"{state.ParameterA},{state.ParameterB}", "Success",
                $"{state.CurrentFunction}(a, b) received {state.ParameterA} and {state.ParameterB}, then returned {state.ReturnValue}.");
            isAnimating = false;
        }

        private IEnumerator Pulse(Transform target)
        {
            if (target == null) yield break;
            Vector3 original = target.localScale;
            Vector3 enlarged = original * 1.14f;
            float elapsed = 0f;
            float halfDuration = actionPulseDuration * 0.5f;
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

            EnsureMaterials();
            inputABox = CreateValueBox("Input a", new Vector3(-0.16f, 0f, 0f), inputBoxSize, state.ParameterA);
            inputBBox = CreateValueBox("Input b", new Vector3(-0.07f, 0f, 0f), inputBoxSize, state.ParameterB);
            outputBox = CreateValueBox("Return value", new Vector3(0.21f, 0f, 0f), outputBoxSize, state.ReturnValue);

            functionPanel = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
            functionPanel.name = "Function panel";
            functionPanel.SetParent(topicRoot, false);
            functionPanel.localPosition = new Vector3(0.065f, 0f, 0f);
            functionPanel.localScale = new Vector3(functionPanelWidth, functionPanelHeight, 0.025f);
            Collider panelCollider = functionPanel.GetComponent<Collider>();
            if (panelCollider != null) Destroy(panelCollider);
            functionPanel.GetComponent<Renderer>().sharedMaterial = panelMaterial;

            CreateLabel("INPUTS", new Vector3(-0.115f, 0.095f, -0.006f), 24f, InputAccent, FontStyles.Bold, 0.13f);
            CreateLabel("FUNCTION", new Vector3(0.065f, 0.095f, -0.006f), 21f, FunctionAccent, FontStyles.Bold, 0.12f);
            CreateLabel("OUTPUT", new Vector3(0.21f, 0.095f, -0.006f), 24f, OutputAccent, FontStyles.Bold, 0.12f);
            CreateLabel("a", new Vector3(-0.16f, -0.065f, -0.006f), 28f, InputAccent, FontStyles.Bold, 0.05f);
            CreateLabel("b", new Vector3(-0.07f, -0.065f, -0.006f), 28f, InputAccent, FontStyles.Bold, 0.05f);
            CreateLabel("return", new Vector3(0.21f, -0.072f, -0.006f), 23f, OutputAccent, FontStyles.Bold, 0.10f);
            CreateLabel("→", new Vector3(-0.014f, 0f, -0.006f), 38f, Color.white, FontStyles.Bold, 0.035f);
            CreateLabel("→", new Vector3(0.145f, 0f, -0.006f), 38f, Color.white, FontStyles.Bold, 0.035f);
            string functionText = $"{state.CurrentFunction}(a, b)";
            CreateLabel(functionText, new Vector3(0.065f, 0f, -0.016f), 24f, Color.white, FontStyles.Bold, functionPanelWidth * 0.92f);
        }

        private Transform CreateValueBox(string objectName, Vector3 position, float size, int value)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = objectName;
            box.transform.SetParent(topicRoot, false);
            box.transform.localPosition = position;
            box.transform.localScale = new Vector3(size, size, size * 0.2f);
            Collider collider = box.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            box.GetComponent<Renderer>().sharedMaterial = valueMaterial;
            CreateLabel(value.ToString(), position + new Vector3(0f, 0f, -size * 0.115f), 48f, Color.white, FontStyles.Normal, size * 0.88f);
            return box.transform;
        }

        private TextMeshPro CreateLabel(string text, Vector3 position, float fontSize, Color color, FontStyles style, float width)
        {
            GameObject labelObject = new GameObject("Functions Label - " + text);
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
            label.fontSizeMin = fontSize * 0.65f;
            label.fontSizeMax = fontSize;
            label.rectTransform.sizeDelta = new Vector2(width / labelScale, 50f);
            if (TMP_Settings.defaultFontAsset != null) label.font = TMP_Settings.defaultFontAsset;
            return label;
        }

        private void EnsureMaterials()
        {
            if (valueMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                valueMaterial = new Material(shader) { color = ValueColor };
            }
            if (panelMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                panelMaterial = new Material(shader) { color = new Color32(18, 20, 43, 255) };
            }
        }

        private void SendEvent(string action, string value, string result, string details)
        {
            SpikeBridge.SendMessageToShell(LociMessageTypes.TopicEvent, new TopicEventPayload
            {
                TopicId = LociTopicId.Functions,
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
            inputABox = inputBBox = functionPanel = outputBox = null;
            if (topicRoot != null)
            {
                Destroy(topicRoot.gameObject);
                topicRoot = null;
            }
        }

        private void DestroyMaterials()
        {
            if (valueMaterial != null) Destroy(valueMaterial);
            if (panelMaterial != null) Destroy(panelMaterial);
            valueMaterial = panelMaterial = null;
        }
    }
}
