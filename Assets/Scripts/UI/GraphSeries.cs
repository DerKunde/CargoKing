using UnityEngine;

/// <summary>
/// One line in a graph: samples, scaling and appearance.
///
/// Two modes:
/// - Time axis (default): x comes from the position in the ring buffer, the newest value
///   sticks to the right edge. This is the case for live values such as throttle travel.
/// - Free x axis, through Push(x, y). Lets the same component draw static curves as well,
///   torque over revolutions for instance.
/// </summary>
public class GraphSeries
{
    public string Name;
    public Color Color = Color.white;
    public float LineWidth = 2f;
    public bool Visible = true;

    // Number format for the legend. "0.##" is enough for throttle and factors, revolutions
    // read better as "0".
    public string Format = "0.##";

    public readonly GraphBuffer Values;
    public readonly GraphRange Range;

    // Stays null while the series runs on the time axis.
    public GraphBuffer XValues;
    public GraphRange XRange;

    public GraphSeries(string name, Color color, GraphRange range, int capacity)
    {
        Name = name;
        Color = color;
        Range = range;
        Values = new GraphBuffer(capacity);
    }

    public bool HasExplicitX
    {
        get { return XValues != null; }
    }

    public float Latest
    {
        get { return Values.Count > 0 ? Values[Values.Count - 1] : 0f; }
    }

    public void Push(float value)
    {
        Values.Push(value);
    }

    /// <summary>
    /// Value with its own x position. The first call creates the x axis; from then on the
    /// series no longer runs over time.
    /// </summary>
    public void Push(float x, float y)
    {
        if (XValues == null)
        {
            XValues = new GraphBuffer(Values.Capacity);
            XRange = GraphRange.Auto();
        }

        XValues.Push(x);
        Values.Push(y);
    }

    public void Clear()
    {
        Values.Clear();

        if (XValues != null)
        {
            XValues.Clear();
        }
    }

    public void ResolveRanges()
    {
        Range.ResolveFrom(Values);

        if (XValues != null)
        {
            XRange.ResolveFrom(XValues);
        }
    }
}
