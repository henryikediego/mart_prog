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
    /// <summary>AR memory table activities, including guided prediction; 2D controls remain in React.</summary>
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
        private const float DataTypesLandscapeScaleMultiplier = 3.0f;
        private const string ActivityId = "variables";
        private const string DataTypesActivityId = "data_types";
        private const string AddressLayoutActivityId = "address_layout";
        private const string MemoryAccessActivityId = "memory_access";
        private const string LoopMemoryActivityId = "loop_memory";
        private const string ArraysActivityId = "arrays";
        private const string FunctionsActivityId = "functions";
        private const string MemorySelectionActivityId = "selection";
        private const string MemoryScopeActivityId = "scope";
        private const string MemoryRemoveVariableActivityId = "remove_variable";
        private const string MemoryTraceExerciseActivityId = "trace_exercise";
        private const string MemoryDebugExerciseActivityId = "debug_exercise";
        private const string MemoryFinalAssessmentActivityId = "final_assessment";

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

        private sealed class MemoryExerciseVariable
        {
            public readonly string Name;
            public readonly string Type;
            public readonly string Value;

            public MemoryExerciseVariable(string name, string type, string value)
            {
                Name = name;
                Type = type;
                Value = value;
            }
        }

        private sealed class MemoryExerciseQuestion
        {
            public readonly string Code;
            public readonly string Question;
            public readonly string Answer;
            public readonly string Focus;
            public readonly string Explanation;
            public readonly MemoryExerciseVariable[] FinalVariables;

            public MemoryExerciseQuestion(string code, string question, string answer, string focus,
                string explanation, params MemoryExerciseVariable[] finalVariables)
            {
                Code = code;
                Question = question;
                Answer = answer;
                Focus = focus;
                Explanation = explanation;
                FinalVariables = finalVariables;
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

        private static readonly MemoryExerciseQuestion[] MemoryExerciseQuestions =
        {
            new MemoryExerciseQuestion("int x = 4; int y = 6; x = y + 2;",
                "What value is stored in x after the last line?", "8", "x",
                "y stores 6, so y + 2 gives 8. Therefore, x is updated from 4 to 8.",
                new MemoryExerciseVariable("x", "int", "8"), new MemoryExerciseVariable("y", "int", "6")),
            new MemoryExerciseQuestion("int a = 10; int b = 3; b = a - b;",
                "What value is stored in b after the last line?", "7", "b",
                "a stores 10 and b stores 3. The expression a - b gives 7, so b becomes 7.",
                new MemoryExerciseVariable("a", "int", "10"), new MemoryExerciseVariable("b", "int", "7")),
            new MemoryExerciseQuestion("int score = 45; if (score >= 50) result = 1; else result = 0;",
                "What value is stored in result?", "0", "result",
                "score is 45, so score >= 50 is false. The else branch runs and result becomes 0.",
                new MemoryExerciseVariable("score", "int", "45"), new MemoryExerciseVariable("result", "int", "0")),
            new MemoryExerciseQuestion("int marks[3] = {70, 80, 90}; marks[1] = 85;",
                "What value is stored in marks[1]?", "85", "marks[1]",
                "Only index 1 is updated. marks[1] changes from 80 to 85.",
                new MemoryExerciseVariable("marks[0]", "int", "70"),
                new MemoryExerciseVariable("marks[1]", "int", "85"),
                new MemoryExerciseVariable("marks[2]", "int", "90")),
            new MemoryExerciseQuestion("int p = 5; int q = p; p = 9;",
                "What value is stored in q after p changes?", "5", "q",
                "q received a copy of p's value when p was 5. Changing p later does not change q.",
                new MemoryExerciseVariable("p", "int", "9"), new MemoryExerciseVariable("q", "int", "5"))
        };

        private static readonly MemoryExerciseQuestion[] DebugExerciseQuestions =
        {
            new MemoryExerciseQuestion("1. int age = 20;\n2. age = \"Mike\";",
                "Which line causes a type error?", "2", "age",
                "Line 2 is wrong because age is an int, but Mike is a string. The stored value remains 20.",
                new MemoryExerciseVariable("age", "int", "20")),
            new MemoryExerciseQuestion("1. int marks[3] = {70, 80, 90};\n2. marks[3] = 100;",
                "Which line tries to access an invalid array index?", "2", "marks[2]",
                "Line 2 is wrong because marks[3] is outside the valid indexes. For size 3, valid indexes are 0, 1, and 2.",
                new MemoryExerciseVariable("marks[0]", "int", "70"),
                new MemoryExerciseVariable("marks[1]", "int", "80"),
                new MemoryExerciseVariable("marks[2]", "int", "90")),
            new MemoryExerciseQuestion("1. char grade = 'A';\n2. grade = 'AB';",
                "Which line assigns an invalid char value?", "2", "grade",
                "Line 2 is wrong because a char should store one character only. AB has two characters.",
                new MemoryExerciseVariable("grade", "char", "'A'")),
            new MemoryExerciseQuestion("1. bool passed = true;\n2. passed = yes;",
                "Which line assigns an invalid bool value?", "2", "passed",
                "Line 2 is wrong because a bool should store true or false, not yes.",
                new MemoryExerciseVariable("passed", "bool", "true"))
        };

        private static readonly MemoryExerciseQuestion[] FinalAssessmentQuestions =
        {
            new MemoryExerciseQuestion("int x = 4;\nint y = 6;\nx = y + 2;",
                "What value is stored in x after the last line?", "8", "x",
                "y stores 6, so y + 2 gives 8. Therefore, x becomes 8.",
                new MemoryExerciseVariable("x", "int", "8"), new MemoryExerciseVariable("y", "int", "6")),
            new MemoryExerciseQuestion("float price = 10.5;\nprice = 12.5;",
                "What value is stored in price after the update?", "12.5", "price",
                "The original value 10.5 is replaced by 12.5 in the same memory location.",
                new MemoryExerciseVariable("price", "float", "12.5")),
            new MemoryExerciseQuestion("int marks[3] = {70, 80, 90};\nmarks[0] = 75;",
                "What value is stored in marks[0]?", "75", "marks[0]",
                "Only index 0 is updated. marks[0] changes from 70 to 75.",
                new MemoryExerciseVariable("marks[0]", "int", "75"),
                new MemoryExerciseVariable("marks[1]", "int", "80"),
                new MemoryExerciseVariable("marks[2]", "int", "90")),
            new MemoryExerciseQuestion("int score = 55;\nif (score >= 50) result = 1;\nelse result = 0;",
                "What value is stored in result?", "1", "result",
                "score is 55, so score >= 50 is true. The true branch runs and result becomes 1.",
                new MemoryExerciseVariable("score", "int", "55"), new MemoryExerciseVariable("result", "int", "1")),
            new MemoryExerciseQuestion("int a = 10;\nint b = 20;\nint sum = a + b;",
                "What value is stored in sum?", "30", "sum",
                "a stores 10 and b stores 20. Therefore, a + b gives 30.",
                new MemoryExerciseVariable("a", "int", "10"),
                new MemoryExerciseVariable("b", "int", "20"),
                new MemoryExerciseVariable("sum", "int", "30"))
        };

        private readonly List<VariableRecord> records = new List<VariableRecord>();
        private readonly List<Transform> rowTransforms = new List<Transform>();
        private readonly List<Material> rowMaterials = new List<Material>();
        private Transform topicRoot;
        private Material headerMaterial;
        private bool isOpen;
        private bool isBusy;
        private bool isDataTypesActivity;
        private bool isAddressLayoutActivity;
        private bool isMemoryAccessActivity;
        private bool isLoopMemoryActivity;
        private bool isArraysActivity;
        private bool isFunctionsActivity;
        private bool isMemorySelectionActivity;
        private bool isMemoryScopeActivity;
        private bool isMemoryRemoveVariableActivity;
        private bool isMemoryTraceExerciseActivity;
        private bool isMemoryDebugExerciseActivity;
        private bool isMemoryFinalAssessmentActivity;
        private bool needsFocusMarkerGap;
        private bool hasGlobalMarker;
        private int predictionIndex;
        private int memoryAccessStep;
        private int loopMemoryStep;
        private int functionMemoryStep;
        private int memorySelectionStep;
        private int memoryScopeStep;
        private int memoryRemoveVariableStep;
        private int traceExerciseQuestionIndex = -1;
        private int traceExerciseAnswerCount;
        private int traceExerciseCorrectCount;
        private bool awaitingTraceExerciseAnswer;
        private bool traceExerciseCompleted;
        private int debugExerciseQuestionIndex = -1;
        private int debugExerciseAnswerCount;
        private int debugExerciseCorrectCount;
        private bool awaitingDebugExerciseAnswer;
        private bool debugExerciseCompleted;
        private int finalAssessmentQuestionIndex = -1;
        private int finalAssessmentAnswerCount;
        private int finalAssessmentCorrectCount;
        private bool awaitingFinalAssessmentAnswer;
        private bool finalAssessmentCompleted;
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
                (payload.ActivityId != ActivityId && payload.ActivityId != DataTypesActivityId &&
                 payload.ActivityId != AddressLayoutActivityId && payload.ActivityId != MemoryAccessActivityId &&
                 payload.ActivityId != LoopMemoryActivityId && payload.ActivityId != ArraysActivityId &&
                 payload.ActivityId != FunctionsActivityId && payload.ActivityId != MemorySelectionActivityId &&
                 payload.ActivityId != MemoryScopeActivityId && payload.ActivityId != MemoryRemoveVariableActivityId &&
                 payload.ActivityId != MemoryTraceExerciseActivityId && payload.ActivityId != MemoryDebugExerciseActivityId &&
                 payload.ActivityId != MemoryFinalAssessmentActivityId))
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
            isAddressLayoutActivity = payload.ActivityId == AddressLayoutActivityId;
            isMemoryAccessActivity = payload.ActivityId == MemoryAccessActivityId;
            isLoopMemoryActivity = payload.ActivityId == LoopMemoryActivityId;
            isArraysActivity = payload.ActivityId == ArraysActivityId;
            isFunctionsActivity = payload.ActivityId == FunctionsActivityId;
            isMemorySelectionActivity = payload.ActivityId == MemorySelectionActivityId;
            isMemoryScopeActivity = payload.ActivityId == MemoryScopeActivityId;
            isMemoryRemoveVariableActivity = payload.ActivityId == MemoryRemoveVariableActivityId;
            isMemoryTraceExerciseActivity = payload.ActivityId == MemoryTraceExerciseActivityId;
            isMemoryDebugExerciseActivity = payload.ActivityId == MemoryDebugExerciseActivityId;
            isMemoryFinalAssessmentActivity = payload.ActivityId == MemoryFinalAssessmentActivityId;
            memoryAccessStep = 0;
            loopMemoryStep = 0;
            functionMemoryStep = 0;
            memorySelectionStep = 0;
            memoryScopeStep = 0;
            memoryRemoveVariableStep = 0;
            traceExerciseQuestionIndex = -1;
            traceExerciseAnswerCount = 0;
            traceExerciseCorrectCount = 0;
            awaitingTraceExerciseAnswer = false;
            traceExerciseCompleted = false;
            debugExerciseQuestionIndex = -1;
            debugExerciseAnswerCount = 0;
            debugExerciseCorrectCount = 0;
            awaitingDebugExerciseAnswer = false;
            debugExerciseCompleted = false;
            finalAssessmentQuestionIndex = -1;
            finalAssessmentAnswerCount = 0;
            finalAssessmentCorrectCount = 0;
            awaitingFinalAssessmentAnswer = false;
            finalAssessmentCompleted = false;
            string rootName = isDataTypesActivity ? "Memory Data Types 3D Content" :
                isAddressLayoutActivity ? "Memory Address Layout 3D Content" :
                isMemoryAccessActivity ? "Memory Access 3D Content" :
                isLoopMemoryActivity ? "Loop Memory 3D Content" :
                isArraysActivity ? "Memory Arrays 3D Content" :
                isFunctionsActivity ? "Memory Functions 3D Content" :
                isMemorySelectionActivity ? "Memory Selection 3D Content" :
                isMemoryScopeActivity ? "Memory Scope 3D Content" :
                isMemoryRemoveVariableActivity ? "Memory Remove Variable 3D Content" :
                isMemoryTraceExerciseActivity ? "Memory Trace Exercise 3D Content" :
                isMemoryDebugExerciseActivity ? "Memory Debug Exercise 3D Content" :
                isMemoryFinalAssessmentActivity ? "Memory Final Assessment 3D Content" : "Memory Variables 3D Content";
            topicRoot = new GameObject(rootName).transform;
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
            if (isMemoryAccessActivity)
            {
                switch (payload.Action)
                {
                    case "Access Step": StepMemoryAccess(); break;
                    case "Reset": ResetMemoryAccess(); break;
                    default:
                        SendEvent(payload.Action, value, "Failed", "Use Access Step to follow the memory access trace, or Reset to start again.");
                        break;
                }
                return;
            }

            if (isLoopMemoryActivity)
            {
                switch (payload.Action)
                {
                    case "Loop Step": StepLoopMemory(); break;
                    case "Reset": ResetLoopMemory(); break;
                    default:
                        SendEvent("Memory Loop", value, "Failed", "Use Loop Step to follow the memory loop trace, or Reset to start again.");
                        break;
                }
                return;
            }

            if (isArraysActivity)
            {
                switch (payload.Action)
                {
                    case "Set Array": SetArray(value); break;
                    case "Inspect": InspectArrayElement(value); break;
                    case "Reset": ResetArrays(); break;
                    default:
                        SendEvent("Memory Set Array", value, "Failed", "Enter an array declaration or element update, inspect an element, or Reset the Arrays activity.");
                        break;
                }
                return;
            }

            if (isFunctionsActivity)
            {
                switch (payload.Action)
                {
                    case "Function Call": StepMemoryFunctionCall(); break;
                    case "Reset": ResetMemoryFunctionCall(); break;
                    default:
                        SendEvent("Memory Function Call", value, "Failed", "Use Function Call to follow the add() memory trace, or Reset to start again.");
                        break;
                }
                return;
            }

            if (isMemorySelectionActivity)
            {
                switch (payload.Action)
                {
                    case "Run If-Else": StepMemorySelection(); break;
                    case "Reset": ResetMemorySelection(); break;
                    default:
                        SendEvent("Memory Selection", value, "Failed", "Use Run If-Else to follow the memory selection trace, or Reset to start again.");
                        break;
                }
                return;
            }

            if (isMemoryScopeActivity)
            {
                switch (payload.Action)
                {
                    case "Scope Step": StepMemoryScope(); break;
                    case "Reset": ResetMemoryScope(); break;
                    default:
                        SendEvent("Memory Scope", value, "Failed", "Use Scope Step to follow the block-scope trace, or Reset to start again.");
                        break;
                }
                return;
            }

            if (isMemoryRemoveVariableActivity)
            {
                switch (payload.Action)
                {
                    case "Remove Step": StepMemoryRemoveVariable(); break;
                    case "Reset": ResetMemoryRemoveVariable(); break;
                    default:
                        SendEvent("Memory Remove Variable", value, "Failed", "Use Remove Step to follow the variable-removal trace, or Reset to start again.");
                        break;
                }
                return;
            }

            if (isMemoryTraceExerciseActivity)
            {
                switch (payload.Action)
                {
                    case "Start Trace":
                    case "Next Question": StartMemoryTraceQuestion(); break;
                    case "Submit Answer": SubmitMemoryTraceAnswer(value); break;
                    case "Reset": ResetMemoryTraceExercise(); break;
                    default:
                        SendEvent("Memory Trace Exercise", value, "Failed", "Start a trace question, submit your answer, or Reset the exercise.");
                        break;
                }
                return;
            }

            if (isMemoryDebugExerciseActivity)
            {
                switch (payload.Action)
                {
                    case "Start Debug":
                    case "Next Question": StartMemoryDebugQuestion(); break;
                    case "Submit Answer": SubmitMemoryDebugAnswer(value); break;
                    case "Reset": ResetMemoryDebugExercise(); break;
                    default:
                        SendEvent("Memory Debug Exercise", value, "Failed", "Start a debug question, submit your answer, or Reset the exercise.");
                        break;
                }
                return;
            }

            if (isMemoryFinalAssessmentActivity)
            {
                switch (payload.Action)
                {
                    case "Start Assessment":
                    case "Next Question": StartMemoryFinalAssessmentQuestion(); break;
                    case "Submit Answer": SubmitMemoryFinalAssessmentAnswer(value); break;
                    case "Finish Assessment": FinishMemoryFinalAssessment(); break;
                    case "Reset": ResetMemoryFinalAssessment(); break;
                    default:
                        SendEvent("Memory Final Assessment", value, "Failed", "Start an assessment question, submit your answer, finish the assessment, or Reset.");
                        break;
                }
                return;
            }

            if (isDataTypesActivity || isAddressLayoutActivity)
            {
                switch (payload.Action)
                {
                    case "Show Types":
                        if (isDataTypesActivity) ShowDataTypes();
                        else SendEvent(payload.Action, value, "Failed", "Show Types is only available in the Data Types activity.");
                        break;
                    case "Show Layout":
                        if (isAddressLayoutActivity) ShowAddressLayout();
                        else SendEvent(payload.Action, value, "Failed", "Show Layout is only available in the Address Layout activity.");
                        break;
                    case "Reset": ResetMemoryDemo(); break;
                    default:
                        SendEvent(payload.Action, value, "Failed", isAddressLayoutActivity
                            ? "Use Show Layout to display the address layout example."
                            : "Use Show Types to display the common data-type examples.");
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

        private void ShowAddressLayout()
        {
            records.Clear();
            predictionIndex = 0;
            waitingForPrediction = false;
            records.Add(new VariableRecord { Address = "0x1000", Name = "age", Type = "int", Value = "20", Size = TypeSize("int") });
            records.Add(new VariableRecord { Address = "0x1004", Name = "price", Type = "float", Value = "15.5", Size = TypeSize("float") });
            records.Add(new VariableRecord { Address = "0x1008", Name = "grade", Type = "char", Value = "'A'", Size = TypeSize("char") });
            records.Add(new VariableRecord { Address = "0x1009", Name = "passed", Type = "bool", Value = "true", Size = TypeSize("bool") });
            records.Add(new VariableRecord { Address = "0x100A", Name = "level", Type = "int", Value = "3", Size = TypeSize("int") });
            Render(4, "ADDRESS\nLAYOUT");
            SendEvent("Show Layout", string.Empty, "Success",
                "Displayed how memory addresses advance by each data type's size. The level int row is highlighted with the ADDRESS LAYOUT marker.");
        }

        private void ResetMemoryDemo()
        {
            records.Clear();
            focusedRowIndex = -1;
            Render();
            string activityName = isAddressLayoutActivity ? "Address Layout" : "Data Types";
            string actionName = isAddressLayoutActivity ? "Show Layout" : "Show Types";
            SendEvent("Reset", string.Empty, "Success", activityName + " reset. Use " + actionName + " to display the example again.");
        }

        private void StepMemoryAccess()
        {
            memoryAccessStep++;
            switch (memoryAccessStep)
            {
                case 1:
                    AddMemoryAccessVariable("x", "4");
                    Render(records.Count - 1, "ALLOCATED\nnew variable");
                    StartCoroutine(CompleteAfterHighlight("Memory Access", "int x = 4;", "Success",
                        "Allocated new variable x with value 4."));
                    break;
                case 2:
                    AddMemoryAccessVariable("y", "6");
                    Render(records.Count - 1, "ALLOCATED\nnew variable");
                    StartCoroutine(CompleteAfterHighlight("Memory Access", "int y = 6;", "Success",
                        "Allocated new variable y with value 6."));
                    break;
                case 3:
                    FocusMemoryAccessRead("x", "4");
                    StartCoroutine(CompleteAfterHighlight("Memory Access", "read x", "Success",
                        "Read x from memory. Its stored value is 4."));
                    break;
                case 4:
                    FocusMemoryAccessRead("y", "6");
                    StartCoroutine(CompleteAfterHighlight("Memory Access", "read y", "Success",
                        "Read y from memory. Its stored value is 6."));
                    break;
                case 5:
                    AddMemoryAccessVariable("sum", "10");
                    Render(records.Count - 1, "ALLOCATED\nnew variable");
                    StartCoroutine(CompleteAfterHighlight("Memory Access", "int sum = x + y;", "Success",
                        "x and y were read from memory. Their values 4 and 6 were added, so sum stores 10."));
                    break;
                default:
                    focusedRowIndex = -1;
                    Render();
                    SendEvent("Memory Access", "", "Finished", "Memory access trace completed.");
                    break;
            }
        }

        private void AddMemoryAccessVariable(string name, string value)
        {
            records.Add(new VariableRecord
            {
                Address = AddressForIndex(records.Count),
                Name = name,
                Type = "int",
                Value = value,
                Size = TypeSize("int")
            });
        }

        private void FocusMemoryAccessRead(string name, string value)
        {
            int index = records.FindIndex(record => record.Name == name);
            Render(index, "READ\n" + name + " = " + value);
        }

        private void ResetMemoryAccess()
        {
            records.Clear();
            memoryAccessStep = 0;
            focusedRowIndex = -1;
            Render();
            SendEvent("Reset", string.Empty, "Success", "Memory Access reset. The table is empty and the trace is ready to start.");
        }

        private void StepLoopMemory()
        {
            loopMemoryStep++;
            switch (loopMemoryStep)
            {
                case 1:
                    AddMemoryLoopArray();
                    Render(0, "ARRAY\nALLOCATED");
                    StartCoroutine(CompleteAfterHighlight("Memory Loop",
                        "int numbers[3] = {2, 4, 6};", "Success",
                        "Loop setup completed. The loop values were placed in consecutive memory locations so the loop can read them one by one."));
                    break;
                case 2:
                {
                    SetMemoryLoopVariable("total", "0", out bool totalAllocated, out string totalPrevious);
                    Render(records.FindIndex(record => record.Name == "total"),
                        totalAllocated ? "ALLOCATED\nnew variable" : "UPDATED\n" + totalPrevious + " → 0");
                    StartCoroutine(CompleteAfterHighlight("Memory Loop", "int total = 0;", "Success",
                        "total was allocated with the starting value 0."));
                    break;
                }
                case 3:
                {
                    SetMemoryLoopVariable("i", "0", out bool indexAllocated, out string indexPrevious);
                    Render(records.FindIndex(record => record.Name == "i"),
                        indexAllocated ? "ALLOCATED\nnew variable" : "UPDATED\n" + indexPrevious + " → 0");
                    StartCoroutine(CompleteAfterHighlight("Memory Loop", "int i = 0;", "Success",
                        "Loop counter i was created with value 0."));
                    break;
                }
                case 4:
                    ReadMemoryLoopValue("numbers[0]", "2");
                    StartCoroutine(CompleteAfterHighlight("Memory Loop", "read numbers[0]", "Success",
                        "The loop reads numbers[0], which stores 2."));
                    break;
                case 5:
                {
                    SetMemoryLoopVariable("total", "2", out _, out string totalBeforeTwo);
                    Render(records.FindIndex(record => record.Name == "total"), "UPDATED\n" + totalBeforeTwo + " → 2");
                    StartCoroutine(CompleteAfterHighlight("Memory Loop", "total = total + numbers[0];", "Success",
                        "total was updated from 0 to 2."));
                    break;
                }
                case 6:
                {
                    SetMemoryLoopVariable("i", "1", out _, out string indexBeforeOne);
                    Render(records.FindIndex(record => record.Name == "i"), "UPDATED\n" + indexBeforeOne + " → 1");
                    StartCoroutine(CompleteAfterHighlight("Memory Loop", "i = i + 1;", "Success",
                        "Loop counter i was updated from 0 to 1."));
                    break;
                }
                case 7:
                    ReadMemoryLoopValue("numbers[1]", "4");
                    StartCoroutine(CompleteAfterHighlight("Memory Loop", "read numbers[1]", "Success",
                        "The loop reads numbers[1], which stores 4."));
                    break;
                case 8:
                {
                    SetMemoryLoopVariable("total", "6", out _, out string totalBeforeSix);
                    Render(records.FindIndex(record => record.Name == "total"), "UPDATED\n" + totalBeforeSix + " → 6");
                    StartCoroutine(CompleteAfterHighlight("Memory Loop", "total = total + numbers[1];", "Success",
                        "total was updated from 2 to 6."));
                    break;
                }
                case 9:
                {
                    SetMemoryLoopVariable("i", "2", out _, out string indexBeforeTwo);
                    Render(records.FindIndex(record => record.Name == "i"), "UPDATED\n" + indexBeforeTwo + " → 2");
                    StartCoroutine(CompleteAfterHighlight("Memory Loop", "i = i + 1;", "Success",
                        "Loop counter i was updated from 1 to 2."));
                    break;
                }
                case 10:
                    ReadMemoryLoopValue("numbers[2]", "6");
                    StartCoroutine(CompleteAfterHighlight("Memory Loop", "read numbers[2]", "Success",
                        "The loop reads numbers[2], which stores 6."));
                    break;
                case 11:
                {
                    SetMemoryLoopVariable("total", "12", out _, out string totalBeforeTwelve);
                    Render(records.FindIndex(record => record.Name == "total"), "UPDATED\n" + totalBeforeTwelve + " → 12");
                    StartCoroutine(CompleteAfterHighlight("Memory Loop", "total = total + numbers[2];", "Success",
                        "total was updated from 6 to 12."));
                    break;
                }
                case 12:
                    records.RemoveAll(record => record.Name == "i");
                    focusedRowIndex = -1;
                    Render(-1, "REMOVED\nloop counter");
                    SendEvent("Memory Loop", "loop ends", "Success",
                        "The loop ended. The temporary loop counter i was removed.");
                    break;
                case 13:
                {
                    int totalIndex = records.FindIndex(record => record.Name == "total");
                    Render(totalIndex, "LOOP\nCOMPLETE");
                    SendEvent("Memory Loop", string.Empty, "Finished",
                        "Loop memory trace completed");
                    break;
                }
                default:
                    SendEvent("Memory Loop", string.Empty, "Finished", "Loop memory trace completed.");
                    break;
            }
        }

        private void AddMemoryLoopArray()
        {
            string[] values = { "2", "4", "6" };
            for (int i = 0; i < values.Length; i++)
            {
                records.Add(new VariableRecord
                {
                    Address = "0x" + (4096 + records.Count * TypeByteCount("int")).ToString("X"),
                    Name = "numbers[" + i + "]",
                    Type = "int",
                    Value = values[i],
                    Size = TypeSize("int")
                });
            }
        }

        private void SetMemoryLoopVariable(string name, string value, out bool allocated, out string previous)
        {
            int index = records.FindIndex(record => record.Name == name);
            allocated = index < 0;
            previous = allocated ? string.Empty : records[index].Value;
            VariableRecord record = new VariableRecord
            {
                Address = allocated ? AddressForIndex(records.Count) : records[index].Address,
                Name = name,
                Type = "int",
                Value = value,
                Size = TypeSize("int")
            };
            if (allocated) records.Add(record);
            else records[index] = record;
        }

        private void ReadMemoryLoopValue(string name, string value)
        {
            int index = records.FindIndex(record => record.Name == name);
            Render(index, "READ\n" + name + " = " + value);
        }

        private void ResetLoopMemory()
        {
            records.Clear();
            loopMemoryStep = 0;
            focusedRowIndex = -1;
            Render();
            SendEvent("Reset", string.Empty, "Success", "Loop Memory reset. The table is empty and the trace is ready to start.");
        }

        private void SetArray(string input)
        {
            if (!TryParseArrayInput(input, out string arrayName, out int declaredLength,
                    out string type, out List<string> values, out bool isUpdate, out string error))
            {
                SendEvent("Memory Set Array", input, "Failed", error);
                return;
            }

            if (isUpdate)
            {
                string elementName = arrayName + "[" + declaredLength + "]";
                int elementIndex = records.FindIndex(record => record.Name == elementName);
                if (elementIndex < 0)
                {
                    SendEvent("Memory Set Array", input, "Failed", "No array element named " + elementName + " was found.");
                    return;
                }

                VariableRecord existing = records[elementIndex];
                string newValue = values[0];
                if (!IsCompatible(existing.Type, newValue))
                {
                    SendEvent("Memory Set Array", input, "Failed", TypeError(existing.Type, elementName));
                    return;
                }

                string previousValue = existing.Value;
                existing.Value = newValue;
                Render(elementIndex, "UPDATED\n" + previousValue + " → " + newValue);
                StartCoroutine(CompleteAfterHighlight("Memory Set Array", input, "Success",
                    "Updated " + elementName + " from " + previousValue + " to " + newValue + "."));
                return;
            }

            records.RemoveAll(record => record.Name.StartsWith(arrayName + "[", StringComparison.Ordinal));
            int baseOffset = 0;
            for (int i = 0; i < records.Count; i++) baseOffset += TypeByteCount(records[i].Type);
            int firstIndex = records.Count;
            int typeBytes = TypeByteCount(type);
            for (int i = 0; i < values.Count; i++)
            {
                records.Add(new VariableRecord
                {
                    Address = "0x" + (4096 + baseOffset + i * typeBytes).ToString("X"),
                    Name = arrayName + "[" + i + "]",
                    Type = type,
                    Value = values[i],
                    Size = TypeSize(type)
                });
            }

            Render(firstIndex, "ARRAY\nALLOCATED");
            StartCoroutine(CompleteAfterHighlight("Memory Set Array", input, "Success",
                "Allocated array " + arrayName + " with " + values.Count + " " + type + " element(s) in consecutive memory locations."));
        }

        private void InspectArrayElement(string input)
        {
            string elementName = (input ?? string.Empty).Trim();
            int index = records.FindIndex(record => record.Name == elementName && record.Name.Contains("["));
            if (index < 0)
            {
                SendEvent("Memory Inspect", elementName, "Failed", "No array element named " + elementName + " was found.");
                return;
            }

            VariableRecord record = records[index];
            Render(index, "INSPECTED");
            StartCoroutine(CompleteAfterHighlight("Memory Inspect", elementName, "Success",
                "Inspect: " + record.Name + " is a " + record.Type + " array element. It stores " + record.Value +
                " at address " + record.Address + " and uses " + record.Size + "."));
        }

        private bool TryParseArrayInput(string input, out string arrayName, out int bracketNumber,
            out string type, out List<string> values, out bool isUpdate, out string error)
        {
            arrayName = string.Empty;
            bracketNumber = 0;
            type = string.Empty;
            values = new List<string>();
            isUpdate = false;
            error = string.Empty;

            string cleaned = (input ?? string.Empty).Trim();
            int equals = cleaned.IndexOf('=');
            if (equals < 0 || equals != cleaned.LastIndexOf('='))
            {
                error = "Use an array declaration such as int marks[3]=70,80,65 or an element update such as marks[2]=85.";
                return false;
            }

            string left = cleaned.Substring(0, equals).Trim();
            string right = cleaned.Substring(equals + 1).Trim().Replace("{", string.Empty).Replace("}", string.Empty);
            string[] knownTypes = { "int", "float", "double", "char", "bool", "string" };
            for (int i = 0; i < knownTypes.Length; i++)
            {
                string prefix = knownTypes[i] + " ";
                if (!left.StartsWith(prefix, StringComparison.Ordinal)) continue;
                type = knownTypes[i];
                left = left.Substring(prefix.Length).Trim();
                break;
            }

            int open = left.IndexOf('[');
            int close = left.IndexOf(']');
            if (open <= 0 || close != left.Length - 1 || close <= open ||
                left.IndexOf('[', open + 1) >= 0 || left.IndexOf(']', close + 1) >= 0)
            {
                error = "Use an array name with a bracketed length or index, such as marks[3].";
                return false;
            }

            arrayName = left.Substring(0, open).Trim();
            string bracketText = left.Substring(open + 1, close - open - 1).Trim();
            if (!NamePattern.IsMatch(arrayName) || !int.TryParse(bracketText, NumberStyles.None,
                    CultureInfo.InvariantCulture, out bracketNumber) || bracketNumber < 0)
            {
                error = "Use a valid array name and a whole-number length or index, such as marks[3].";
                return false;
            }
            if (right.Length == 0)
            {
                error = "Enter at least one array value.";
                return false;
            }

            isUpdate = type.Length == 0 && !right.Contains(",");
            string[] rawValues = right.Split(',');
            for (int i = 0; i < rawValues.Length; i++)
            {
                string item = rawValues[i].Trim();
                if (item.Length == 0)
                {
                    error = "Remove empty entries and enter one value for each array element.";
                    return false;
                }
                values.Add(item);
            }

            if (isUpdate)
            {
                if (values.Count != 1)
                {
                    error = "Enter one value when updating an array element.";
                    return false;
                }
                return true;
            }

            if (bracketNumber != values.Count)
            {
                error = "The declared array length is " + bracketNumber + ", but " + values.Count +
                    " value(s) were entered. Enter exactly " + bracketNumber + " value(s), or change the declared length.";
                return false;
            }
            if (values.Count > 6)
            {
                error = "Arrays can contain at most 6 values in this activity.";
                return false;
            }
            if (type.Length == 0) type = InferType(values[0]);
            if (type == null)
            {
                error = "Could not identify the array element type. Add a type such as int or string.";
                return false;
            }
            for (int i = 0; i < values.Count; i++)
            {
                if (!IsCompatible(type, values[i]))
                {
                    error = TypeError(type, arrayName + "[" + i + "]");
                    return false;
                }
            }
            return true;
        }

        private void ResetArrays()
        {
            records.Clear();
            focusedRowIndex = -1;
            Render();
            SendEvent("Reset", string.Empty, "Success", "Arrays reset. The table is empty and ready for a new array.");
        }

        private void StepMemoryFunctionCall()
        {
            functionMemoryStep++;
            switch (functionMemoryStep)
            {
                case 1:
                    AddMemoryFunctionVariable("x", "10");
                    Render(records.Count - 1, "ALLOCATED\nnew variable");
                    StartCoroutine(CompleteAfterHighlight("Memory Function Call", "int x = 10;", "Success",
                        "Allocated new variable x with value 10."));
                    break;
                case 2:
                    AddMemoryFunctionVariable("y", "20");
                    Render(records.Count - 1, "ALLOCATED\nnew variable");
                    StartCoroutine(CompleteAfterHighlight("Memory Function Call", "int y = 20;", "Success",
                        "Allocated new variable y with value 20."));
                    break;
                case 3:
                    Render();
                    SendEvent("Memory Function Call", "result = add(x, y);", "Success",
                        "Function call add(x, y) started. A new function frame is created.");
                    break;
                case 4:
                    AddMemoryFunctionFrameVariable("a:param", "10");
                    Render(records.Count - 1, "FUNCTION\nFRAME");
                    StartCoroutine(CompleteAfterHighlight("Memory Function Call", "int add(int a, int b)", "Success",
                        "Created param variable a inside add() with value 10."));
                    break;
                case 5:
                    AddMemoryFunctionFrameVariable("b:param", "20");
                    Render(records.Count - 1, "FUNCTION\nFRAME");
                    StartCoroutine(CompleteAfterHighlight("Memory Function Call", "int add(int a, int b)", "Success",
                        "Created param variable b inside add() with value 20."));
                    break;
                case 6:
                    AddMemoryFunctionFrameVariable("sum:local", "30");
                    Render(records.Count - 1, "FUNCTION\nFRAME");
                    StartCoroutine(CompleteAfterHighlight("Memory Function Call", "int sum = a + b;", "Success",
                        "Created local variable sum inside add() with value 30."));
                    break;
                case 7:
                {
                    int sumIndex = records.FindIndex(record => record.Name == "sum:local");
                    Render(sumIndex, "RETURN\n30");
                    StartCoroutine(CompleteAfterHighlight("Memory Function Call", "return sum;", "Success",
                        "The function returns the value 30."));
                    break;
                }
                case 8:
                    records.RemoveAll(record => record.Name == "a:param" || record.Name == "b:param" || record.Name == "sum:local");
                    AddMemoryFunctionVariable("result", "30");
                    Render(records.Count - 1, "RETURN\nSTORED");
                    StartCoroutine(CompleteAfterHighlight("Memory Function Call", "int result = 30;", "Success",
                        "Return value 30 was stored in result. The add() frame was removed."));
                    break;
                case 9:
                    focusedRowIndex = -1;
                    Render();
                    SendEvent("Memory Function Call", string.Empty, "Finished", "Function call memory trace completed");
                    break;
                default:
                    SendEvent("Memory Function Call", string.Empty, "Finished", "Function call memory trace completed");
                    break;
            }
        }

        private void AddMemoryFunctionVariable(string name, string value)
        {
            AddMemoryTypedVariable(name, "int", value);
        }

        private void AddMemoryTypedVariable(string name, string type, string value)
        {
            records.Add(new VariableRecord
            {
                Address = AddressForIndex(records.Count),
                Name = name,
                Type = type,
                Value = value,
                Size = TypeSize(type)
            });
        }

        private void AddMemoryFunctionFrameVariable(string name, string value)
        {
            int byteOffset = 0;
            for (int i = 0; i < records.Count; i++) byteOffset += TypeByteCount(records[i].Type);
            records.Add(new VariableRecord
            {
                Address = "0x" + (4096 + byteOffset).ToString("X"),
                Name = name,
                Type = "int",
                Value = value,
                Size = TypeSize("int")
            });
        }

        private void ResetMemoryFunctionCall()
        {
            records.Clear();
            functionMemoryStep = 0;
            focusedRowIndex = -1;
            Render();
            SendEvent("Reset", string.Empty, "Success", "Memory Function Call reset. The table is empty and the trace is ready to start.");
        }

        private void StepMemorySelection()
        {
            memorySelectionStep++;
            switch (memorySelectionStep)
            {
                case 1:
                    AddMemoryFunctionVariable("score", "75");
                    Render(records.Count - 1, "ALLOCATED\nnew variable");
                    StartCoroutine(CompleteAfterHighlight("Memory Selection", "int score = 75;", "Success",
                        "Allocated new variable score with value 75."));
                    break;
                case 2:
                    Render(-1, "CONDITION\nTRUE");
                    SendEvent("Memory Selection", "if (score >= 50)", "Success",
                        "The condition score >= 50 evaluated TRUE because score stores 75.");
                    break;
                case 3:
                    AddMemoryFunctionVariable("result", "1");
                    Render(records.Count - 1, "ALLOCATED\nnew variable");
                    StartCoroutine(CompleteAfterHighlight("Memory Selection", "result = 1;", "Success",
                        "The TRUE branch executed. result was stored as 1 in memory."));
                    break;
                case 4:
                    Render(-1, "BRANCH\nSKIPPED");
                    SendEvent("Memory Selection", "else branch skipped", "Success",
                        "The ELSE branch was skipped because the condition was TRUE.");
                    break;
                case 5:
                    focusedRowIndex = -1;
                    Render();
                    SendEvent("Memory Selection", string.Empty, "Finished", "Selection memory trace completed");
                    break;
                default:
                    SendEvent("Memory Selection", string.Empty, "Finished", "Selection memory trace completed");
                    break;
            }
        }

        private void ResetMemorySelection()
        {
            records.Clear();
            memorySelectionStep = 0;
            focusedRowIndex = -1;
            Render();
            SendEvent("Reset", string.Empty, "Success", "Memory Selection reset. The table is empty and the trace is ready to start.");
        }

        private void StepMemoryScope()
        {
            memoryScopeStep++;
            switch (memoryScopeStep)
            {
                case 1:
                    AddMemoryFunctionVariable("x", "10");
                    Render(records.Count - 1, "ALLOCATED\nouter scope");
                    StartCoroutine(CompleteAfterHighlight("Memory Scope", "int x = 10;", "Success",
                        "Allocated x = 10 outside the if block. It remains in scope after the block ends."));
                    break;
                case 2:
                    Render(-1, "CONDITION\nTRUE");
                    SendEvent("Memory Scope", "if (x > 5)", "Success",
                        "The condition x > 5 evaluated TRUE because x stores 10.");
                    break;
                case 3:
                    AddMemoryFunctionVariable("y", "20");
                    Render(-1, "LOCAL\nif block");
                    StartCoroutine(CompleteAfterHighlight("Memory Scope", "int y = 20;", "Success",
                        "Allocated y = 20 inside the if block. y is local to that block."));
                    break;
                case 4:
                {
                    int xIndex = records.FindIndex(record => record.Name == "x");
                    if (xIndex < 0)
                    {
                        SendEvent("Memory Scope", "x = x + y;", "Failed", "The outer-scope variable x is missing. Reset the trace and try again.");
                        break;
                    }
                    records[xIndex].Value = "30";
                    Render(xIndex, "UPDATED\n10 → 30");
                    StartCoroutine(CompleteAfterHighlight("Memory Scope", "x = x + y;", "Success",
                        "Updated x from 10 to 30 using the block-local variable y = 20."));
                    break;
                }
                case 5:
                    records.RemoveAll(record => record.Name == "y");
                    Render(-1, "REMOVED\ny out of scope");
                    SendEvent("Memory Scope", "end of if block", "Success",
                        "The if block ended, so its local variable y was removed from memory.");
                    break;
                case 6:
                {
                    int xIndex = records.FindIndex(record => record.Name == "x");
                    Render(xIndex, "SCOPE\nREMAINS");
                    SendEvent("Memory Scope", "x remains in memory", "Success",
                        "x remains in memory with value 30 because it was declared outside the if block.");
                    break;
                }
                case 7:
                    focusedRowIndex = -1;
                    Render();
                    SendEvent("Memory Scope", string.Empty, "Finished", "Scope memory trace completed");
                    break;
                default:
                    SendEvent("Memory Scope", string.Empty, "Finished", "Scope memory trace completed");
                    break;
            }
        }

        private void ResetMemoryScope()
        {
            records.Clear();
            memoryScopeStep = 0;
            focusedRowIndex = -1;
            Render();
            SendEvent("Reset", string.Empty, "Success", "Memory Scope reset. The table is empty and the trace is ready to start.");
        }

        private void StepMemoryRemoveVariable()
        {
            memoryRemoveVariableStep++;
            switch (memoryRemoveVariableStep)
            {
                case 1:
                    AddMemoryTypedVariable("age", "int", "20");
                    Render(records.Count - 1, "ALLOCATED\nnew variable");
                    StartCoroutine(CompleteAfterHighlight("Memory Remove Variable", "int age = 20;", "Success",
                        "Allocated age with value 20."));
                    break;
                case 2:
                    AddMemoryTypedVariable("price", "float", "15.5");
                    Render(records.Count - 1, "ALLOCATED\nnew variable");
                    StartCoroutine(CompleteAfterHighlight("Memory Remove Variable", "float price = 15.5;", "Success",
                        "Allocated price with value 15.5."));
                    break;
                case 3:
                    records.RemoveAll(record => record.Name == "age");
                    Render(-1, "REMOVED\nage = 20");
                    SendEvent("Memory Remove Variable", "remove age;", "Success",
                        "Removed age from memory. The separate price variable remains allocated.");
                    break;
                case 4:
                {
                    int priceIndex = records.FindIndex(record => record.Name == "price");
                    Render(priceIndex, "REMAINS\nafter removal");
                    SendEvent("Memory Remove Variable", "price remains in memory", "Success",
                        "price remains in memory with value 15.5 because only age was removed.");
                    break;
                }
                case 5:
                    focusedRowIndex = -1;
                    Render();
                    SendEvent("Memory Remove Variable", string.Empty, "Finished", "Remove Variable trace completed");
                    break;
                default:
                    SendEvent("Memory Remove Variable", string.Empty, "Finished", "Remove Variable trace completed");
                    break;
            }
        }

        private void ResetMemoryRemoveVariable()
        {
            records.Clear();
            memoryRemoveVariableStep = 0;
            focusedRowIndex = -1;
            Render();
            SendEvent("Reset", string.Empty, "Success", "Remove Variable reset. The table is empty and the trace is ready to start.");
        }

        private void StartMemoryTraceQuestion()
        {
            if (traceExerciseCompleted)
            {
                SendEvent("Memory Trace Exercise", string.Empty, "Finished",
                    "Trace Exercise is complete. Reset to start again.");
                return;
            }

            if (awaitingTraceExerciseAnswer)
            {
                SendEvent("Memory Trace Exercise", string.Empty, "Failed",
                    "Submit your answer to the current trace question before moving on.");
                return;
            }

            int nextIndex = traceExerciseQuestionIndex + 1;
            if (nextIndex >= MemoryExerciseQuestions.Length)
            {
                SendEvent("Memory Trace Exercise", string.Empty, "Finished",
                    "Trace Exercise is complete. Reset to start again.");
                return;
            }

            traceExerciseQuestionIndex = nextIndex;
            awaitingTraceExerciseAnswer = true;
            records.Clear();
            focusedRowIndex = -1;
            Render();
            MemoryExerciseQuestion question = MemoryExerciseQuestions[traceExerciseQuestionIndex];
            SendEvent("Memory Trace Exercise", question.Code, "Started",
                "Question " + (traceExerciseQuestionIndex + 1) + "/" + MemoryExerciseQuestions.Length + ": " + question.Question);
        }

        private void SubmitMemoryTraceAnswer(string answer)
        {
            if (traceExerciseCompleted)
            {
                SendEvent("Memory Trace Exercise", answer, "Finished",
                    "Trace Exercise is complete. Reset to start again.");
                return;
            }

            if (!awaitingTraceExerciseAnswer || traceExerciseQuestionIndex < 0 ||
                traceExerciseQuestionIndex >= MemoryExerciseQuestions.Length)
            {
                SendEvent("Memory Trace Exercise", answer, "Failed",
                    "Start a trace question before submitting an answer.");
                return;
            }

            if (string.IsNullOrWhiteSpace(answer))
            {
                SendEvent("Memory Trace Exercise", answer, "Failed",
                    "Enter your answer before submitting it.");
                return;
            }

            MemoryExerciseQuestion question = MemoryExerciseQuestions[traceExerciseQuestionIndex];
            bool correct = NormalizePrediction(answer) == NormalizePrediction(question.Answer);
            if (correct) traceExerciseCorrectCount++;
            traceExerciseAnswerCount++;
            awaitingTraceExerciseAnswer = false;

            records.Clear();
            for (int i = 0; i < question.FinalVariables.Length; i++)
            {
                MemoryExerciseVariable item = question.FinalVariables[i];
                records.Add(new VariableRecord
                {
                    Address = AddressForIndex(records.Count),
                    Name = item.Name,
                    Type = item.Type,
                    Value = item.Value,
                    Size = TypeSize(item.Type)
                });
            }

            int focusIndex = records.FindIndex(record => record.Name == question.Focus);
            Render(focusIndex, "TRACE\nRESULT");
            bool isFinalAnswer = traceExerciseAnswerCount >= MemoryExerciseQuestions.Length;
            if (isFinalAnswer) traceExerciseCompleted = true;

            string details = (correct ? "Correct. " : "Not correct. Expected answer: " + question.Answer + ". ") +
                question.Explanation;
            if (isFinalAnswer)
            {
                details += " Question " + traceExerciseAnswerCount + "/" + MemoryExerciseQuestions.Length +
                    ". Trace Exercise completed. Score: " + traceExerciseCorrectCount + "/" + MemoryExerciseQuestions.Length + ".";
            }

            StartCoroutine(CompleteAfterHighlight("Memory Trace Exercise", answer,
                correct ? "Correct" : "Incorrect", details));
        }

        private void ResetMemoryTraceExercise()
        {
            records.Clear();
            traceExerciseQuestionIndex = -1;
            traceExerciseAnswerCount = 0;
            traceExerciseCorrectCount = 0;
            awaitingTraceExerciseAnswer = false;
            traceExerciseCompleted = false;
            focusedRowIndex = -1;
            Render();
            SendEvent("Reset", string.Empty, "Success",
                "Memory Trace Exercise reset. The table is empty and the first question is ready.");
        }

        private void StartMemoryDebugQuestion()
        {
            if (debugExerciseCompleted)
            {
                SendEvent("Memory Debug Exercise", string.Empty, "Finished",
                    "Debug Exercise is complete. Reset to start again.");
                return;
            }

            if (awaitingDebugExerciseAnswer)
            {
                SendEvent("Memory Debug Exercise", string.Empty, "Failed",
                    "Submit your answer to the current debug question before moving on.");
                return;
            }

            int nextIndex = debugExerciseQuestionIndex + 1;
            if (nextIndex >= DebugExerciseQuestions.Length)
            {
                SendEvent("Memory Debug Exercise", string.Empty, "Finished",
                    "Debug Exercise is complete. Reset to start again.");
                return;
            }

            debugExerciseQuestionIndex = nextIndex;
            awaitingDebugExerciseAnswer = true;
            records.Clear();
            focusedRowIndex = -1;
            Render();
            MemoryExerciseQuestion question = DebugExerciseQuestions[debugExerciseQuestionIndex];
            SendEvent("Memory Debug Exercise", question.Code, "Started",
                "Question " + (debugExerciseQuestionIndex + 1) + "/" + DebugExerciseQuestions.Length + ": " + question.Question);
        }

        private void SubmitMemoryDebugAnswer(string answer)
        {
            if (debugExerciseCompleted)
            {
                SendEvent("Memory Debug Exercise", answer, "Finished",
                    "Debug Exercise is complete. Reset to start again.");
                return;
            }

            if (!awaitingDebugExerciseAnswer || debugExerciseQuestionIndex < 0 ||
                debugExerciseQuestionIndex >= DebugExerciseQuestions.Length)
            {
                SendEvent("Memory Debug Exercise", answer, "Failed",
                    "Start a debug question before submitting an answer.");
                return;
            }

            if (string.IsNullOrWhiteSpace(answer))
            {
                SendEvent("Memory Debug Exercise", answer, "Failed",
                    "Enter the line number before submitting your answer.");
                return;
            }

            MemoryExerciseQuestion question = DebugExerciseQuestions[debugExerciseQuestionIndex];
            bool correct = NormalizePrediction(answer) == NormalizePrediction(question.Answer);
            if (correct) debugExerciseCorrectCount++;
            debugExerciseAnswerCount++;
            awaitingDebugExerciseAnswer = false;

            records.Clear();
            for (int i = 0; i < question.FinalVariables.Length; i++)
            {
                MemoryExerciseVariable item = question.FinalVariables[i];
                records.Add(new VariableRecord
                {
                    Address = AddressForIndex(records.Count),
                    Name = item.Name,
                    Type = item.Type,
                    Value = item.Value,
                    Size = TypeSize(item.Type)
                });
            }

            int focusIndex = records.FindIndex(record => record.Name == question.Focus);
            Render(focusIndex, "DEBUG\nRESULT");
            bool isFinalAnswer = debugExerciseAnswerCount >= DebugExerciseQuestions.Length;
            if (isFinalAnswer) debugExerciseCompleted = true;

            string details = (correct ? "Correct. " : "Not correct. Expected answer: " + question.Answer + ". ") +
                question.Explanation;
            if (isFinalAnswer)
            {
                details += " Question " + debugExerciseAnswerCount + "/" + DebugExerciseQuestions.Length +
                    ". Debug Exercise completed. Score: " + debugExerciseCorrectCount + "/" + DebugExerciseQuestions.Length + ".";
            }

            StartCoroutine(CompleteAfterHighlight("Memory Debug Exercise", answer,
                correct ? "Correct" : "Incorrect", details));
        }

        private void ResetMemoryDebugExercise()
        {
            records.Clear();
            debugExerciseQuestionIndex = -1;
            debugExerciseAnswerCount = 0;
            debugExerciseCorrectCount = 0;
            awaitingDebugExerciseAnswer = false;
            debugExerciseCompleted = false;
            focusedRowIndex = -1;
            Render();
            SendEvent("Reset", string.Empty, "Success",
                "Memory Debug Exercise reset. The table is empty and the first question is ready.");
        }

        private void StartMemoryFinalAssessmentQuestion()
        {
            if (finalAssessmentCompleted)
            {
                SendEvent("Memory Final Assessment", string.Empty, "Finished",
                    "Final Assessment is complete. Reset to start again.");
                return;
            }

            if (awaitingFinalAssessmentAnswer)
            {
                SendEvent("Memory Final Assessment", string.Empty, "Failed",
                    "Submit your answer to the current assessment question before moving on.");
                return;
            }

            int nextIndex = finalAssessmentQuestionIndex + 1;
            if (nextIndex >= FinalAssessmentQuestions.Length)
            {
                SendEvent("Memory Final Assessment", string.Empty, "Failed",
                    "All five questions have been answered. Choose Finish Assessment to see your score.");
                return;
            }

            finalAssessmentQuestionIndex = nextIndex;
            awaitingFinalAssessmentAnswer = true;
            records.Clear();
            focusedRowIndex = -1;
            Render();
            MemoryExerciseQuestion question = FinalAssessmentQuestions[finalAssessmentQuestionIndex];
            SendEvent("Memory Final Assessment", question.Code, "Started",
                "Question " + (finalAssessmentQuestionIndex + 1) + "/" + FinalAssessmentQuestions.Length + ": " + question.Question);
        }

        private void SubmitMemoryFinalAssessmentAnswer(string answer)
        {
            if (finalAssessmentCompleted)
            {
                SendEvent("Memory Final Assessment", answer, "Finished",
                    "Final Assessment is complete. Reset to start again.");
                return;
            }

            if (!awaitingFinalAssessmentAnswer || finalAssessmentQuestionIndex < 0 ||
                finalAssessmentQuestionIndex >= FinalAssessmentQuestions.Length)
            {
                SendEvent("Memory Final Assessment", answer, "Failed",
                    "Start an assessment question before submitting an answer.");
                return;
            }

            if (string.IsNullOrWhiteSpace(answer))
            {
                SendEvent("Memory Final Assessment", answer, "Failed",
                    "Enter your answer before submitting it.");
                return;
            }

            MemoryExerciseQuestion question = FinalAssessmentQuestions[finalAssessmentQuestionIndex];
            bool correct = NormalizePrediction(answer) == NormalizePrediction(question.Answer);
            if (correct) finalAssessmentCorrectCount++;
            finalAssessmentAnswerCount++;
            awaitingFinalAssessmentAnswer = false;

            records.Clear();
            for (int i = 0; i < question.FinalVariables.Length; i++)
            {
                MemoryExerciseVariable item = question.FinalVariables[i];
                records.Add(new VariableRecord
                {
                    Address = AddressForIndex(records.Count),
                    Name = item.Name,
                    Type = item.Type,
                    Value = item.Value,
                    Size = TypeSize(item.Type)
                });
            }

            int focusIndex = records.FindIndex(record => record.Name == question.Focus);
            Render(focusIndex, "FINAL\nRESULT");

            string details = (correct ? "Correct. " : "Not correct. Expected answer: " + question.Answer + ". ") +
                question.Explanation + " Question " + finalAssessmentAnswerCount + "/" + FinalAssessmentQuestions.Length + ".";
            StartCoroutine(CompleteAfterHighlight("Memory Final Assessment", answer,
                correct ? "Correct" : "Incorrect", details));
        }

        private void FinishMemoryFinalAssessment()
        {
            if (awaitingFinalAssessmentAnswer)
            {
                SendEvent("Memory Final Assessment", string.Empty, "Failed",
                    "Submit your answer to the current question before finishing the assessment.");
                return;
            }

            if (finalAssessmentAnswerCount < FinalAssessmentQuestions.Length)
            {
                SendEvent("Memory Final Assessment", string.Empty, "Failed",
                    "Answer all five questions before finishing the assessment.");
                return;
            }

            finalAssessmentCompleted = true;
            SendEvent("Memory Final Assessment", string.Empty, "Finished",
                "Final Assessment completed. Score: " + finalAssessmentCorrectCount + " out of " + FinalAssessmentQuestions.Length + ".");
        }

        private void ResetMemoryFinalAssessment()
        {
            records.Clear();
            finalAssessmentQuestionIndex = -1;
            finalAssessmentAnswerCount = 0;
            finalAssessmentCorrectCount = 0;
            awaitingFinalAssessmentAnswer = false;
            finalAssessmentCompleted = false;
            focusedRowIndex = -1;
            Render();
            SendEvent("Reset", string.Empty, "Success",
                "Memory Final Assessment reset. The table is empty and the first question is ready.");
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
            needsFocusMarkerGap = focusIndex >= 0 && focusIndex < records.Count - 1 && marker.Length > 0;
            hasGlobalMarker = focusIndex < 0 && marker.Length > 0 && records.Count > 0;
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
                    if (needsFocusMarkerGap && i > focusIndex) y -= RowStep * 1.2f;
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
                if (hasGlobalMarker)
                    CreateLabel(marker, new Vector3(0f, -0.012f - records.Count * RowStep, -0.014f),
                        26f, FocusMarkerColor, 0.56f, FontStyles.Bold);
            }

            UpdateViewportFit();
        }

        private void UpdateViewportFit()
        {
            Camera camera = Camera.main;
            if (camera == null || topicRoot == null) return;

            // Keep every Memory table at a consistent middle angle between the
            // placement's upright board and a fully camera-facing board. This
            // avoids the foreshortening of the other memory tables while keeping
            // Data Types and Address Layout from appearing square-on.
            Transform placementRoot = LociARController.Instance != null
                ? LociARController.Instance.ContentRoot
                : topicRoot.parent;
            if (placementRoot != null)
            {
                Quaternion middleAngle = Quaternion.Slerp(
                    placementRoot.rotation, camera.transform.rotation, 0.5f);
                float rotationBlend = 1f - Mathf.Exp(-8f * Time.deltaTime);
                topicRoot.rotation = Quaternion.Slerp(topicRoot.rotation, middleAngle, rotationBlend);
            }

            float depth = camera.WorldToViewportPoint(topicRoot.position).z;
            if (depth <= 0f) return;
            Vector3 left = camera.ViewportToWorldPoint(new Vector3(horizontalViewportMargin, 0.5f, depth));
            Vector3 right = camera.ViewportToWorldPoint(new Vector3(1f - horizontalViewportMargin, 0.5f, depth));
            float availableWidth = Vector3.Distance(left, right);
            Vector3 bottom = camera.ViewportToWorldPoint(new Vector3(0.5f, horizontalViewportMargin, depth));
            Vector3 top = camera.ViewportToWorldPoint(new Vector3(0.5f, 1f - horizontalViewportMargin, depth));
            float availableHeight = Vector3.Distance(bottom, top);
            bool landscape = Screen.width > Screen.height;
            float scaleCap = landscape
                ? (isDataTypesActivity || isAddressLayoutActivity ? DataTypesLandscapeScaleMultiplier : LandscapeScaleMultiplier)
                : PortraitScaleMultiplier;
            float contentHeight = 0.24f + Mathf.Max(1, records.Count) * RowStep +
                (needsFocusMarkerGap || hasGlobalMarker ? RowStep * 1.2f : 0f);
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
