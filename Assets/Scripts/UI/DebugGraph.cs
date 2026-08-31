using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One graph in the debug overlay with all the series it holds.
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
/// Facade for the quick case: write a value somewhere in the code and see it in the
/// overlay, without creating anything or wiring it up in the Inspector.
///
///     DebugGraph.Plot("Throttle", gasValue, 0f, 1f);
///     DebugGraph.Plot("RPM", currentRPM);
///     DebugGraph.Plot("Drivetrain", "Throttle", gasValue, 0f, 1f);   // several series in one graph
///
/// Values are collected even while no DebugGraphHud exists in the scene. The HUD picks up
/// whatever is there on its next frame.
/// </summary>
public static class DebugGraph
{
    /// <summary>Samples per series. At 50 Hz FixedUpdate, 600 samples are about 12 seconds.</summary>
    public static int Capacity = GraphElement.DefaultCapacity;

    private static readonly List<DebugGraphChannel> channels = new List<DebugGraphChannel>();
    private static readonly Dictionary<string, DebugGraphChannel> byName = new Dictionary<string, DebugGraphChannel>();

    /// <summary>
    /// Counts up whenever a graph or a series is added. That is how the HUD notices it has
    /// to catch up on its elements.
    /// </summary>
    public static int Revision { get; private set; }

    public static IReadOnlyList<DebugGraphChannel> Channels
    {
        get { return channels; }
    }

    /// <summary>Value in a graph of its own, scaled automatically.</summary>
    public static void Plot(string name, float value)
    {
        GetOrCreateSeries(name, name, false, 0f, 0f).Push(value);
    }

    /// <summary>Value in a graph of its own, with fixed bounds.</summary>
    public static void Plot(string name, float value, float min, float max)
    {
        GetOrCreateSeries(name, name, true, min, max).Push(value);
    }

    /// <summary>Series in a named graph, scaled automatically.</summary>
    public static void Plot(string graphName, string seriesName, float value)
    {
        GetOrCreateSeries(graphName, seriesName, false, 0f, 0f).Push(value);
    }

    /// <summary>Series in a named graph, with fixed bounds.</summary>
    public static void Plot(string graphName, string seriesName, float value, float min, float max)
    {
        GetOrCreateSeries(graphName, seriesName, true, min, max).Push(value);
    }

    /// <summary>
    /// Point with its own x position - for curves instead of a time trace, torque over
    /// revolutions for instance.
    /// </summary>
    public static void PlotXY(string graphName, string seriesName, float x, float y)
    {
        GetOrCreateSeries(graphName, seriesName, false, 0f, 0f).Push(x, y);
    }

    /// <summary>
    /// Access to the series, to set colour, line width or number format. Creates it if it
    /// does not exist yet.
    /// </summary>
    public static GraphSeries GetSeries(string graphName, string seriesName)
    {
        return GetOrCreateSeries(graphName, seriesName, false, 0f, 0f);
    }

    /// <summary>Drops the samples but keeps graphs and series.</summary>
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

    /// <summary>Throws everything away. The HUD clears its elements on the next frame.</summary>
    public static void RemoveAll()
    {
        channels.Clear();
        byName.Clear();
        Revision++;
    }

    // The bounds are only needed when creating. Passing them as values instead of a ready
    // made GraphRange saves the allocation on every single Plot call.
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
            // Without explicit bounds the series scales itself - the more common case when
            // looking at an unknown value for the first time.
            GraphRange range = hasFixedRange ? GraphRange.Fixed(min, max) : GraphRange.Auto();

            series = new GraphSeries(seriesName, GraphElement.PaletteColor(channel.Series.Count), range, Capacity);
            channel.Series.Add(series);
            Revision++;
        }

        return series;
    }

    // Static fields survive leaving Play Mode when the domain reload is turned off. Without
    // this reset the old graphs and their samples would still be there on the next start.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        channels.Clear();
        byName.Clear();
        Revision = 0;
    }
}
