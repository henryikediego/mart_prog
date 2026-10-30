using System.Collections.Generic;

namespace Loci.Topics.Queue
{
    /// <summary>FIFO queue state independent of Unity objects and UI.</summary>
    public sealed class QueueState
    {
        public const int Capacity = 5;
        private readonly List<int> values = new List<int> { 10, 20, 30 };
        public IReadOnlyList<int> Values => values;
        public int Count => values.Count;
        public bool IsEmpty => values.Count == 0;
        public bool IsFull => values.Count >= Capacity;
        public int Front => IsEmpty ? 0 : values[0];

        public void Reset()
        {
            values.Clear();
            values.AddRange(new[] { 10, 20, 30 });
        }
        public bool TryEnqueue(int value)
        {
            if (IsFull) return false;
            values.Add(value);
            return true;
        }
        public bool TryDequeue(out int value)
        {
            value = 0;
            if (IsEmpty) return false;
            value = values[0];
            values.RemoveAt(0);
            return true;
        }
        public bool TryPeek(out int value)
        {
            value = Front;
            return !IsEmpty;
        }
        public int FindIndex(int value) => values.IndexOf(value);
        public string GetFrontToRearOrder() => string.Join(", ", values);
    }
}
