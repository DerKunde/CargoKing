using UnityEditor;
using UnityEngine;

namespace CargoKing.Streets.Editor
{
    /// <summary>
    /// Adds the curve and tile readouts below the normal fields of a <see cref="StreetSegment"/>, and
    /// draws its lanes in the scene while it is selected.
    ///
    /// A tight curve compresses the inside of a swept road mesh, which is geometry rather than a
    /// defect and cannot be corrected away. So the tool reports the number instead of hiding it, and
    /// says what to do about it.
    /// </summary>
    [CustomEditor(typeof(StreetSegment))]
    [CanEditMultipleObjects]
    public class StreetSegmentEditor : UnityEditor.Editor
    {
        private static readonly Color ForwardLaneColor = new Color(0.3f, 0.85f, 1f);
        private static readonly Color BackwardLaneColor = new Color(1f, 0.75f, 0.3f);

        /// <summary>Roughly how far apart the direction arrows sit along a lane, in metres.</summary>
        private const float ArrowSpacing = 8f;

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
                return;
            }

            EditorGUILayout.LabelField("Tightest curve", $"{segment.MinimumRadius:0.0} m radius");

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

        private void OnSceneGUI()
        {
            StreetSegment segment = (StreetSegment)target;
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
