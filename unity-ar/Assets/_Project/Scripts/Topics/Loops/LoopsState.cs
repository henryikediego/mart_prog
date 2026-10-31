using System.Collections.Generic;

namespace Loci.Topics.Loops
{
    /// <summary>Loop values and iteration state, independent of Unity objects and AR.</summary>
    public sealed class LoopsState
    {
        public const int MaximumValueCount = 6;
        public const int DefaultFirstValue = 10;
        public const int DefaultSecondValue = 20;
        public const int DefaultThirdValue = 30;

        private readonly List<int> values = new List<int>();

        public IReadOnlyList<int> Values => values;
        public int CurrentIndex { get; private set; }
        public int RunningTotal { get; private set; }
        public int VisitedCount { get; private set; }
        public string LoopMessage { get; private set; }

        public LoopsState() => Reset();

        public bool TrySetValues(IReadOnlyList<int> newValues)
        {
            if (newValues == null || newValues.Count < 1 || newValues.Count > MaximumValueCount)
                return false;

            values.Clear();
            for (int i = 0; i < newValues.Count; i++)
                values.Add(newValues[i]);

            ResetProgress();
            return true;
        }

        /// <returns>True if an iteration ran, false if the loop was already complete.</returns>
        public bool Step()
        {
            if (CurrentIndex >= values.Count - 1)
            {
                LoopMessage = "done\ntotal = " + RunningTotal;
                return false;
            }

            CurrentIndex++;
            VisitedCount = CurrentIndex + 1;
            RunningTotal += values[CurrentIndex];
            LoopMessage = "value = " + values[CurrentIndex] + "\ntotal = " + RunningTotal;
            return true;
        }

        public void BeginRun()
        {
            CurrentIndex = -1;
            RunningTotal = 0;
            VisitedCount = 0;
            LoopMessage = "starting\ntotal = 0";
        }

        public void CompleteRun()
        {
            CurrentIndex = -1;
            LoopMessage = "done\ntotal = " + RunningTotal;
        }

        public void Reset()
        {
            values.Clear();
            values.Add(DefaultFirstValue);
            values.Add(DefaultSecondValue);
            values.Add(DefaultThirdValue);
            ResetProgress();
        }

        public string FormatValues(string separator = ", ") => string.Join(separator, values);

        private void ResetProgress()
        {
            CurrentIndex = -1;
            RunningTotal = 0;
            VisitedCount = 0;
            LoopMessage = "for value\nin numbers";
        }
    }
}
