using System;
using System.Collections.Generic;

namespace MapGen.Core.Helpers
{
    /// <summary>
    /// A generic Min-Heap implementation that mirrors the .NET 6 PriorityQueue API.
    /// </summary>
    public class PriorityQueue<TElement, TPriority>
    {
        private readonly List<(TElement Element, TPriority Priority)> _nodes;
        private readonly IComparer<TPriority> _comparer;

        public int Count => _nodes.Count;

        public PriorityQueue() : this(Comparer<TPriority>.Default) { }

        public PriorityQueue(IComparer<TPriority> comparer)
        {
            _nodes = new List<(TElement, TPriority)>();
            _comparer = comparer ?? Comparer<TPriority>.Default;
        }

        public void Enqueue(TElement element, TPriority priority)
        {
            _nodes.Add((element, priority));
            int i = _nodes.Count - 1;
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                // If the current node's priority is >= parent's, heap property is satisfied
                if (_comparer.Compare(_nodes[i].Priority, _nodes[parent].Priority) >= 0) break;

                Swap(i, parent);
                i = parent;
            }
        }

        public TElement Dequeue()
        {
            if (_nodes.Count == 0) throw new InvalidOperationException("Queue is empty.");

            TryDequeue(out TElement element, out _);
            return element;
        }

        public bool TryDequeue(out TElement element, out TPriority priority)
        {
            if (_nodes.Count == 0)
            {
                element = default;
                priority = default;
                return false;
            }

            var root = _nodes[0];
            element = root.Element;
            priority = root.Priority;

            _nodes[0] = _nodes[_nodes.Count - 1];
            _nodes.RemoveAt(_nodes.Count - 1);

            if (_nodes.Count > 0)
            {
                HeapifyDown(0);
            }

            return true;
        }

        private void HeapifyDown(int i)
        {
            while (true)
            {
                int left = 2 * i + 1;
                int right = 2 * i + 2;
                int smallest = i;

                if (left < _nodes.Count && _comparer.Compare(_nodes[left].Priority, _nodes[smallest].Priority) < 0)
                    smallest = left;

                if (right < _nodes.Count && _comparer.Compare(_nodes[right].Priority, _nodes[smallest].Priority) < 0)
                    smallest = right;

                if (smallest == i) break;

                Swap(i, smallest);
                i = smallest;
            }
        }

        private void Swap(int a, int b)
        {
            var temp = _nodes[a];
            _nodes[a] = _nodes[b];
            _nodes[b] = temp;
        }
    }
}