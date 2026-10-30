namespace Loci.Topics.LinkedList
{
    /// <summary>Minimal singly linked-list state, independent of Unity and rendering.</summary>
    public sealed class LinkedListState
    {
        public const int Capacity = 5;

        public sealed class Node
        {
            public int Value { get; }
            public Node Next { get; internal set; }

            internal Node(int value) => Value = value;
        }

        public Node Head { get; private set; }
        public Node Tail { get; private set; }
        public int Count { get; private set; }
        public bool IsEmpty => Head == null;
        public bool IsFull => Count >= Capacity;

        public LinkedListState() => Reset();

        public void Reset()
        {
            Head = new Node(10);
            Head.Next = new Node(20);
            Head.Next.Next = new Node(30);
            Tail = Head.Next.Next;
            Count = 3;
        }

        public bool TryAppend(int value)
        {
            if (IsFull) return false;

            var node = new Node(value);
            if (IsEmpty)
                Head = node;
            else
                Tail.Next = node;

            Tail = node;
            Count++;
            return true;
        }

        public bool TryRemoveHead(out int value)
        {
            value = 0;
            if (IsEmpty) return false;

            value = Head.Value;
            Head = Head.Next;
            Count--;
            if (Head == null)
                Tail = null;
            return true;
        }

        public int FindIndex(int value)
        {
            int index = 0;
            for (Node node = Head; node != null; node = node.Next, index++)
                if (node.Value == value) return index;
            return -1;
        }

        public int ValueAt(int index)
        {
            if (index < 0 || index >= Count) throw new System.ArgumentOutOfRangeException(nameof(index));
            Node node = Head;
            for (int i = 0; i < index; i++) node = node.Next;
            return node.Value;
        }

        public string GetHeadToTailOrder()
        {
            var order = new System.Text.StringBuilder();
            for (Node node = Head; node != null; node = node.Next)
            {
                if (order.Length > 0) order.Append(", ");
                order.Append(node.Value);
            }
            return order.ToString();
        }
    }
}
