using UnityEditor;
using UnityEngine;

namespace CargoKing.Streets.Editor
{
    /// <summary>
    /// Shows what an <see cref="Intersection"/> derived from its sockets: how many ways lead across,
    /// and where each of them runs.
    /// </summary>
    [CustomEditor(typeof(Intersection))]
    public class IntersectionEditor : UnityEditor.Editor
    {
        private static readonly Color SocketColor = new Color(0.4f, 1f, 0.6f);
        private static readonly Color StraightColor = new Color(0.3f, 0.85f, 1f);
        private static readonly Color LeftColor = new Color(1f, 0.5f, 0.9f);
        private static readonly Color RightColor = new Color(0.6f, 1f, 0.4f);

        // The connections follow the sockets while they are dragged, so this has to repaint on its own.
        public override bool RequiresConstantRepaint() => true;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            Intersection intersection = (Intersection)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Sockets", $"{intersection.Sockets.Count} active");
            EditorGUILayout.LabelField("Ways across", $"{intersection.Connections.Count}");
            EditorGUILayout.LabelField("Free sockets", $"{CountFree(intersection)}");

            StreetDrawing.DrawInspectorNotice();

            if (StreetDrawing.Enabled)
            {
                EditorGUILayout.HelpBox(
                    "Drag the amber ball in front of a free socket to pull a street out of it. The draw "
                    + "tool then takes over, so the road can be carried on with more knots. Release near "
                    + "another socket or a street end instead to dock the far end and finish the road.",
                    MessageType.None);
            }

            if (intersection.Sockets.Count < 2)
            {
                EditorGUILayout.HelpBox(
                    "An intersection needs at least two active sockets. Add an Intersection Socket to a "
                    + "child object at the end of each arm, with its blue Z axis pointing away from the "
                    + "intersection. Deactivate a socket to drop that arm - that is how the same model "
                    + "serves as a T junction.",
                    MessageType.Info);
            }

            Intersection self = intersection;
            bool hasPair = JunctionInsertion.TryGetDockedPair(self, out _, out _, out _, out _);

            using (new EditorGUI.DisabledScope(!hasPair))
            {
                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("Flip"))
                {
                    JunctionInsertion.Flip(self);
                }

                if (GUILayout.Button("Remove and close the road"))
                {
                    JunctionInsertion.Remove(self);
                }

                EditorGUILayout.EndHorizontal();
            }

            if (!hasPair)
            {
                EditorGUILayout.HelpBox(
                    "Flipping and removing need exactly two streets docked to this junction.",
                    MessageType.None);
            }
        }

        private void OnSceneGUI()
        {
            if (!StreetDrawing.Enabled)
            {
                return;
            }

            Intersection intersection = (Intersection)target;
            Transform transform = intersection.transform;

            DrawSockets(intersection, transform);

            for (int index = 0; index < intersection.Connections.Count; index++)
            {
                DrawConnection(intersection.Connections[index], transform);
            }

            // Drawn last so the pull handles sit on top of the socket markings they belong to.
            IntersectionSocketDragging.Draw(intersection);
        }

        /// <summary>How many arms of this intersection have no street on them yet.</summary>
        private static int CountFree(Intersection intersection)
        {
            StreetSegment[] segments = Object.FindObjectsByType<StreetSegment>(FindObjectsSortMode.None);
            int free = 0;

            for (int index = 0; index < intersection.Sockets.Count; index++)
            {
                if (StreetSnapping.FindSegmentAt(intersection.Sockets[index], null, segments) == null)
                {
                    free++;
                }
            }

            return free;
        }

        private static void DrawSockets(Intersection intersection, Transform transform)
        {
            Handles.color = SocketColor;

            for (int index = 0; index < intersection.Sockets.Count; index++)
            {
                IntersectionSocket socket = intersection.Sockets[index];
                Vector3 position = socket.transform.position;
                Vector3 sideways = socket.transform.right * (socket.roadWidth * 0.5f);

                Handles.DrawAAPolyLine(4f, position - sideways, position + sideways);

                float size = HandleUtility.GetHandleSize(position) * 0.3f;
                Handles.ArrowHandleCap(0, position, socket.transform.rotation, size, EventType.Repaint);
                Handles.Label(position + sideways, $"#{index}");
            }
        }

        private static void DrawConnection(IntersectionConnection connection, Transform transform)
        {
            StreetLaneSample[] samples = connection.Lane.Samples;
            if (samples.Length < 2)
            {
                return;
            }

            Vector3[] points = new Vector3[samples.Length];
            for (int index = 0; index < samples.Length; index++)
            {
                points[index] = transform.TransformPoint(samples[index].position);
            }

            Handles.color = ColorFor(connection.Turn);
            Handles.DrawAAPolyLine(2f, points);

            // One arrow at the midpoint is enough - twelve paths crossing in a few square metres turn
            // unreadable fast if every one of them carries a row of arrows.
            int middle = samples.Length / 2;
            Vector3 direction = transform.TransformDirection(samples[middle].direction);
            if (direction.sqrMagnitude > 0.000001f)
            {
                float size = HandleUtility.GetHandleSize(points[middle]) * 0.15f;
                Handles.ArrowHandleCap(0, points[middle], Quaternion.LookRotation(direction), size, EventType.Repaint);
            }
        }

        private static Color ColorFor(StreetTurn turn)
        {
            switch (turn)
            {
                case StreetTurn.Left:
                    return LeftColor;
                case StreetTurn.Right:
                    return RightColor;
                default:
                    return StraightColor;
            }
        }
    }
}
