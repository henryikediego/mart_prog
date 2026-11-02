using System;

namespace Loci.Topics.Selection
{
    /// <summary>Basic If inputs and evaluation state, independent of Unity objects and AR.</summary>
    public sealed class SelectionState
    {
        public const int DefaultValue = 75;
        public const int DefaultThreshold = 50;

        private static readonly string[] BasicRules = { ">= 50", "< 50", "== 0", "!= 0" };
        private int ruleIndex;
        private string conditionOperator;
        private int conditionThreshold;

        public int Value { get; private set; }
        public string ConditionText => conditionOperator + " " + conditionThreshold;
        public string ResultText { get; private set; }

        public SelectionState() => Reset();

        public void SetValue(int value)
        {
            Value = value;
            ResultText = "?";
        }

        public bool TrySetCondition(string input, out string normalized, out string error)
        {
            normalized = string.Empty;
            error = string.Empty;
            string text = (input ?? string.Empty).Trim();
            if (text.Length == 0)
            {
                error = "Enter a condition such as >= 50, < 40, == 0, or != 10.";
                return false;
            }

            if (text.StartsWith("value", StringComparison.OrdinalIgnoreCase))
                text = text.Substring(5).Trim();

            string[] operators = { ">=", "<=", "==", "!=", ">", "<" };
            foreach (string candidate in operators)
            {
                if (!text.StartsWith(candidate, StringComparison.Ordinal)) continue;
                string numberText = text.Substring(candidate.Length).Trim();
                if (!int.TryParse(numberText, out int parsedThreshold)) continue;

                conditionOperator = candidate;
                conditionThreshold = parsedThreshold;
                normalized = candidate + " " + numberText;
                ResultText = "?";
                return true;
            }

            error = "Enter an operator followed by a whole number, such as >= 50, < 40, == 0, or != 10.";
            return false;
        }

        public string ToggleRule()
        {
            ruleIndex = (ruleIndex + 1) % BasicRules.Length;
            TrySetCondition(BasicRules[ruleIndex], out string normalized, out _);
            return normalized;
        }

        public bool Evaluate()
        {
            bool result;
            switch (conditionOperator)
            {
                case ">=": result = Value >= conditionThreshold; break;
                case "<=": result = Value <= conditionThreshold; break;
                case ">": result = Value > conditionThreshold; break;
                case "<": result = Value < conditionThreshold; break;
                case "==": result = Value == conditionThreshold; break;
                case "!=": result = Value != conditionThreshold; break;
                default: result = false; break;
            }

            ResultText = result ? "TRUE" : "FALSE";
            return result;
        }

        public void Reset()
        {
            Value = DefaultValue;
            ruleIndex = 0;
            conditionOperator = ">=";
            conditionThreshold = DefaultThreshold;
            ResultText = "?";
        }
    }
}
