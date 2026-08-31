using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// Shows every graph registered through DebugGraph as an overlay in Play Mode.
///
/// Setup in the scene: an empty GameObject with this component. RequireComponent brings the
/// UIDocument along, which only needs a PanelSettings asset. Nothing else to wire up - the
/// graphs come out of the Plot calls themselves.
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

    // DebugGraph.Revision starts at 0, so the first sync has to run in any case.
    private int knownRevision = -1;

    private void Awake()
    {
        document = GetComponent<UIDocument>();
    }

    private void OnDisable()
    {
        // The UIDocument throws its tree away on deactivation, leaving the references
        // dangling. Reset here and rebuild on the next pass.
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
    /// Returns false while the UIDocument has no tree yet - which happens in the first
    /// frame, depending on execution order.
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

        // The overlay must not swallow clicks - the game is underneath.
        container.pickingMode = PickingMode.Ignore;
        container.style.position = Position.Absolute;
        ApplyContainerLayout();
        root.Add(container);

        // The tree is new, so the elements remembered before belong to the old one.
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

            // Series can appear at any time, whenever a new Plot call runs for the first time.
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
