namespace Loci.Topics.Selection
{
    /// <summary>Preset nested-if rules, inputs, and short-circuit evaluation.</summary>
    public sealed class NestedIfState
    {
        private sealed class Rule
        {
            public readonly string Name;
            public readonly string FirstName;
            public readonly string SecondName;
            public readonly string OuterCondition;
            public readonly string InnerCondition;
            public readonly int OuterThreshold;
            public readonly int InnerThreshold;
            public readonly string TrueResult;
            public readonly string InnerFalseResult;
            public readonly string OuterFalseResult;

            public Rule(string name, string firstName, string secondName,
                string outerCondition, string innerCondition, int outerThreshold, int innerThreshold,
                string trueResult, string innerFalseResult, string outerFalseResult)
            {
                Name = name;
                FirstName = firstName;
                SecondName = secondName;
                OuterCondition = outerCondition;
                InnerCondition = innerCondition;
                OuterThreshold = outerThreshold;
                InnerThreshold = innerThreshold;
                TrueResult = trueResult;
                InnerFalseResult = innerFalseResult;
                OuterFalseResult = outerFalseResult;
            }
        }

        private static readonly Rule[] Rules =
        {
            new Rule("Pass Rule", "score", "attendance", "score >= 50", "attendance >= 75", 50, 75,
                "Pass", "Attendance too low", "Fail"),
            new Rule("Project Eligibility Rule", "exam", "project", "exam >= 80", "project >= 70", 80, 70,
                "Eligible", "Project score too low", "Exam score too low")
        };

        private int ruleIndex;
        public int FirstValue { get; private set; }
        public int SecondValue { get; private set; }
        public string Result { get; private set; }
        public string RuleName => Rules[ruleIndex].Name;
        public string FirstName => Rules[ruleIndex].FirstName;
        public string SecondName => Rules[ruleIndex].SecondName;
        public string OuterCondition => Rules[ruleIndex].OuterCondition;
        public string InnerCondition => Rules[ruleIndex].InnerCondition;

        public NestedIfState() => Reset();

        public bool TrySetValues(string input, out string normalized, out string error)
        {
            normalized = string.Empty;
            error = string.Empty;
            string[] parts = (input ?? string.Empty).Split(',');
            if (parts.Length != 2 || !int.TryParse(parts[0].Trim(), out int first) ||
                !int.TryParse(parts[1].Trim(), out int second))
            {
                error = "Enter exactly two whole numbers separated by a comma, such as 65,80.";
                return false;
            }

            FirstValue = first;
            SecondValue = second;
            Result = "?";
            normalized = first + "," + second;
            return true;
        }

        public string ToggleRule()
        {
            ruleIndex = (ruleIndex + 1) % Rules.Length;
            Result = "?";
            return RuleName;
        }

        public string Evaluate(out string explanation)
        {
            Rule rule = Rules[ruleIndex];
            bool outerPassed = FirstValue >= rule.OuterThreshold;
            bool innerPassed = outerPassed && SecondValue >= rule.InnerThreshold;
            Result = outerPassed
                ? (innerPassed ? rule.TrueResult : rule.InnerFalseResult)
                : rule.OuterFalseResult;

            explanation = outerPassed
                ? "Outer condition " + rule.OuterCondition + " was true; inner condition " + rule.InnerCondition +
                  " was checked and was " + (innerPassed ? "true" : "false") + "."
                : "Outer condition " + rule.OuterCondition + " was false; inner condition " + rule.InnerCondition + " was skipped.";
            return Result;
        }

        public void Reset()
        {
            FirstValue = 65;
            SecondValue = 80;
            ruleIndex = 0;
            Result = "?";
        }
    }
}
