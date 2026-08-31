using System;

/// <summary>
/// Fixed size ring buffer for samples. Once it is full every new value displaces the
/// oldest, so the window moves along and the graph always shows the last Capacity samples.
///
/// Deliberately free of UnityEngine: pure computation, so it compiles and can be tested
/// outside the editor.
/// </summary>
public class GraphBuffer
{
    private readonly float[] samples;

    // Write position for the next value. Wraps around.
    private int head;
    private int count;

    public GraphBuffer(int capacity)
    {
        if (capacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Ein Graph braucht mindestens einen Sample-Platz.");
        }

        samples = new float[capacity];
    }

    public int Capacity
    {
        get { return samples.Length; }
    }

    public int Count
    {
        get { return count; }
    }

    /// <summary>
    /// Index 0 is the oldest stored value, Count-1 the newest, so the caller never has to
    /// know where the write position sits in the ring.
    /// </summary>
    public float this[int index]
    {
        get
        {
            if (index < 0 || index >= count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return samples[RingIndexOf(index)];
        }
    }

    public void Push(float value)
    {
        samples[head] = value;
        head = (head + 1) % samples.Length;

        if (count < samples.Length)
        {
            count++;
        }
    }

    public void Clear()
    {
        head = 0;
        count = 0;
    }

    /// <summary>
    /// Smallest and largest value in the current window. Returns false while no value has
    /// arrived yet - then there is simply nothing to scale.
    /// </summary>
    public bool TryGetMinMax(out float min, out float max)
    {
        if (count == 0)
        {
            min = 0f;
            max = 0f;
            return false;
        }

        min = float.MaxValue;
        max = float.MinValue;

        for (int i = 0; i < count; i++)
        {
            float value = samples[RingIndexOf(i)];

            if (value < min)
            {
                min = value;
            }

            if (value > max)
            {
                max = value;
            }
        }

        return true;
    }

    // While the buffer is not full the oldest value sits at 0. Afterwards it sits exactly
    // where the next write goes.
    private int RingIndexOf(int index)
    {
        int oldest = count == samples.Length ? head : 0;
        return (oldest + index) % samples.Length;
    }
}
