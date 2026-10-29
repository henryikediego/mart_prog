using System.Collections.Generic;

namespace Loci.Topics.ArrayList
{
    /// <summary>
    /// The Array/List data state, independent of Unity objects, AR, and UI.
    /// The five-item learner-facing capacity and starting values match the
    /// validated legacy Array/List controller.
    /// </summary>
    public sealed class ArrayListState
    {
        public const int Capacity = 5;

        private readonly List<int> values = new List<int> { 10, 20, 30 };

        public IReadOnlyList<int> Values => values;

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
