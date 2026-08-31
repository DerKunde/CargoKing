using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CargoKing.Streets.Editor
{
    /// <summary>
    /// Adds the curve and tile readouts below the normal fields of a <see cref="StreetSegment"/>, draws
    /// its lanes in the scene, and lets its ends be dragged onto a socket or another segment.
    ///
    /// A tight curve compresses the inside of a swept road mesh, which is geometry rather than a defect
    /// and cannot be corrected away. So the tool reports the number instead of hiding it, and says what
    /// to do about it.
    /// </summary>
    [CustomEditor(typeof(StreetSegment))]
    [CanEditMultipleObjects]
    public class StreetSegmentEditor : UnityEditor.Editor
    {
        private static readonly Color ForwardLaneColor = new Color(0.3f, 0.85f, 1f);
        private static readonly Color BackwardLaneColor = new Color(1f, 0.75f, 0.3f);
        private static readonly Color ContinuationColor = new Color(0.7f, 1f, 0.7f);
        private static readonly Color ConnectedColor = new Color(0.35f, 1f, 0.45f);
        private static readonly Color OpenColor = new Color(1f, 0.35f, 0.35f);

        /// <summary>Roughly how far apart the direction arrows sit along a lane, in metres.</summary>
        private const float ArrowSpacing = 8f;

        /// <summary>How much of a continuing lane is drawn, in metres. Enough to see it carries on.</summary>
        private const float ContinuationPreview = 12f;

        private readonly List<StreetContinuation> continuations = new List<StreetContinuation>();

        private bool isDragging;
        private StreetEnd draggedEnd;
        private Vector3 dragPosition;

        // The readouts track the spline while it is being dragged, so they have to repaint on their own.
        public override bool RequiresConstantRepaint() => true;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if (targets.Length != 1)
            {
                return;
            }

            StreetSegment segment = (StreetSegment)target;

            EditorGUILayout.Space();
            DrawTileReadout(segment);
            DrawCurveReadout(segment);
            DrawConnectionReadout(segment);
        }

        private static void DrawTileReadout(StreetSegment segment)
        {
            Vector3 tile = segment.TileSize;
            if (tile == Vector3.zero)
            {
                return;
            }

            EditorGUILayout.LabelField("Tile", $"{tile.z:0.00} m long, {tile.x:0.00} m across");
        }

        private static void DrawCurveReadout(StreetSegment segment)
        {
            if (float.IsInfinity(segment.MinimumRadius))
            {
                EditorGUILayout.LabelField("Tightest curve", "straight");
            }
            else
            {
                EditorGUILayout.LabelField("Tightest curve", $"{segment.MinimumRadius:0.0} m radius");
            }

            if (!segment.HasTightCurve)
            {
                return;
            }

            EditorGUILayout.HelpBox(
                $"The spline bends down to {segment.MinimumRadius:0.0} m radius, tighter than the "
                + $"{segment.WarningRadius:0.0} m this road is wide enough for. The inside of the curve "
                + "compresses. Ease the curve, or build the corner as an intersection prefab instead of "
                + "bending a segment through it.",
                MessageType.Warning);
        }

        private static void DrawConnectionReadout(StreetSegment segment)
        {
            DrawEndReadout(segment, StreetEnd.Start);
            DrawEndReadout(segment, StreetEnd.End);
        }

        private static void DrawEndReadout(StreetSegment segment, StreetEnd end)
        {
            StreetEndConnector connector = segment.ConnectorAt(end);

            string state = "open";
            if (connector.socket != null)
            {
                state = $"socket '{connector.socket.name}'";
            }
            else if (connector.segment != null)
            {
                state = $"'{connector.segment.name}' ({connector.segmentEnd})";
            }

            EditorGUILayout.LabelField($"{end} end", connector.driven || !connector.IsConnected ? state : state + ", not driven");

            string issue = StreetSnapping.Validate(segment, end);
            if (issue != null)
            {
                EditorGUILayout.HelpBox(issue, MessageType.Warning);
            }
        }

        private void OnSceneGUI()
        {
            StreetSegment segment = (StreetSegment)target;

            DrawLanes(segment);
            DrawContinuations(segment, StreetEnd.Start);
            DrawContinuations(segment, StreetEnd.End);

            DrawEndHandle(segment, StreetEnd.Start);
            DrawEndHandle(segment, StreetEnd.End);
        }

        private void DrawEndHandle(StreetSegment segment, StreetEnd end)
        {
            Vector3 position = segment.EndPosition(end);
            StreetEndConnector connector = segment.ConnectorAt(end);
            float size = HandleUtility.GetHandleSize(position) * 0.12f;

            Handles.color = connector.IsConnected ? ConnectedColor : OpenColor;

            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.FreeMoveHandle(position, size, Vector3.zero, Handles.SphereHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                isDragging = true;
                draggedEnd = end;
                dragPosition = moved;
            }

            if (!isDragging || draggedEnd != end)
            {
                return;
            }

            StreetSnapTarget candidate = StreetSnapping.FindNearest(dragPosition, segment, StreetSnapping.SnapRadius);
            DrawCandidate(candidate, position);

            // Committed on release rather than while dragging, so a pass over a socket on the way to
            // another one does not leave a connection behind.
            if (Event.current.type == EventType.MouseUp)
            {
                if (candidate.IsValid)
                {
                    StreetSnapping.Connect(segment, end, candidate);
                }
                else
                {
                    StreetSnapping.Disconnect(segment, end);
                }

                isDragging = false;
            }
        }

        private void DrawCandidate(StreetSnapTarget candidate, Vector3 from)
        {
            if (!candidate.IsValid)
            {
                Handles.color = OpenColor;
                Handles.DrawDottedLine(from, dragPosition, 3f);
                Handles.Label(dragPosition, "release to disconnect");
                return;
            }

            Handles.color = ConnectedColor;
            Handles.DrawDottedLine(dragPosition, candidate.position, 3f);
            Handles.DrawWireDisc(
                candidate.position,
                Vector3.up,
                HandleUtility.GetHandleSize(candidate.position) * 0.4f);
            Handles.Label(candidate.position, candidate.Label);
        }

        private void DrawContinuations(StreetSegment segment, StreetEnd end)
        {
            segment.CollectContinuations(end, continuations);

            Handles.color = ContinuationColor;
            for (int index = 0; index < continuations.Count; index++)
            {
                DrawContinuation(continuations[index]);
            }
        }

        private static void DrawContinuation(StreetContinuation continuation)
        {
            StreetLaneSample[] samples = continuation.lane.Samples;
            if (samples.Length < 2 || continuation.space == null)
            {
                return;
            }

            List<Vector3> points = new List<Vector3>();
            for (int index = 0; index < samples.Length; index++)
            {
                points.Add(continuation.space.TransformPoint(samples[index].position));

                // Only the first stretch is drawn: enough to see that the lane carries on, without
                // painting the whole rest of the network every time a segment is selected.
                if (samples[index].distance > ContinuationPreview)
                {
                    break;
                }
            }

            if (points.Count >= 2)
            {
                Handles.DrawAAPolyLine(6f, points.ToArray());
            }
        }

        private static void DrawLanes(StreetSegment segment)
        {
            if (segment.Lanes == null)
            {
                return;
            }

            // Drawn only while selected. Unselected they would pile up into an unreadable thicket as
            // soon as there is more than a handful of streets; the permanent toggle belongs in the
            // Street Builder overlay later on.
            for (int index = 0; index < segment.Lanes.Count; index++)
            {
                DrawLane(segment, segment.Lanes[index]);
            }
        }

        private static void DrawLane(StreetSegment segment, StreetLane lane)
        {
            StreetLaneSample[] samples = lane.Samples;
            if (samples.Length < 2)
            {
                return;
            }

            Transform transform = segment.transform;
            Vector3[] points = new Vector3[samples.Length];
            for (int index = 0; index < samples.Length; index++)
            {
                points[index] = transform.TransformPoint(samples[index].position);
            }

            Handles.color = lane.Direction == StreetLaneDirection.Forward ? ForwardLaneColor : BackwardLaneColor;
            Handles.DrawAAPolyLine(3f, points);

            DrawDirectionArrows(transform, lane, points);
        }

        private static void DrawDirectionArrows(Transform transform, StreetLane lane, Vector3[] points)
        {
            StreetLaneSample[] samples = lane.Samples;
            float nextArrowAt = ArrowSpacing * 0.5f;

            for (int index = 0; index < samples.Length; index++)
            {
                if (samples[index].distance < nextArrowAt)
                {
                    continue;
                }

                nextArrowAt += ArrowSpacing;

                Vector3 direction = transform.TransformDirection(samples[index].direction);
                if (direction.sqrMagnitude < 0.000001f)
                {
                    continue;
                }

                float size = HandleUtility.GetHandleSize(points[index]) * 0.25f;
                Handles.ArrowHandleCap(0, points[index], Quaternion.LookRotation(direction), size, EventType.Repaint);
            }
        }
    }
}
