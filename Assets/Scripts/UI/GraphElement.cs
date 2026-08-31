using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// UI Toolkit element that draws one or more value series as a line graph, using the vector
/// API (painter2D) - so no textures and no extra GameObjects.
///
/// Direct use:
///     var graph = new GraphElement { graphTitle = "Drivetrain" };
///     var gas = graph.AddSeries("Throttle", Color.green, 0f, 1f);
///     gas.Push(gasValue);
///     graph.Repaint();
///
/// For the quick debug case there is DebugGraph.Plot(...), which creates and draws by itself.
/// </summary>
[UxmlElement]
public partial class GraphElement : VisualElement
{
    public const int DefaultCapacity = 600;

    // Colours for automatically assigned series, in this order.
    private static readonly Color[] Palette =
    {
        new Color(0.40f, 0.85f, 0.35f),
        new Color(1.00f, 0.72f, 0.20f),
        new Color(0.35f, 0.70f, 1.00f),
        new Color(1.00f, 0.42f, 0.42f),
        new Color(0.80f, 0.55f, 1.00f),
        new Color(0.30f, 0.90f, 0.85f),
    };

    private readonly List<GraphSeries> series = new List<GraphSeries>();
    private readonly List<Label> legendLabels = new List<Label>();

    private readonly Label titleLabel;
    private readonly VisualElement legend;
    private readonly VisualElement plotArea;
    private readonly Label maxLabel;
    private readonly Label minLabel;

    private Color gridColor = new Color(1f, 1f, 1f, 0.12f);
    private int gridLineCount = 4;

    public GraphElement()
    {
        style.flexDirection = FlexDirection.Column;
        style.backgroundColor = new Color(0f, 0f, 0f, 0.55f);
        style.borderTopLeftRadius = 4;
        style.borderTopRightRadius = 4;
        style.borderBottomLeftRadius = 4;
        style.borderBottomRightRadius = 4;
        style.paddingLeft = 6;
        style.paddingRight = 6;
        style.paddingTop = 4;
        style.paddingBottom = 4;
        style.marginBottom = 6;

        var header = new VisualElement();
        header.style.flexDirection = FlexDirection.Row;
        header.style.alignItems = Align.Center;
        header.style.flexShrink = 0;
        Add(header);

        titleLabel = new Label();
        titleLabel.style.color = new Color(1f, 1f, 1f, 0.85f);
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        titleLabel.style.fontSize = 11;
        titleLabel.style.marginRight = 8;
        header.Add(titleLabel);

        legend = new VisualElement();
        legend.style.flexDirection = FlexDirection.Row;
        legend.style.flexWrap = Wrap.Wrap;
        legend.style.flexGrow = 1;
        header.Add(legend);

        plotArea = new VisualElement();
        plotArea.style.flexGrow = 1;
        plotArea.style.minHeight = 40;
        plotArea.generateVisualContent += OnGenerateVisualContent;
        Add(plotArea);

        maxLabel = CreateAxisLabel();
        maxLabel.style.top = 0;
        plotArea.Add(maxLabel);

        minLabel = CreateAxisLabel();
        minLabel.style.bottom = 0;
        plotArea.Add(minLabel);

        // After a layout change the plot area no longer matches - redraw.
        plotArea.RegisterCallback<GeometryChangedEvent>(_ => plotArea.MarkDirtyRepaint());
    }

    [UxmlAttribute]
    public string graphTitle
    {
        get { return titleLabel.text; }
        set { titleLabel.text = value; }
    }

    /// <summary>Number of horizontal grid line sections.</summary>
    [UxmlAttribute]
    public int gridLines
    {
        get { return gridLineCount; }
        set
        {
            gridLineCount = Mathf.Max(1, value);
            plotArea.MarkDirtyRepaint();
        }
    }

    public IReadOnlyList<GraphSeries> Series
    {
        get { return series; }
    }

    /// <summary>Series with fixed bounds, throttle 0..1 for instance.</summary>
    public GraphSeries AddSeries(string name, Color color, float min, float max, int capacity = DefaultCapacity)
    {
        return AddSeries(new GraphSeries(name, color, GraphRange.Fixed(min, max), capacity));
    }

    /// <summary>Series whose bounds follow the data in the window.</summary>
    public GraphSeries AddSeries(string name, Color color, int capacity = DefaultCapacity)
    {
        return AddSeries(new GraphSeries(name, color, GraphRange.Auto(), capacity));
    }

    /// <summary>Series with a colour taken from the palette.</summary>
    public GraphSeries AddSeries(string name, int capacity = DefaultCapacity)
    {
        return AddSeries(name, NextPaletteColor(), capacity);
    }

    public GraphSeries AddSeries(GraphSeries newSeries)
    {
        series.Add(newSeries);

        var label = new Label();
        label.style.color = newSeries.Color;
        label.style.fontSize = 11;
        label.style.marginRight = 8;
        legend.Add(label);
        legendLabels.Add(label);

        plotArea.MarkDirtyRepaint();
        return newSeries;
    }

