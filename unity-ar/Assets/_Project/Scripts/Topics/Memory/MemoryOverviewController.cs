using Loci.AR;
using Loci.Bridge;
using TMPro;
using UnityEngine;

namespace Loci.Topics.Memory
{
    /// <summary>Renders the Memory Overview table; React owns the activity picker and all 2D UI.</summary>
    public sealed class MemoryOverviewController : MonoBehaviour
    {
        private static readonly Color32 TitleColor = new Color32(255, 217, 26, 255);
        private static readonly Color32 HeaderBackground = new Color32(20, 22, 30, 255);
        private static readonly Color32 HeaderText = new Color32(102, 204, 255, 255);
        private static readonly Color32 ValueText = new Color32(102, 255, 140, 255);
        private static readonly Color32 EmptyText = new Color32(230, 230, 230, 255);

        // Source table dimensions scaled to the portrait-phone layout used by MemoryModule.
        private const float TableWidth = 0.612f;
        private const float HeaderHeight = 0.0432f;
        private const float RowDepth = 0.0192f;
        private const float LabelScale = 0.006f;
        private const float OverviewScaleMultiplier = 1.45f;
        private const string OverviewActivityId = "overview";

        [SerializeField, Range(0.05f, 0.2f)] private float horizontalViewportMargin = 0.08f;

        private Transform topicRoot;
        private Material headerMaterial;
        private bool isOpen;

        private void OnEnable() => SpikeBridge.ShellCommandReceived += OnShellCommand;

        private void OnDisable()
        {
            SpikeBridge.ShellCommandReceived -= OnShellCommand;
            CloseTopic();
            if (headerMaterial != null) Destroy(headerMaterial);
            headerMaterial = null;
        }

        private void OnShellCommand(LociMessageEnvelope message, ILociMessagePayload payload)
        {
            switch (message.Type)
            {
                case LociMessageTypes.TopicOpen: Open(payload as TopicOpenPayload); break;
                case LociMessageTypes.TopicClose:
                case LociMessageTypes.PlacementReset:
                case LociMessageTypes.SessionStop: CloseTopic(); break;
            }
        }

        private void Update()
        {
            if (isOpen && topicRoot != null) UpdateViewportFit();
        }

        private void Open(TopicOpenPayload payload)
        {
            if (payload == null) return;
            bool isOverview = payload.TopicId == LociTopicId.Memory && payload.ActivityId == OverviewActivityId;
            if (!isOverview) { CloseTopic(); return; }

            LociARController ar = LociARController.Instance;
            if (ar == null || !ar.IsPlaced || ar.ContentRoot == null) return;

            CloseTopic();
            topicRoot = new GameObject("Memory Overview 3D Content").transform;
            topicRoot.SetParent(ar.ContentRoot, false);
            isOpen = true;
            Render();
            UpdateViewportFit();
            SpikeBridge.SendMessageToShell(LociMessageTypes.TopicLoaded,
                new TopicLoadedPayload { TopicId = LociTopicId.Memory });
        }

        private void UpdateViewportFit()
        {
            Camera arCamera = Camera.main;
            if (arCamera == null) return;

            float cameraDepth = arCamera.WorldToViewportPoint(topicRoot.position).z;
            if (cameraDepth <= 0f) return;

            Vector3 left = arCamera.ViewportToWorldPoint(
                new Vector3(horizontalViewportMargin, 0.5f, cameraDepth));
            Vector3 right = arCamera.ViewportToWorldPoint(
                new Vector3(1f - horizontalViewportMargin, 0.5f, cameraDepth));
            float availableWidth = Vector3.Distance(left, right);
            float overviewWidth = TableWidth * OverviewScaleMultiplier;
            float fit = Mathf.Min(1f, availableWidth / overviewWidth);
            topicRoot.localScale = Vector3.one * (OverviewScaleMultiplier * fit);
        }

        private void Render()
        {
            if (!isOpen || topicRoot == null) return;
            if (headerMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                headerMaterial = new Material(shader) { color = HeaderBackground };
            }

            CreateBox("Memory Table Header", new Vector3(0f, 0.042f, 0f),
                new Vector3(TableWidth, HeaderHeight, RowDepth), headerMaterial);
            CreateLabel("MEMORY TABLE", new Vector3(0f, 0.102f, -0.012f), 36f, TitleColor, 0.30f, FontStyles.Bold);

            CreateLabel("ADDRESS", new Vector3(-0.243f, 0.042f, -0.012f), 21f, HeaderText, 0.105f, FontStyles.Bold);
            CreateLabel("NAME", new Vector3(-0.1215f, 0.042f, -0.012f), 21f, HeaderText, 0.10f, FontStyles.Bold);
            CreateLabel("TYPE", new Vector3(-0.0135f, 0.042f, -0.012f), 21f, HeaderText, 0.09f, FontStyles.Bold);
            CreateLabel("VALUE", new Vector3(0.099f, 0.042f, -0.012f), 21f, ValueText, 0.10f, FontStyles.Bold);
            CreateLabel("SIZE", new Vector3(0.2295f, 0.042f, -0.012f), 21f, HeaderText, 0.08f, FontStyles.Bold);

            CreateLabel("No variables allocated yet.", new Vector3(0f, -0.03f, -0.012f),
                30f, EmptyText, 0.52f);
        }

        private void CreateBox(string objectName, Vector3 position, Vector3 size, Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = objectName;
            box.transform.SetParent(topicRoot, false);
            box.transform.localPosition = position;
            box.transform.localScale = size;
            Collider collider = box.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            box.GetComponent<Renderer>().sharedMaterial = material;
        }

        private void CreateLabel(string text, Vector3 position, float fontSize, Color color, float width,
            FontStyles style = FontStyles.Normal)
        {
            GameObject labelObject = new GameObject("Memory Overview Label - " + text);
            labelObject.transform.SetParent(topicRoot, false);
            labelObject.transform.localPosition = position;
            labelObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            labelObject.transform.localScale = new Vector3(-LabelScale, LabelScale, LabelScale);
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
            label.rectTransform.sizeDelta = new Vector2(width / LabelScale, 50f);
            if (TMP_Settings.defaultFontAsset != null) label.font = TMP_Settings.defaultFontAsset;
        }

        private void CloseTopic()
        {
            isOpen = false;
            if (topicRoot != null)
            {
                Destroy(topicRoot.gameObject);
                topicRoot = null;
            }
        }
    }
}
