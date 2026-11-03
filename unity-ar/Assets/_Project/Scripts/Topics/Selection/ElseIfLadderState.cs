using System.Collections.Generic;

namespace Loci.Topics.Selection
{
    /// <summary>Preset Else-if Ladder rules and first-match evaluation.</summary>
    public sealed class ElseIfLadderState
    {
        private sealed class Branch
        {
            public readonly string Label;
            public readonly int Threshold;
            public readonly string Result;

            public Branch(string label, int threshold, string result)
            {
                Label = label;
                Threshold = threshold;
                Result = result;
            }
        }

        private sealed class Rule
        {
            public readonly string Name;
            public readonly Branch[] Branches;
            public readonly string ElseResult;

            public Rule(string name, Branch[] branches, string elseResult)
            {
                Name = name;
                Branches = branches;
                ElseResult = elseResult;
            }
        }

        private static readonly Rule[] Rules =
        {
            new Rule("Grade Rule", new[]
            {
                new Branch("score >= 80", 80, "A"),
                new Branch("score >= 70", 70, "B"),
                new Branch("score >= 50", 50, "Pass")
            }, "Fail"),
            new Rule("Performance Rule", new[]
            {
                new Branch("score >= 90", 90, "Excellent"),
                new Branch("score >= 70", 70, "Good"),
                new Branch("score >= 50", 50, "Average")
            }, "Needs Improvement")
        };

        private int ruleIndex;
        public int Score { get; private set; }
        public string Result { get; private set; }
        public string RuleName => Rules[ruleIndex].Name;

        public ElseIfLadderState() => Reset();

        public void SetScore(int score)
        {
            Score = score;
            Result = "?";
        }

        public string ToggleRule()
        {
            ruleIndex = (ruleIndex + 1) % Rules.Length;
            Result = "?";
            return RuleName;
        }

        public string Evaluate(out string trace)
        {
            Rule rule = Rules[ruleIndex];
            var evaluated = new List<string>();
            string result = rule.ElseResult;

            foreach (Branch branch in rule.Branches)
            {
                bool matches = Score >= branch.Threshold;
                evaluated.Add(branch.Label + " → " + (matches ? "True" : "False"));
                if (!matches) continue;
                result = branch.Result;
                break;
            }

            Result = result;
            trace = string.Join(" | ", evaluated);
            return Result;
        }

        public void Reset()
        {
            Score = 75;
            ruleIndex = 0;
            Result = "?";
        }
    }
}