    public GraphSeries FindSeries(string name)
    {
        for (int i = 0; i < series.Count; i++)
        {
            if (series[i].Name == name)
            {
                return series[i];
            }
        }

        return null;
    }

    public Color NextPaletteColor()
    {
        return PaletteColor(series.Count);
    }

    /// <summary>
    /// Palette colour number index, wrapping around. Lets series that do not belong to a
    /// graph yet pick the matching colour straight away.
    /// </summary>
    public static Color PaletteColor(int index)
    {
        return Palette[Mathf.Abs(index) % Palette.Length];
    }

    public void ClearSeriesData()
    {
        for (int i = 0; i < series.Count; i++)
        {
            series[i].Clear();
        }

        plotArea.MarkDirtyRepaint();
    }

    /// <summary>
    /// Catches up the scaling, refreshes the labels and redraws. Call once per frame, not
    /// once per pushed value.
    /// </summary>
    public void Repaint()
    {
        for (int i = 0; i < series.Count; i++)
        {
            GraphSeries current = series[i];
            current.ResolveRanges();

            Label label = legendLabels[i];
            label.style.color = current.Color;
            label.text = current.Name + "  " + current.Latest.ToString(current.Format);
            label.style.display = current.Visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        UpdateAxisLabels();
        plotArea.MarkDirtyRepaint();
    }

    // An axis label is only unambiguous while exactly one series sets the range. With
    // several series on different bounds it would mislead, and the legend shows the values.
    private void UpdateAxisLabels()
    {
        bool showAxis = series.Count == 1;
        maxLabel.style.display = showAxis ? DisplayStyle.Flex : DisplayStyle.None;
        minLabel.style.display = showAxis ? DisplayStyle.Flex : DisplayStyle.None;

        if (!showAxis)
        {
            return;
        }

        GraphSeries only = series[0];
        maxLabel.text = only.Range.Max.ToString(only.Format);
        minLabel.text = only.Range.Min.ToString(only.Format);
    }

    private static Label CreateAxisLabel()
    {
        var label = new Label();
        label.style.position = Position.Absolute;
        label.style.left = 2;
        label.style.fontSize = 9;
        label.style.color = new Color(1f, 1f, 1f, 0.45f);
        return label;
    }

    private void OnGenerateVisualContent(MeshGenerationContext context)
    {
        Rect rect = plotArea.contentRect;

        // Before the first layout the size is NaN, and under two pixels there is nothing
        // sensible to draw.
        if (float.IsNaN(rect.width) || float.IsNaN(rect.height) || rect.width < 2f || rect.height < 2f)
        {
            return;
        }

        Painter2D painter = context.painter2D;
        DrawGrid(painter, rect);

        for (int i = 0; i < series.Count; i++)
        {
            if (series[i].Visible)
            {
                DrawSeries(painter, rect, series[i]);
            }
        }
    }

    private void DrawGrid(Painter2D painter, Rect rect)
    {
        painter.strokeColor = gridColor;
        painter.lineWidth = 1f;

        for (int i = 0; i <= gridLineCount; i++)
        {
            float y = rect.yMin + rect.height * i / gridLineCount;

            painter.BeginPath();
            painter.MoveTo(new Vector2(rect.xMin, y));
            painter.LineTo(new Vector2(rect.xMax, y));
            painter.Stroke();
        }
    }

    private void DrawSeries(Painter2D painter, Rect rect, GraphSeries current)
    {
        int count = current.Values.Count;

        // A line needs two points.
        if (count < 2)
        {
            return;
        }

        current.ResolveRanges();

        painter.strokeColor = current.Color;
        painter.lineWidth = current.LineWidth;
        painter.lineJoin = LineJoin.Round;
        painter.lineCap = LineCap.Round;

        painter.BeginPath();

        for (int i = 0; i < count; i++)
        {
            Vector2 point = PointFor(rect, current, i, count);

            if (i == 0)
            {
                painter.MoveTo(point);
            }
            else
            {
                painter.LineTo(point);
            }
        }

        painter.Stroke();
    }

    private static Vector2 PointFor(Rect rect, GraphSeries current, int index, int count)
    {
        float x;

        if (current.HasExplicitX && current.XValues.Count == count)
        {
            x = rect.xMin + current.XRange.Normalize(current.XValues[index]) * rect.width;
        }
        else
        {
            // Time axis: the spacing of two samples follows the full capacity, so the line
            // grows in from the right instead of squeezing itself together.
            float step = rect.width / Mathf.Max(1, current.Values.Capacity - 1);
            x = rect.xMax - (count - 1 - index) * step;
        }

        // In UI Toolkit y grows downwards, the sample value upwards.
        float y = rect.yMax - current.Range.Normalize(current.Values[index]) * rect.height;

        return new Vector2(x, y);
    }
}
