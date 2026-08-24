using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ein Graph im Debug-Overlay mit allen Serien, die darin liegen.
/// </summary>
public class DebugGraphChannel
{
    public readonly string Name;
    public readonly List<GraphSeries> Series = new List<GraphSeries>();

    public DebugGraphChannel(string name)
    {
        Name = name;
    }

    public GraphSeries Find(string seriesName)
    {
        for (int i = 0; i < Series.Count; i++)
        {
            if (Series[i].Name == seriesName)
            {
                return Series[i];
            }
        }

        return null;
    }
}

/// <summary>
/// Fassade fuer den schnellen Fall: irgendwo im Code einen Wert hinschreiben und ihn im
/// Overlay sehen, ohne vorher etwas anzulegen oder im Inspector zu verdrahten.
///
///     DebugGraph.Plot("Gas", gasValue, 0f, 1f);
///     DebugGraph.Plot("Drehzahl", currentRPM);
///     DebugGraph.Plot("Antrieb", "Gas", gasValue, 0f, 1f);   // mehrere Serien in einem Graphen
///
/// Die Werte werden auch dann gesammelt, wenn noch kein DebugGraphHud in der Szene liegt.
/// Das HUD holt sich beim naechsten Frame, was da ist.
/// </summary>
public static class DebugGraph
{
    /// <summary>Samples pro Serie. Bei 50 Hz FixedUpdate sind 600 Samples rund 12 Sekunden.</summary>
    public static int Capacity = GraphElement.DefaultCapacity;

    private static readonly List<DebugGraphChannel> channels = new List<DebugGraphChannel>();
    private static readonly Dictionary<string, DebugGraphChannel> byName = new Dictionary<string, DebugGraphChannel>();

    /// <summary>
    /// Zaehlt hoch, sobald ein Graph oder eine Serie dazukommt. Das HUD erkennt daran,
    /// dass es seine Elemente nachziehen muss.
    /// </summary>
    public static int Revision { get; private set; }

    public static IReadOnlyList<DebugGraphChannel> Channels
    {
        get { return channels; }
    }

    /// <summary>Wert in einen eigenen Graphen mit automatischer Skalierung.</summary>
    public static void Plot(string name, float value)
    {
        GetOrCreateSeries(name, name, false, 0f, 0f).Push(value);
    }

    /// <summary>Wert in einen eigenen Graphen mit festen Grenzen.</summary>
    public static void Plot(string name, float value, float min, float max)
    {
        GetOrCreateSeries(name, name, true, min, max).Push(value);
    }

    /// <summary>Serie in einem benannten Graphen, automatisch skaliert.</summary>
    public static void Plot(string graphName, string seriesName, float value)
    {
        GetOrCreateSeries(graphName, seriesName, false, 0f, 0f).Push(value);
    }

    /// <summary>Serie in einem benannten Graphen, mit festen Grenzen.</summary>
    public static void Plot(string graphName, string seriesName, float value, float min, float max)
    {
        GetOrCreateSeries(graphName, seriesName, true, min, max).Push(value);
    }

    /// <summary>
    /// Punkt mit eigener X-Position - fuer Kurven statt Zeitverlauf, etwa Drehmoment
    /// ueber Drehzahl.
    /// </summary>
    public static void PlotXY(string graphName, string seriesName, float x, float y)
    {
        GetOrCreateSeries(graphName, seriesName, false, 0f, 0f).Push(x, y);
    }

    /// <summary>
    /// Zugriff auf die Serie, um Farbe, Linienbreite oder Zahlenformat zu setzen.
    /// Legt sie an, falls es sie noch nicht gibt.
    /// </summary>
    public static GraphSeries GetSeries(string graphName, string seriesName)
    {
        return GetOrCreateSeries(graphName, seriesName, false, 0f, 0f);
    }

    /// <summary>Loescht die Messwerte, behaelt aber Graphen und Serien.</summary>
    public static void ClearData()
    {
        for (int i = 0; i < channels.Count; i++)
        {
            List<GraphSeries> series = channels[i].Series;

            for (int s = 0; s < series.Count; s++)
            {
                series[s].Clear();
            }
        }
    }

    /// <summary>Wirft alles weg. Das HUD raeumt seine Elemente beim naechsten Frame ab.</summary>
    public static void RemoveAll()
    {
        channels.Clear();
        byName.Clear();
        Revision++;
    }

    // Die Grenzen werden nur beim Anlegen gebraucht. Sie als Werte durchzureichen statt als
    // fertige GraphRange spart die Allokation bei jedem einzelnen Plot-Aufruf.
    private static GraphSeries GetOrCreateSeries(string graphName, string seriesName, bool hasFixedRange, float min, float max)
    {
        DebugGraphChannel channel;

        if (!byName.TryGetValue(graphName, out channel))
        {
            channel = new DebugGraphChannel(graphName);
            byName.Add(graphName, channel);
            channels.Add(channel);
            Revision++;
        }

        GraphSeries series = channel.Find(seriesName);

        if (series == null)
        {
            // Ohne ausdrueckliche Grenzen skaliert die Serie selbst - der haeufigere Fall,
            // wenn man einen unbekannten Wert erst einmal anschauen will.
            GraphRange range = hasFixedRange ? GraphRange.Fixed(min, max) : GraphRange.Auto();

            series = new GraphSeries(seriesName, GraphElement.PaletteColor(channel.Series.Count), range, Capacity);
            channel.Series.Add(series);
            Revision++;
        }

        return series;
    }

    // Statische Felder ueberleben das Verlassen des Play Mode, wenn der Domain Reload
    // abgeschaltet ist. Ohne diesen Reset staenden beim naechsten Start noch die alten
    // Graphen samt Messwerten da.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        channels.Clear();
        byName.Clear();
        Revision = 0;
    }
}
