using UnityEngine;

/// <summary>
/// Eine Linie im Graphen: Messwerte, Skalierung und Darstellung.
///
/// Zwei Betriebsarten:
/// - Zeitachse (Standard): X ergibt sich aus der Position im Ringpuffer, der neueste Wert
///   klebt am rechten Rand. Das ist der Fall fuer Live-Werte wie den Gaspedalweg.
/// - Freie X-Achse: ueber Push(x, y). Damit laesst sich dieselbe Komponente auch fuer
///   statische Kurven benutzen, etwa Drehmoment ueber Drehzahl.
/// </summary>
public class GraphSeries
{
    public string Name;
    public Color Color = Color.white;
    public float LineWidth = 2f;
    public bool Visible = true;

    // Zahlenformat fuer die Legende. "0.##" reicht fuer Gaspedal und Faktoren,
    // fuer Drehzahlen ist "0" lesbarer.
    public string Format = "0.##";

    public readonly GraphBuffer Values;
    public readonly GraphRange Range;

    // Bleibt null, solange die Serie auf der Zeitachse laeuft.
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
    /// Wert mit eigener X-Position. Der erste Aufruf legt die X-Achse an; ab dann laeuft
    /// die Serie nicht mehr ueber die Zeit.
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
