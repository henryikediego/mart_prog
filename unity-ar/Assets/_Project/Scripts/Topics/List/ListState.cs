using System.Collections.Generic;

namespace Loci.Topics.List
{
    /// <summary>
    /// The List data state, independent of Unity objects, AR, and UI.
    /// List data and a display-derived learner-facing capacity, independent
    /// of Unity objects, AR, and UI.
    /// </summary>
    public sealed class ListState
    {
        public const int DefaultCapacity = 5;

        private readonly List<int> values = new List<int> { 10, 20, 30 };

        public int Capacity { get; }
        public IReadOnlyList<int> Values => values;

        public ListState(int capacity = DefaultCapacity)
        {
            Capacity = System.Math.Max(3, capacity);
        }

        public void Reset()
        {
            values.Clear();
            values.Add(10);
            values.Add(20);
            values.Add(30);
        }

        public bool TryInsert(int value, out int insertedIndex)
        {
            insertedIndex = -1;
            if (values.Count >= Capacity)
                return false;

            insertedIndex = values.Count;
            values.Add(value);
            return true;
        }

        public bool TryDeleteLast(out int deletedValue)
        {
            deletedValue = 0;
            if (values.Count == 0)
                return false;

            int lastIndex = values.Count - 1;
            deletedValue = values[lastIndex];
            values.RemoveAt(lastIndex);
            return true;
        }

        public int FindIndex(int value) => values.IndexOf(value);

        public string GetTraversalOrder()
        {
            return string.Join(", ", values);
        }
    }
}
