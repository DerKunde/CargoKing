using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// Zeigt alle ueber DebugGraph angemeldeten Graphen als Overlay im Play Mode.
///
/// Aufbau in der Szene: ein leeres GameObject, darauf diese Komponente. Das UIDocument
/// kommt per RequireComponent automatisch dazu und braucht nur noch ein PanelSettings-Asset.
/// Sonst ist nichts zu verdrahten - die Graphen entstehen aus den Plot-Aufrufen selbst.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class DebugGraphHud : MonoBehaviour
{
    [Header("Darstellung")]
    public float panelWidth = 340f;
    public float graphHeight = 110f;
    public float margin = 8f;
    public bool anchorRight = false;

    [Header("Steuerung")]
    public bool showGraphs = true;
    public Key toggleKey = Key.F1;

    private UIDocument document;
    private VisualElement container;

    private readonly Dictionary<string, GraphElement> graphs = new Dictionary<string, GraphElement>();
    private readonly List<string> staleGraphNames = new List<string>();

    // DebugGraph.Revision startet bei 0, also muss der erste Abgleich in jedem Fall laufen.
    private int knownRevision = -1;

    private void Awake()
    {
        document = GetComponent<UIDocument>();
    }

    private void OnDisable()
    {
        // Das UIDocument wirft seinen Baum beim Deaktivieren weg. Die Referenzen zeigen
        // danach ins Leere, deshalb hier zuruecksetzen und beim naechsten Mal neu aufbauen.
        container = null;
        graphs.Clear();
        knownRevision = -1;
    }

    private void LateUpdate()
    {
        HandleToggleKey();

        if (!EnsureContainer())
        {
            return;
        }

        container.style.display = showGraphs ? DisplayStyle.Flex : DisplayStyle.None;

        if (!showGraphs)
        {
            return;
        }

        if (knownRevision != DebugGraph.Revision)
        {
            SyncGraphs();
            knownRevision = DebugGraph.Revision;
        }

        foreach (KeyValuePair<string, GraphElement> pair in graphs)
        {
            pair.Value.style.height = graphHeight;
            pair.Value.Repaint();
        }
    }

    private void HandleToggleKey()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null || toggleKey == Key.None)
        {
            return;
        }

        if (keyboard[toggleKey].wasPressedThisFrame)
        {
            showGraphs = !showGraphs;
        }
    }

    /// <summary>
    /// Liefert false, solange das UIDocument noch keinen Baum hat - das passiert im ersten
    /// Frame je nach Ausfuehrungsreihenfolge.
    /// </summary>
    private bool EnsureContainer()
    {
        if (container != null && container.panel != null)
        {
            ApplyContainerLayout();
            return true;
        }

        if (document == null)
        {
            document = GetComponent<UIDocument>();
        }

        VisualElement root = document != null ? document.rootVisualElement : null;

        if (root == null)
        {
            return false;
        }

        container = new VisualElement();

        // Das Overlay darf keine Klicks abfangen - darunter liegt das Spiel.
        container.pickingMode = PickingMode.Ignore;
        container.style.position = Position.Absolute;
        ApplyContainerLayout();
        root.Add(container);

        // Der Baum ist neu, die vorher gemerkten Elemente gehoeren zum alten.
        graphs.Clear();
        knownRevision = -1;

        return true;
    }

    private void ApplyContainerLayout()
    {
        container.style.top = margin;
        container.style.width = panelWidth;

        if (anchorRight)
        {
            container.style.right = margin;
            container.style.left = StyleKeyword.Auto;
        }
        else
        {
            container.style.left = margin;
            container.style.right = StyleKeyword.Auto;
        }
    }

    private void SyncGraphs()
    {
        IReadOnlyList<DebugGraphChannel> channels = DebugGraph.Channels;

        RemoveGraphsWithoutChannel(channels);

        for (int i = 0; i < channels.Count; i++)
        {
            DebugGraphChannel channel = channels[i];
            GraphElement graph;

            if (!graphs.TryGetValue(channel.Name, out graph))
            {
                graph = new GraphElement();
                graph.graphTitle = channel.Name;
                graph.style.height = graphHeight;
                container.Add(graph);
                graphs.Add(channel.Name, graph);
            }

            // Serien koennen jederzeit dazukommen, wenn irgendwo ein neuer Plot-Aufruf
            // zum ersten Mal laeuft.
            for (int s = 0; s < channel.Series.Count; s++)
            {
                GraphSeries series = channel.Series[s];

                if (graph.FindSeries(series.Name) == null)
                {
                    graph.AddSeries(series);
                }
            }
        }
    }

    private void RemoveGraphsWithoutChannel(IReadOnlyList<DebugGraphChannel> channels)
    {
        staleGraphNames.Clear();

        foreach (KeyValuePair<string, GraphElement> pair in graphs)
        {
            if (!ContainsChannel(channels, pair.Key))
            {
                staleGraphNames.Add(pair.Key);
            }
        }

        for (int i = 0; i < staleGraphNames.Count; i++)
        {
            string name = staleGraphNames[i];
            graphs[name].RemoveFromHierarchy();
            graphs.Remove(name);
        }
    }

    private static bool ContainsChannel(IReadOnlyList<DebugGraphChannel> channels, string name)
    {
        for (int i = 0; i < channels.Count; i++)
        {
            if (channels[i].Name == name)
            {
                return true;
            }
        }

        return false;
    }
}
