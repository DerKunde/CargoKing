using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace CargoKing.Streets.Editor
{
    /// <summary>
    /// A place a street end can dock to: an intersection socket, or an end of another segment.
    /// </summary>
    public struct StreetSnapTarget
    {
        public IntersectionSocket socket;
        public StreetSegment segment;
        public StreetEnd segmentEnd;
        public Vector3 position;

        /// <summary>
        /// Whether nothing else docks here yet: a socket no other segment uses, or a segment end that
        /// is still open. The nearest search finds taken targets too - validation reports those - but
        /// only free ones are worth showing as places a street can go.
        /// </summary>
        public bool isFree;

        public bool IsValid => socket != null || segment != null;

        public string Label => socket != null ? socket.name : $"{segment.name} ({segmentEnd})";
    }

    /// <summary>
    /// Finds what a dragged street end could dock to, and writes the connection.
    ///
    /// Proximity is only the gesture here - what gets stored is the reference. Searching by distance
    /// again later would be the fragile version: two streets passing close by would weld themselves
    /// together, and nudging an intersection would silently break a seam with no error anywhere.
    /// </summary>
    public static class StreetSnapping
    {
        /// <summary>How close a dragged end has to come before it docks, in metres.</summary>
        public const float SnapRadius = 6f;

        /// <summary>
        /// The nearest place within reach that this end could dock to.
        /// </summary>
        /// <param name="position">Where the dragged end currently is, in world space.</param>
        /// <param name="exclude">Segment being dragged, so it cannot dock to itself.</param>
        public static StreetSnapTarget FindNearest(Vector3 position, StreetSegment exclude, float radius)
        {
            CollectTargets(exclude, nearestBuffer);

            StreetSnapTarget best = default;
            float bestDistance = radius;

            // Strictly closer only, in the order CollectTargets lists them: of two targets at the
            // same distance the socket wins, then the earlier segment - as before the list existed.
            for (int index = 0; index < nearestBuffer.Count; index++)
            {
                float distance = Vector3.Distance(position, nearestBuffer[index].position);

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = nearestBuffer[index];
                }
            }

            return best;
        }

        private static readonly System.Collections.Generic.List<StreetSnapTarget> nearestBuffer =
            new System.Collections.Generic.List<StreetSnapTarget>();

        /// <summary>
        /// Every place an end of this segment could dock to: every intersection socket, then both ends
        /// of every other segment. Each is marked free or taken, so the scene view can show where a
        /// street can go before it is dragged there, and the nearest search reads the same list.
        /// </summary>
        /// <param name="exclude">Segment being dragged. Its own ends are left out, and a socket it
        /// docks to itself still counts as free.</param>
        public static void CollectTargets(
            StreetSegment exclude,
            System.Collections.Generic.List<StreetSnapTarget> results)
        {
            results.Clear();

            StreetSegment[] segments = Object.FindObjectsByType<StreetSegment>(FindObjectsSortMode.None);
            IntersectionSocket[] sockets = Object.FindObjectsByType<IntersectionSocket>(FindObjectsSortMode.None);

            for (int index = 0; index < sockets.Length; index++)
            {
                IntersectionSocket socket = sockets[index];

                results.Add(new StreetSnapTarget
                {
                    socket = socket,
                    position = socket.transform.position,
                    isFree = FindSegmentAt(socket, exclude, segments) == null,
                });
            }

            for (int index = 0; index < segments.Length; index++)
            {
                StreetSegment segment = segments[index];
                if (segment == exclude)
                {
                    continue;
                }

                AddEnd(results, segment, StreetEnd.Start);
                AddEnd(results, segment, StreetEnd.End);
            }
        }

        private static void AddEnd(
            System.Collections.Generic.List<StreetSnapTarget> results,
            StreetSegment segment,
            StreetEnd end)
        {
            results.Add(new StreetSnapTarget
            {
                segment = segment,
                segmentEnd = end,
                position = segment.EndPosition(end),
                isFree = !segment.ConnectorAt(end).IsConnected,
            });
        }

        /// <summary>
        /// The segment docked to a socket, or null when the socket is still free.
        /// </summary>
        /// <param name="exclude">Segment to ignore, so one can ask about its own socket.</param>
        public static StreetSegment FindSegmentAt(IntersectionSocket socket, StreetSegment exclude)
        {
            return FindSegmentAt(socket, exclude, Object.FindObjectsByType<StreetSegment>(FindObjectsSortMode.None));
        }

        /// <summary>
        /// The same question asked against a list of segments that has already been gathered. Scene
        /// GUI code repaints constantly and would otherwise scan the whole scene once per socket.
        /// </summary>
        public static StreetSegment FindSegmentAt(
            IntersectionSocket socket,
            StreetSegment exclude,
            StreetSegment[] segments)
        {
            if (socket == null)
            {
                return null;
            }

            for (int index = 0; index < segments.Length; index++)
            {
                StreetSegment segment = segments[index];
                if (segment == exclude)
                {
                    continue;
                }

                if (segment.startConnection.socket == socket || segment.endConnection.socket == socket)
                {
                    return segment;
                }
            }

            return null;
        }

        /// <summary>
        /// Docks one end of a segment to a target. Docking to another segment merges the two.
        /// </summary>
        /// <returns>
        /// The segment that carries the connection afterwards. Merging returns the survivor, which is
        /// not the segment that was passed in. Null when nothing happened.
        /// </returns>
        public static StreetSegment Connect(StreetSegment segment, StreetEnd end, StreetSnapTarget target)
        {
            if (!target.IsValid)
            {
                return null;
            }

            // Two streets meeting no longer stay two objects that reference each other - they become
            // one street with one spline.
            if (target.socket == null)
            {
                if (!StreetSurgery.CanMerge(segment, target.segment, out string problem))
                {
                    // Asked before anything moves, so a refused merge leaves the dragged street where
                    // the user put it rather than yanked onto a target it never joined.
                    Debug.LogWarning(problem, target.segment);
                    return null;
                }

                PullEndOnto(segment, end, target.segment.EndPosition(target.segmentEnd));

                return StreetSurgery.Merge(segment, end, target.segment, target.segmentEnd);
            }

            Undo.RecordObject(segment, "Connect Street");

            StreetEndConnector connector = segment.ConnectorAt(end);
            connector.Clear();
            connector.driven = true;
            connector.socket = target.socket;

            EditorUtility.SetDirty(segment);
            segment.Rebuild();

            return segment;
        }

        /// <summary>
        /// Moves one end of a segment exactly onto a world position, so a merge finds the two ends
        /// lying on each other and welds them into a single knot.
        ///
        /// The gesture docks anything within <see cref="SnapRadius"/>, six hundred times the distance
        /// a merge treats as "the same point". Without this the road would keep both knots and carry a
        /// kinked gap of up to six metres. The old driven-connector path did exactly this, by pulling
        /// the driven end onto its counterpart. Taking a junction back out deliberately does not: there
        /// the two halves stand a socket offset apart and that stretch of road has to stay.
        /// </summary>
        private static void PullEndOnto(StreetSegment segment, StreetEnd end, Vector3 worldPosition)
        {
            SplineContainer container = segment.GetComponent<SplineContainer>();
            Spline spline = container != null ? container.Spline : null;

            if (spline == null || spline.Count == 0)
            {
                return;
            }

            Undo.RecordObject(container, "Connect Street");

            int index = end == StreetEnd.Start ? 0 : spline.Count - 1;
            Vector3 local = segment.transform.InverseTransformPoint(worldPosition);

            BezierKnot knot = spline[index];
            knot.Position = new float3(local.x, local.y, local.z);
            spline.SetKnot(index, knot);
        }

        /// <summary>
        /// Opens one end again, and clears whatever recorded the seam from the other side.
        /// </summary>
        public static void Disconnect(StreetSegment segment, StreetEnd end)
        {
            StreetEndConnector connector = segment.ConnectorAt(end);
            if (!connector.IsConnected)
            {
                return;
            }

            Undo.RecordObject(segment, "Disconnect Street");

            if (connector.segment != null)
            {
                Undo.RecordObject(connector.segment, "Disconnect Street");

                StreetEndConnector counterpart = connector.segment.ConnectorAt(connector.segmentEnd);
                if (counterpart.segment == segment)
                {
                    counterpart.Clear();
                }

                EditorUtility.SetDirty(connector.segment);
            }

            connector.Clear();
            EditorUtility.SetDirty(segment);
            segment.Rebuild();
        }

        /// <summary>
        /// What is wrong with one end, or null when it is fine.
        /// </summary>
        public static string Validate(StreetSegment segment, StreetEnd end)
        {
            StreetEndConnector connector = segment.ConnectorAt(end);

            if (connector.socket != null && connector.segment != null)
            {
                return $"The {end} end docks to a socket and to a segment at the same time. Clear one of them.";
            }

            if (connector.socket == null && connector.segment == null)
            {
                return null;
            }

            if (connector.socket != null)
            {
                return ValidateSocket(segment, end, connector.socket);
            }

            if (connector.segment == segment)
            {
                return $"The {end} end docks to its own segment.";
            }

            StreetEndConnector counterpart = connector.segment.ConnectorAt(connector.segmentEnd);
            if (counterpart.segment != segment)
            {
                return $"The {end} end docks to '{connector.segment.name}', but that segment does not "
                    + "record the seam. Reconnect it.";
            }

            if (connector.driven == counterpart.driven)
            {
                return $"Both sides of the seam at the {end} end are set to "
                    + (connector.driven ? "driven" : "not driven")
                    + ". Exactly one of them has to move.";
            }

            return null;
        }

        private static string ValidateSocket(StreetSegment segment, StreetEnd end, IntersectionSocket socket)
        {
            if (!Mathf.Approximately(socket.roadWidth, segment.RoadWidth))
            {
                return $"The socket is {socket.roadWidth:0.0} m wide, this street {segment.RoadWidth:0.0} m. "
                    + "The lanes will not line up at the seam.";
            }

            StreetEnd otherEnd = end == StreetEnd.Start ? StreetEnd.End : StreetEnd.Start;
            if (segment.ConnectorAt(otherEnd).socket == socket)
            {
                return "Both ends of this segment dock to the same socket.";
            }

            StreetSegment other = FindSegmentAt(socket, segment);
            if (other != null)
            {
                return $"'{other.name}' already docks to this socket.";
            }

            return null;
        }
    }
}
