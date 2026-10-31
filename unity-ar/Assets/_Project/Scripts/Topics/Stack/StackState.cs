using System.Collections.Generic;

namespace Loci.Topics.Stack
{
    /// <summary>Stack values and LIFO operations, independent of Unity objects and UI.</summary>
    public sealed class StackState
    {
        public const int DefaultCapacity = 5;
        private readonly List<int> values = new List<int> { 10, 20, 30 };

        public int Capacity { get; }
        public IReadOnlyList<int> Values => values;
        public int Count => values.Count;
        public bool IsEmpty => values.Count == 0;
        public bool IsFull => values.Count >= Capacity;
        public int Top => values.Count == 0 ? 0 : values[values.Count - 1];

        public StackState(int capacity = DefaultCapacity)
        {
            Capacity = System.Math.Max(3, capacity);
        }

        public void Reset()
        {
            values.Clear();
            values.AddRange(new[] { 10, 20, 30 });
        }

        public bool TryPush(int value)
        {
            if (IsFull) return false;
            values.Add(value);
            return true;
        }

        public bool TryPop(out int value)
        {
            value = 0;
            if (IsEmpty) return false;
            int top = values.Count - 1;
            value = values[top];
            values.RemoveAt(top);
            return true;
        }

        public bool TryPeek(out int value)
        {
            value = Top;
            return !IsEmpty;
        }

        public int FindIndexFromTop(int value)
        {
            for (int i = values.Count - 1, depth = 0; i >= 0; i--, depth++)
                if (values[i] == value) return depth;
            return -1;
        }

        public string GetTopToBottomOrder()
        {
            var ordered = new List<int>(values);
            ordered.Reverse();
            return string.Join(", ", ordered);
        }
    }
}
