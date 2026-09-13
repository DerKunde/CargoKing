using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CargoKing.Streets.Editor
{
    /// <summary>
    /// The cockpit for one street network: bake, see what is wrong, and try a route.
    ///
    /// The route preview is the point of it. A baked graph is invisible otherwise, and "does routing
    /// work" is not a question a log line answers convincingly.
    /// </summary>
    [CustomEditor(typeof(StreetNetworkAuthoring))]
    public class StreetNetworkAuthoringEditor : UnityEditor.Editor
    {
        private static readonly Color LaneColour = new Color(0.3f, 0.8f, 1f, 0.5f);
        private static readonly Color IntersectionLaneColour = new Color(1f, 0.8f, 0.3f, 0.6f);
        private static readonly Color RouteColour = new Color(0.2f, 1f, 0.4f);

        private readonly List<StreetNetworkIssue> issues = new List<StreetNetworkIssue>();
        private readonly List<StreetSegment> segments = new List<StreetSegment>();
        private readonly List<Intersection> intersections = new List<Intersection>();
        private readonly List<int> route = new List<int>();

        private bool pickingStart;
        private bool pickingGoal;
        private bool hasStart;
        private bool hasGoal;
        private Vector3 start;
        private Vector3 goal;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            StreetDrawing.DrawInspectorNotice();

            StreetNetworkAuthoring authoring = (StreetNetworkAuthoring)target;

            EditorGUILayout.Space();

            if (authoring.asset == null)
            {
                EditorGUILayout.HelpBox(
                    "No asset. Create one via Assets ▸ Create ▸ CargoKing ▸ Street Network Asset and "
                    + "assign it here.",
                    MessageType.Info);
            }
            else if (StreetNetworkBakeJob.IsStale(authoring))
            {
                EditorGUILayout.HelpBox("The scene has changed since the last bake.", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    $"Baked: {authoring.asset.Lanes.Length} lanes, {authoring.asset.Samples.Length} samples.",
                    MessageType.None);
            }

            if (GUILayout.Button("Bake Network"))
            {
                StreetNetworkBakeJob.Bake(authoring);
                Search(authoring);
                SceneView.RepaintAll();
            }

            DrawValidation(authoring);
            DrawRouteTester(authoring);
        }

        private void DrawValidation(StreetNetworkAuthoring authoring)
        {
            authoring.CollectSegments(segments);
            authoring.CollectIntersections(intersections);
            StreetNetworkValidation.Run(segments, intersections, issues);

            if (issues.Count == 0)
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Issues ({issues.Count})", EditorStyles.boldLabel);

            for (int index = 0; index < issues.Count; index++)
            {
                StreetNetworkIssue issue = issues[index];

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(
                    (issue.severity == StreetNetworkIssueSeverity.Error ? "✖ " : "▲ ") + issue.message,
                    EditorStyles.wordWrappedMiniLabel);

                // Selecting the offending object is the whole value of a list over a log: the author
                // gets to the thing that is wrong in one click.
                if (issue.target != null && GUILayout.Button("Select", GUILayout.Width(60f)))
                {
                    Selection.activeObject = issue.target;
                    SceneView.FrameLastActiveSceneView();
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawRouteTester(StreetNetworkAuthoring authoring)
        {
            if (authoring.asset == null || authoring.asset.IsEmpty)
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Route test", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            pickingStart = GUILayout.Toggle(pickingStart, "Pick start", "Button");
            if (pickingStart)
            {
                pickingGoal = false;
            }

            pickingGoal = GUILayout.Toggle(pickingGoal, "Pick goal", "Button");
            if (pickingGoal)
            {
                pickingStart = false;
            }

            EditorGUILayout.EndHorizontal();

            if (hasStart && hasGoal)
            {
                EditorGUILayout.LabelField(
                    route.Count > 0 ? $"{route.Count} lanes" : "No route found",
                    EditorStyles.miniLabel);
            }
            else
            {
                EditorGUILayout.LabelField("Click in the scene to place both ends.", EditorStyles.miniLabel);
            }
        }

        private void OnSceneGUI()
        {
            StreetNetworkAuthoring authoring = (StreetNetworkAuthoring)target;

            // Off means off: the toggle exists so Unity's own spline editor can have the scene view
            // to itself, and a network drawn over the top of it would defeat that.
            if (!StreetDrawing.Enabled || authoring.asset == null || authoring.asset.IsEmpty)
            {
                return;
            }

            DrawLanes(authoring.asset);
            HandlePicking(authoring);
            DrawRoute(authoring);
        }

        private static void DrawLanes(StreetNetworkAsset asset)
        {
            for (int index = 0; index < asset.Lanes.Length; index++)
            {
                StreetNetworkLane lane = asset.Lanes[index];
                if (lane.sampleCount < 2)
                {
                    continue;
                }

                Handles.color = lane.intersection >= 0 ? IntersectionLaneColour : LaneColour;
                Handles.DrawAAPolyLine(2f, LanePoints(asset, lane, 0f));
            }
        }

        private void HandlePicking(StreetNetworkAuthoring authoring)
        {
            if (!pickingStart && !pickingGoal)
            {
                return;
            }

            // Takes the click away from selection while a pick is armed, the way an EditorTool would.
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

            Event current = Event.current;
            if (current.type != EventType.MouseDown || current.button != 0)
            {
                return;
            }

            Ray ray = HandleUtility.GUIPointToWorldRay(current.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 5000f))
            {
                return;
            }

            if (pickingStart)
            {
                start = hit.point;
                hasStart = true;
                pickingStart = false;
            }
            else
            {
                goal = hit.point;
                hasGoal = true;
                pickingGoal = false;
            }

            Search(authoring);
            current.Use();
            Repaint();
        }

        private void Search(StreetNetworkAuthoring authoring)
        {
            route.Clear();

            if (!hasStart || !hasGoal)
            {
                return;
            }

            StreetNetworkRuntime runtime = authoring.Runtime;
            if (runtime == null)
            {
                return;
            }

            if (runtime.TryProject(start, out StreetRoutePosition from)
                && runtime.TryProject(goal, out StreetRoutePosition to))
            {
                runtime.TryFindRoute(from, to, route);
            }
        }

        private void DrawRoute(StreetNetworkAuthoring authoring)
        {
            StreetNetworkAsset asset = authoring.asset;

            Handles.color = RouteColour;

            if (hasStart)
            {
                Handles.SphereHandleCap(0, start, Quaternion.identity, 2f, EventType.Repaint);
            }

            if (hasGoal)
            {
                Handles.SphereHandleCap(0, goal, Quaternion.identity, 2f, EventType.Repaint);
            }

            for (int index = 0; index < route.Count; index++)
            {
                int lane = route[index];

                // A rebake can shrink the lane array underneath a route found against the old one.
                if (lane < 0 || lane >= asset.Lanes.Length || asset.Lanes[lane].sampleCount < 2)
                {
                    continue;
                }

                // Lifted a little so it reads on top of the thin lane lines rather than inside them.
                Handles.DrawAAPolyLine(6f, LanePoints(asset, asset.Lanes[lane], 0.2f));
            }
        }

        private static Vector3[] LanePoints(StreetNetworkAsset asset, in StreetNetworkLane lane, float lift)
        {
            Vector3[] points = new Vector3[lane.sampleCount];

            for (int sample = 0; sample < lane.sampleCount; sample++)
            {
                points[sample] = asset.Samples[lane.firstSample + sample].position + Vector3.up * lift;
            }

            return points;
        }
    }
}
