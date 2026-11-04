using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Loci.AR;
using Loci.Bridge;
using TMPro;
using UnityEngine;

namespace Loci.Topics.Memory
{
    /// <summary>AR memory table and guided variable prediction; 2D controls remain in React.</summary>
    public sealed class MemoryVariablesController : MonoBehaviour
    {
        private static readonly Color32 TitleColor = new Color32(255, 217, 26, 255);
        private static readonly Color32 HeaderBackground = new Color32(20, 22, 30, 255);
        private static readonly Color32 HeaderText = new Color32(102, 204, 255, 255);
        private static readonly Color32 ValueText = new Color32(102, 255, 140, 255);
        private static readonly Color32 RowColor = new Color32(91, 75, 255, 255);
        private static readonly Color32 HighlightColor = new Color32(255, 217, 26, 255);
        private static readonly Color32 FocusMarkerColor = new Color32(102, 204, 255, 255);
        private static readonly Color32 BodyText = new Color32(245, 245, 250, 255);
        private static readonly Regex NamePattern = new Regex("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

        private const float TableWidth = 0.612f;
        private const float HeaderHeight = 0.0432f;
        private const float RowHeight = 0.0468f;
        private const float RowStep = 0.052f;
        private const float RowDepth = 0.0192f;
        private const float LabelScale = 0.006f;
        private const float PortraitScaleMultiplier = 1.45f;
        private const float LandscapeScaleMultiplier = 2.0f;
        private const string ActivityId = "variables";
        private const string DataTypesActivityId = "data_types";

        [SerializeField, Range(0.05f, 0.2f)] private float horizontalViewportMargin = 0.08f;
        [SerializeField, Min(0.05f)] private float highlightDuration = 0.35f;

        private sealed class VariableRecord
        {
            public string Address;
            public string Name;
            public string Type;
            public string Value;
            public string Size;
        }

        private sealed class PredictionStep
        {
            public readonly string Code;
            public readonly string Question;
            public readonly string Name;
            public readonly string Type;
            public readonly string Expected;

            public PredictionStep(string code, string question, string name, string type, string expected)
            {
                Code = code;
                Question = question;
                Name = name;
                Type = type;
                Expected = expected;
            }
        }

        private static readonly PredictionStep[] PredictionSteps =
        {
            new PredictionStep("int age = 20;", "Predict the value that will be stored in age.", "age", "int", "20"),
            new PredictionStep("float price = 15.5;", "Predict the value that will be stored in price.", "price", "float", "15.5"),
            new PredictionStep("char grade = 'A';", "Predict the value that will be stored in grade.", "grade", "char", "'A'"),
            new PredictionStep("age = 21;", "Predict the new value that will replace the old value of age.", "age", "int", "21"),
            new PredictionStep("bool passed = true;", "Predict the value that will be stored in passed.", "passed", "bool", "true")
        };

        private readonly List<VariableRecord> records = new List<VariableRecord>();
        private readonly List<Transform> rowTransforms = new List<Transform>();
        private readonly List<Material> rowMaterials = new List<Material>();
        private Transform topicRoot;
        private Material headerMaterial;
        private bool isOpen;
        private bool isBusy;
        private bool isDataTypesActivity;
        private int predictionIndex;
        private bool waitingForPrediction;

        private void OnEnable() => SpikeBridge.ShellCommandReceived += OnShellCommand;

        private void OnDisable()
        {
            SpikeBridge.ShellCommandReceived -= OnShellCommand;
            CloseTopic();
            DestroyMaterial(ref headerMaterial);
            for (int i = 0; i < rowMaterials.Count; i++) Destroy(rowMaterials[i]);
            rowMaterials.Clear();
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

        private void Update()
        {
            if (isOpen && topicRoot != null) UpdateViewportFit();
        }

        private void Open(TopicOpenPayload payload)
        {
            if (payload == null) return;
            if (payload.TopicId != LociTopicId.Memory ||
                (payload.ActivityId != ActivityId && payload.ActivityId != DataTypesActivityId))
            {
                CloseTopic();
                return;
            }

            LociARController ar = LociARController.Instance;
            if (ar == null || !ar.IsPlaced || ar.ContentRoot == null) return;

            CloseTopic();
            records.Clear();
            predictionIndex = 0;
            waitingForPrediction = false;
            isDataTypesActivity = payload.ActivityId == DataTypesActivityId;
            topicRoot = new GameObject(isDataTypesActivity ? "Memory Data Types 3D Content" : "Memory Variables 3D Content").transform;
            topicRoot.SetParent(ar.ContentRoot, false);
            isOpen = true;
            Render();
            UpdateViewportFit();
            SpikeBridge.SendMessageToShell(LociMessageTypes.TopicLoaded,
                new TopicLoadedPayload { TopicId = LociTopicId.Memory });
        }

        private void Act(TopicActionPayload payload)
        {
            if (!isOpen || payload == null || payload.TopicId != LociTopicId.Memory || isBusy) return;
            string value = payload.Value == null ? string.Empty : payload.Value.Trim();
            if (isDataTypesActivity)
            {
                switch (payload.Action)
                {
                    case "Show Types": ShowDataTypes(); break;
                    case "Reset": ResetDataTypes(); break;
                    default:
                        SendEvent(payload.Action, value, "Failed", "Use Show Types to display the common data-type examples.");
                        break;
                }
                return;
            }

            switch (payload.Action)
            {
                case "Set/Inspect": SetOrInspect(value); break;
                case "Predict Step": PredictStep(value); break;
                case "Reset": Reset(); break;
                default: Debug.LogWarning("MemoryVariablesController ignored unsupported action: " + payload.Action); break;
            }
        }

        private void SetOrInspect(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                SendEvent("Set/Inspect", input, "Failed", "Enter a declaration such as int age=20, an update such as age=25, or a name such as age to inspect.");
                return;
            }

            if (!input.Contains("="))
            {
                int index = records.FindIndex(record => record.Name == input);
                if (index < 0)
                {
                    SendEvent("Inspect", input, "Failed", "No memory record named " + input + " was found.");
                    return;
                }

                VariableRecord record = records[index];
                Render(index, "INSPECTED");
                StartCoroutine(CompleteAfterHighlight("Inspect", input, "Success",
                    "Inspect: " + record.Name + " is a " + record.Type + " variable. It stores " + record.Value +
                    " at address " + record.Address + " and uses " + record.Size + "."));
                return;
            }

            if (!TryParseAssignment(input, out string type, out string name, out string value, out string error))
            {
                SendEvent("Set Variable", input, "Failed", error);
                return;
            }

            int existingIndex = records.FindIndex(record => record.Name == name);
            if (existingIndex >= 0 && type == null) type = records[existingIndex].Type;
            if (type == null) type = InferType(value);
            if (type == null)
            {
                SendEvent("Set Variable", input, "Failed", "Could not identify the variable type. Add a type such as int, float, double, char, bool, or string.");
                return;
            }
            if (!IsCompatible(type, value))
            {
                SendEvent("Set Variable", input, "Failed", TypeError(type, name));
                return;
            }

            bool allocated = existingIndex < 0;
            string previous = allocated ? string.Empty : records[existingIndex].Value;
            VariableRecord updated = new VariableRecord
            {
                Address = allocated ? AddressForIndex(records.Count) : records[existingIndex].Address,
                Name = name,
                Type = type,
                Value = value,
                Size = TypeSize(type)
            };
            if (allocated) records.Add(updated);
            else records[existingIndex] = updated;

            waitingForPrediction = false;
            Render(records.IndexOf(updated), allocated ? "ALLOCATED" : "UPDATED");
            string detail = allocated
                ? "Allocated new variable " + name + " with value " + value + "."
                : "Updated " + name + " from " + previous + " to " + value + ".";
            StartCoroutine(CompleteAfterHighlight("Set Variable", input, "Success",
                (allocated ? type + " " : string.Empty) + name + " = " + value + "; " + detail));
        }

        private void PredictStep(string answer)
        {
            if (!waitingForPrediction)
            {
                if (predictionIndex >= PredictionSteps.Length)
                {
                    SendEvent("Predict Step", string.Empty, "Finished", "The guided variable prediction is complete. Reset to start again.");
                    return;
                }

                waitingForPrediction = true;
                PredictionStep step = PredictionSteps[predictionIndex];
                Render();
                SendEvent("Predict Step", step.Code, "Prompt",
                    "Next statement: " + step.Code + " Question: " + step.Question + " Enter your predicted value, then choose Predict Step again.");
                return;
            }

            if (answer.Length == 0)
            {
                SendEvent("Predict Step", answer, "Failed", "Enter your predicted value before choosing Predict Step again.");
                return;
            }

            PredictionStep current = PredictionSteps[predictionIndex];
            bool correct = NormalizePrediction(answer) == NormalizePrediction(current.Expected);
            int index = records.FindIndex(record => record.Name == current.Name);
            bool allocated = index < 0;
            string previous = allocated ? string.Empty : records[index].Value;
            VariableRecord recordToWrite = new VariableRecord
            {
                Address = allocated ? AddressForIndex(records.Count) : records[index].Address,
                Name = current.Name,
                Type = current.Type,
                Value = current.Expected,
                Size = TypeSize(current.Type)
            };
            if (allocated) records.Add(recordToWrite);
            else records[index] = recordToWrite;

            waitingForPrediction = false;
            predictionIndex++;
            Render(records.IndexOf(recordToWrite), "UPDATED");
            string result = correct ? "Correct" : "Incorrect";
            string details = (correct ? "Correct." : "Not correct.") + " Executed " + current.Code +
                " Expected value: " + current.Expected + ". " +
                (allocated ? "Allocated " + current.Name + " in memory." : "Updated " + current.Name + " from " + previous + " to " + current.Expected + ".");
            StartCoroutine(CompleteAfterHighlight("Predict Step", answer, result, details));
        }

        private IEnumerator CompleteAfterHighlight(string action, string value, string result, string details)
        {
            isBusy = true;
            if (rowTransforms.Count > 0)
            {
                int index = Mathf.Clamp(records.Count - 1, 0, rowTransforms.Count - 1);
                // For updates and inspection, Render stores the focused row last in render order via this field.
                if (focusedRowIndex >= 0 && focusedRowIndex < rowTransforms.Count) index = focusedRowIndex;
                yield return HighlightRow(index);
            }
            SendEvent(action, value, result, details);
            isBusy = false;
        }

        private int focusedRowIndex = -1;

        private IEnumerator HighlightRow(int index)
        {
            if (index < 0 || index >= rowTransforms.Count) yield break;
            Transform row = rowTransforms[index];
            Vector3 original = row.localScale;
            Vector3 enlarged = original * 1.08f;
            float half = highlightDuration * 0.5f;
            float elapsed = 0f;
            while (elapsed < half && row != null)
            {
                elapsed += Time.deltaTime;
                row.localScale = Vector3.Lerp(original, enlarged, Mathf.Clamp01(elapsed / half));
                yield return null;
            }
            elapsed = 0f;
            while (elapsed < half && row != null)
            {
                elapsed += Time.deltaTime;
                row.localScale = Vector3.Lerp(enlarged, original, Mathf.Clamp01(elapsed / half));
                yield return null;
            }
            if (row != null) row.localScale = original;
        }

        private void Reset()
        {
            records.Clear();
            predictionIndex = 0;
            waitingForPrediction = false;
            focusedRowIndex = -1;
            Render();
            SendEvent("Reset", string.Empty, "Success", "Variables reset: the memory table is empty and the guided prediction is ready to start.");
        }

        private void ShowDataTypes()
        {
            records.Clear();
            predictionIndex = 0;
            waitingForPrediction = false;
            focusedRowIndex = -1;

            string[,] demo =
            {
                { "age", "int", "20" },
                { "price", "float", "15.5" },
                { "grade", "char", "'A'" },
                { "passed", "bool", "true" },
                { "name", "string", "\"Mike\"" }
            };
            int byteOffset = 0;
            for (int i = 0; i < demo.GetLength(0); i++)
            {
                string type = demo[i, 1];
                records.Add(new VariableRecord
                {
                    Address = "0x" + (4096 + byteOffset).ToString("X"),
                    Name = demo[i, 0],
                    Type = type,
                    Value = demo[i, 2],
                    Size = TypeSize(type)
                });
                byteOffset += TypeByteCount(type);
            }

            Render();
            SendEvent("Show Types", string.Empty, "Success",
                "Displayed common data types with their values, size-aware addresses, and memory sizes: int, float, char, bool, and string.");
        }

        private void ResetDataTypes()
        {
            records.Clear();
            focusedRowIndex = -1;
            Render();
            SendEvent("Reset", string.Empty, "Success", "Data Types reset. Use Show Types to display the examples again.");
        }

        private static string TypeSize(string type)
        {
            switch (type)
            {
                case "int":
                case "float": return "4 bytes";
                case "char":
                case "bool": return "1 byte";
                case "string": return "varies";
                default: return "unknown";
            }
        }

        private static int TypeByteCount(string type)
        {
            switch (type)
            {
                case "int":
                case "float": return 4;
                case "char":
                case "bool": return 1;
                case "string": return 8;
                default: return 4;
            }
        }

        private void Render(int focusIndex = -1, string marker = "")
        {
            if (!isOpen || topicRoot == null) return;
            focusedRowIndex = focusIndex;
            ClearVisuals();
            EnsureMaterials();

            CreateBox("Memory Variables Header", new Vector3(0f, 0.042f, 0f),
                new Vector3(TableWidth, HeaderHeight, RowDepth), headerMaterial);
            CreateLabel("MEMORY TABLE", new Vector3(0f, 0.103f, -0.012f), 36f, TitleColor, 0.30f, FontStyles.Bold);
            CreateLabel("ADDRESS", new Vector3(-0.243f, 0.042f, -0.012f), 21f, HeaderText, 0.105f, FontStyles.Bold);
            CreateLabel("NAME", new Vector3(-0.1215f, 0.042f, -0.012f), 21f, HeaderText, 0.10f, FontStyles.Bold);
            CreateLabel("TYPE", new Vector3(-0.0135f, 0.042f, -0.012f), 21f, HeaderText, 0.09f, FontStyles.Bold);
            CreateLabel("VALUE", new Vector3(0.099f, 0.042f, -0.012f), 21f, ValueText, 0.10f, FontStyles.Bold);
            CreateLabel("SIZE", new Vector3(0.2295f, 0.042f, -0.012f), 21f, HeaderText, 0.08f, FontStyles.Bold);

            if (records.Count == 0)
            {
                CreateLabel("No variables allocated yet.", new Vector3(0f, -0.03f, -0.012f), 30f, BodyText, 0.52f);
            }
            else
            {
                for (int i = 0; i < records.Count; i++)
                {
                    float y = -0.012f - i * RowStep;
                    Material material = CreateMaterial(i == focusIndex ? HighlightColor : RowColor);
                    rowMaterials.Add(material);
                    Transform row = CreateBox("Memory Variable Row " + records[i].Name,
                        new Vector3(0f, y, 0f), new Vector3(TableWidth, RowHeight, RowDepth), material);
                    rowTransforms.Add(row);
                    VariableRecord record = records[i];
                    Color valueColor = i == focusIndex ? new Color32(20, 22, 30, 255) : BodyText;
                    CreateLabel(record.Address, new Vector3(-0.243f, y, -0.012f), 21f, valueColor, 0.105f);
                    CreateLabel(record.Name, new Vector3(-0.1215f, y, -0.012f), 21f, valueColor, 0.10f);
                    CreateLabel(record.Type, new Vector3(-0.0135f, y, -0.012f), 21f, valueColor, 0.09f);
                    CreateLabel(record.Value, new Vector3(0.099f, y, -0.012f), 21f, valueColor, 0.10f);
                    CreateLabel(record.Size, new Vector3(0.2295f, y, -0.012f), 21f, valueColor, 0.08f);
                    if (i == focusIndex && marker.Length > 0)
                        CreateLabel(marker, new Vector3(0f, y - RowStep * 0.82f, -0.014f), 26f,
                            FocusMarkerColor, 0.56f, FontStyles.Bold);
                }
            }

            UpdateViewportFit();
        }

        private void UpdateViewportFit()
        {
            Camera camera = Camera.main;
            if (camera == null || topicRoot == null) return;
            float depth = camera.WorldToViewportPoint(topicRoot.position).z;
            if (depth <= 0f) return;
            Vector3 left = camera.ViewportToWorldPoint(new Vector3(horizontalViewportMargin, 0.5f, depth));
            Vector3 right = camera.ViewportToWorldPoint(new Vector3(1f - horizontalViewportMargin, 0.5f, depth));
            float availableWidth = Vector3.Distance(left, right);
            Vector3 bottom = camera.ViewportToWorldPoint(new Vector3(0.5f, horizontalViewportMargin, depth));
            Vector3 top = camera.ViewportToWorldPoint(new Vector3(0.5f, 1f - horizontalViewportMargin, depth));
            float availableHeight = Vector3.Distance(bottom, top);
            bool landscape = Screen.width > Screen.height;
            float scaleCap = landscape ? LandscapeScaleMultiplier : PortraitScaleMultiplier;
            float contentHeight = 0.24f + Mathf.Max(1, records.Count) * RowStep;
            float fit = Mathf.Min(1f,
                Mathf.Min(availableWidth / (TableWidth * scaleCap), availableHeight / (contentHeight * scaleCap)));
            topicRoot.localScale = Vector3.one * (scaleCap * fit);
        }

        private bool TryParseAssignment(string input, out string type, out string name, out string value, out string error)
        {
            type = null;
            name = string.Empty;
            value = string.Empty;
            error = string.Empty;
            string declaration = input.Trim();
            string[] knownTypes = { "int", "float", "double", "char", "bool", "string" };
            for (int i = 0; i < knownTypes.Length; i++)
            {
                string prefix = knownTypes[i] + " ";
                if (!declaration.StartsWith(prefix, StringComparison.Ordinal)) continue;
                type = knownTypes[i];
                declaration = declaration.Substring(prefix.Length);
                break;
            }

            int equals = declaration.IndexOf('=');
            if (equals < 0 || equals != declaration.LastIndexOf('='))
            {
                error = "Use the format name=value or type name=value, such as int age=20.";
                return false;
            }
            name = declaration.Substring(0, equals).Trim();
            value = declaration.Substring(equals + 1).Trim();
            if (name.Length == 0 || value.Length == 0)
            {
                error = "Variable name and value are required.";
                return false;
            }
            if (!NamePattern.IsMatch(name))
            {
                error = name + " is not a valid variable name. Start with a letter or underscore and use only letters, numbers, and underscores.";
                return false;
            }
            return true;
        }

        private static string InferType(string value)
        {
            string lower = value.ToLowerInvariant();
            if (lower == "true" || lower == "false") return "bool";
            if (value.StartsWith("\"") && value.EndsWith("\"")) return "string";
            if (value.StartsWith("'") && value.EndsWith("'"))
                return value.Length >= 2 && value.Substring(1, value.Length - 2).Length == 1 ? "char" : "string";
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)) return "int";
            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _)) return "float";
            return null;
        }

        private static bool IsCompatible(string type, string value)
        {
            switch (type)
            {
                case "int": return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
                case "float":
                case "double": return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
                case "bool": return value.Equals("true", StringComparison.OrdinalIgnoreCase) || value.Equals("false", StringComparison.OrdinalIgnoreCase);
                case "char": return value.Length >= 3 && value[0] == '\'' && value[value.Length - 1] == '\'' && value.Substring(1, value.Length - 2).Length == 1;
                case "string": return value.Length >= 2 && ((value[0] == '"' && value[value.Length - 1] == '"') || (value[0] == '\'' && value[value.Length - 1] == '\''));
                default: return false;
            }
        }

        private static string TypeSize(string type)
        {
            switch (type)
            {
                case "int":
                case "float": return "4 bytes";
                case "double": return "8 bytes";
                case "char":
                case "bool": return "1 byte";
                case "string": return "varies";
                default: return "unknown";
            }
        }

        private static string TypeError(string type, string name)
        {
            switch (type)
            {
                case "int": return name + " is an int, so use a whole number like 25.";
                case "float": return name + " is a float, so use a number like 25.5.";
                case "double": return name + " is a double, so use a number like 25.5.";
                case "bool": return name + " is a bool, so use true or false.";
                case "char": return name + " is a char, so use one character in single quotes, like 'A'.";
                case "string": return name + " is a string, so use text in quotes, like \"Mike\".";
                default: return "The value does not match the variable type.";
            }
        }

        private static string AddressForIndex(int index) => "0x" + (4096 + index * 4).ToString("X");

        private static string NormalizePrediction(string input)
        {
            string value = (input ?? string.Empty).Trim().ToLowerInvariant();
            if (value.Contains("="))
            {
                string[] parts = value.Split('=');
                if (parts.Length == 2) value = parts[1].Trim();
            }
            if (value.EndsWith(";")) value = value.Substring(0, value.Length - 1).Trim();
            if (value.Length >= 2 && ((value[0] == '\'' && value[value.Length - 1] == '\'') || (value[0] == '"' && value[value.Length - 1] == '"')))
                value = value.Substring(1, value.Length - 2);
            return value;
        }

        private void EnsureMaterials()
        {
            if (headerMaterial == null) headerMaterial = CreateMaterial(HeaderBackground);
        }

        private static Material CreateMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            return new Material(shader) { color = color };
        }

        private Transform CreateBox(string name, Vector3 position, Vector3 size, Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
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
            GameObject labelObject = new GameObject("Memory Variables Label - " + text);
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
            label.fontSizeMin = fontSize * 0.55f;
            label.fontSizeMax = fontSize;
            label.rectTransform.sizeDelta = new Vector2(width / LabelScale, 50f);
            if (TMP_Settings.defaultFontAsset != null) label.font = TMP_Settings.defaultFontAsset;
        }

        private void ClearVisuals()
        {
            rowTransforms.Clear();
            for (int i = 0; i < rowMaterials.Count; i++) Destroy(rowMaterials[i]);
            rowMaterials.Clear();
            for (int i = topicRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = topicRoot.GetChild(i);
                child.gameObject.SetActive(false);
                child.SetParent(null, false);
                Destroy(child.gameObject);
            }
        }

        private void SendEvent(string action, string value, string result, string details)
        {
            SpikeBridge.SendMessageToShell(LociMessageTypes.TopicEvent, new TopicEventPayload
            {
                TopicId = LociTopicId.Memory,
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
            isBusy = false;
            waitingForPrediction = false;
            if (topicRoot != null)
            {
                Destroy(topicRoot.gameObject);
                topicRoot = null;
            }
        }

        private static void DestroyMaterial(ref Material material)
        {
            if (material != null) Destroy(material);
            material = null;
        }
    }
}
