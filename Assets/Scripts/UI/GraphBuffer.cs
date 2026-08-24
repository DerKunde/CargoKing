using System;

/// <summary>
/// Ringpuffer fester Groesse fuer Messwerte. Sobald er voll ist, verdraengt jeder neue
/// Wert den aeltesten - das Fenster wandert also mit und der Graph zeigt immer die
/// letzten Capacity Samples.
///
/// Bewusst frei von UnityEngine: die Klasse ist reine Rechenlogik und laesst sich damit
/// ausserhalb des Editors kompilieren und testen.
/// </summary>
public class GraphBuffer
{
    private readonly float[] samples;

    // Schreibposition fuer den naechsten Wert. Laeuft im Kreis.
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
    /// Index 0 ist der aelteste noch gespeicherte Wert, Count-1 der neueste. Der Aufrufer
    /// muss dadurch nichts von der Schreibposition im Ring wissen.
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
    /// Kleinster und groesster Wert im aktuellen Fenster. Liefert false, solange noch kein
    /// Wert vorliegt - dann gibt es schlicht nichts zu skalieren.
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

    // Solange der Puffer nicht voll ist, steht der aelteste Wert bei 0. Danach steht er
    // genau dort, wo als naechstes geschrieben wird.
    private int RingIndexOf(int index)
    {
        int oldest = count == samples.Length ? head : 0;
        return (oldest + index) % samples.Length;
    }
}
