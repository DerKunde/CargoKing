using UnityEditor;
using UnityEngine;

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
            StreetSnapTarget best = default;
            float bestDistance = radius;

            IntersectionSocket[] sockets = Object.FindObjectsByType<IntersectionSocket>(FindObjectsSortMode.None);
            for (int index = 0; index < sockets.Length; index++)
            {
                Vector3 socketPosition = sockets[index].transform.position;
                float distance = Vector3.Distance(position, socketPosition);

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = new StreetSnapTarget { socket = sockets[index], position = socketPosition };
                }
            }

            StreetSegment[] segments = Object.FindObjectsByType<StreetSegment>(FindObjectsSortMode.None);
            for (int index = 0; index < segments.Length; index++)
            {
                StreetSegment segment = segments[index];
                if (segment == exclude)
                {
                    continue;
                }

                bestDistance = ConsiderEnd(position, segment, StreetEnd.Start, bestDistance, ref best);
                bestDistance = ConsiderEnd(position, segment, StreetEnd.End, bestDistance, ref best);
            }

            return best;
        }

        private static float ConsiderEnd(
            Vector3 position,
            StreetSegment segment,
            StreetEnd end,
            float bestDistance,
            ref StreetSnapTarget best)
        {
            Vector3 endPosition = segment.EndPosition(end);
            float distance = Vector3.Distance(position, endPosition);

            if (distance >= bestDistance)
            {
                return bestDistance;
            }

            best = new StreetSnapTarget { segment = segment, segmentEnd = end, position = endPosition };
            return distance;
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
            if (!Mathf.Approximately(socket.roadWidth, segment.roadWidth))
            {
                return $"The socket is {socket.roadWidth:0.0} m wide, this street {segment.roadWidth:0.0} m. "
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
