using System;

namespace CargoKing.Diagnostics
{
    /// <summary>
    /// Fixed-capacity buffer that keeps the newest values and drops the oldest. Index 0 is the
    /// oldest value still held, Count - 1 the newest - the order a trail is drawn in.
    /// </summary>
    public sealed class RingBuffer<T>
    {
        private readonly T[] items;
        private int start;

        public RingBuffer(int capacity)
        {
            if (capacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "A ring buffer needs room for at least one value.");
            }

            items = new T[capacity];
        }

        public int Capacity => items.Length;
        public int Count { get; private set; }

        public void Add(T item)
        {
            if (Count < items.Length)
            {
                items[(start + Count) % items.Length] = item;
                Count++;
                return;
            }

            items[start] = item;
            start = (start + 1) % items.Length;
        }

        public T this[int index]
        {
            get
            {
                if (index < 0 || index >= Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(index), index, $"The buffer holds {Count} values.");
                }

                return items[(start + index) % items.Length];
            }
        }

        public void Clear()
        {
            Array.Clear(items, 0, items.Length);
            start = 0;
            Count = 0;
        }
    }
}
