using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// UI-Toolkit-Element, das eine oder mehrere Wertereihen als Liniengraph zeichnet.
/// Gezeichnet wird mit der Vector-API (painter2D) - also ohne Textures und ohne
/// zusaetzliche GameObjects.
///
/// Direkte Benutzung:
///     var graph = new GraphElement { graphTitle = "Antrieb" };
///     var gas = graph.AddSeries("Gas", Color.green, 0f, 1f);
///     gas.Push(gasValue);
///     graph.Repaint();
///
/// Fuer den schnellen Debug-Fall gibt es DebugGraph.Plot(...), das sich um Anlegen und
/// Zeichnen selbst kuemmert.
/// </summary>
[UxmlElement]
public partial class GraphElement : VisualElement
{
    public const int DefaultCapacity = 600;

    // Farben fuer automatisch vergebene Serien, in dieser Reihenfolge.
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

        // Nach einem Layoutwechsel stimmt die Zeichenflaeche nicht mehr - neu zeichnen.
        plotArea.RegisterCallback<GeometryChangedEvent>(_ => plotArea.MarkDirtyRepaint());
    }

    [UxmlAttribute]
    public string graphTitle
    {
        get { return titleLabel.text; }
        set { titleLabel.text = value; }
    }

    /// <summary>Anzahl der waagerechten Rasterlinien-Abschnitte.</summary>
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

    /// <summary>Serie mit festen Grenzen, etwa Gaspedal 0..1.</summary>
    public GraphSeries AddSeries(string name, Color color, float min, float max, int capacity = DefaultCapacity)
    {
        return AddSeries(new GraphSeries(name, color, GraphRange.Fixed(min, max), capacity));
    }

    /// <summary>Serie, deren Grenzen laufend aus den Daten im Fenster kommen.</summary>
    public GraphSeries AddSeries(string name, Color color, int capacity = DefaultCapacity)
    {
        return AddSeries(new GraphSeries(name, color, GraphRange.Auto(), capacity));
    }

    /// <summary>Serie mit automatisch vergebener Farbe aus der Palette.</summary>
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
    /// Farbe Nummer index aus der Palette, umlaufend. Damit koennen auch Serien, die noch
    /// keinem Graphen zugeordnet sind, gleich die passende Farbe bekommen.
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
    /// Skalierung nachziehen, Beschriftung auffrischen und neu zeichnen. Einmal pro
    /// Frame aufrufen - nicht pro gepushtem Wert.
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

    // Eine Achsenbeschriftung ist nur eindeutig, solange genau eine Serie den Bereich
    // vorgibt. Bei mehreren Serien mit unterschiedlichen Grenzen waere sie irrefuehrend -
    // dann zeigt allein die Legende die Werte.
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

        // Vor dem ersten Layout ist die Groesse NaN, und unter zwei Pixeln gibt es nichts
        // Sinnvolles zu zeichnen.
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

        // Eine Linie braucht zwei Punkte.
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
            // Zeitachse: der Abstand zweier Samples richtet sich nach der vollen Kapazitaet,
            // damit die Linie beim Fuellen von rechts hereinwaechst statt sich zu stauchen.
            float step = rect.width / Mathf.Max(1, current.Values.Capacity - 1);
            x = rect.xMax - (count - 1 - index) * step;
        }

        // In UI Toolkit waechst Y nach unten, der Messwert aber nach oben.
        float y = rect.yMax - current.Range.Normalize(current.Values[index]) * rect.height;

        return new Vector2(x, y);
    }
}
