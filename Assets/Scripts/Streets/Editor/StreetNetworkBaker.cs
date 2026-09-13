using System.Collections.Generic;
using UnityEngine;

namespace CargoKing.Streets.Editor
{
    /// <summary>
    /// What one pass of the baker produced, before it is written into an asset. Lists rather than
    /// arrays because the baker fills them as it goes.
    /// </summary>
    public class StreetNetworkBakeResult
    {
        public readonly List<StreetNetworkSample> samples = new List<StreetNetworkSample>();
        public readonly List<StreetNetworkLane> lanes = new List<StreetNetworkLane>();
        public readonly List<int> exits = new List<int>();

        /// <summary>
        /// The authoring object each baked lane came from, in the same order as <see cref="lanes"/>.
        /// Not written into the asset - it is what lets a validation message and the scene view point
        /// at the object the author has to fix.
        /// </summary>
        public readonly List<Object> laneSources = new List<Object>();
    }

    /// <summary>
    /// Turns the street objects in a scene into the flat arrays of a baked network.
    ///
    /// Reads the authoring side but writes nothing back into it. Editor-only: after a bake nothing in
    /// a build needs any of this.
    /// </summary>
    public static class StreetNetworkBaker
    {
        /// <summary>Nominal limit on an intersection path in m/s. High on purpose - the curve binds.</summary>
        private const float IntersectionSpeedLimit = 25f;

        /// <summary>Shortest lane ambient traffic may be placed on, in metres.</summary>
        public const float MinimumSpawnLength = 30f;

        /// <summary>Tightest bend a spawn lane may have, in metres. A car appearing mid-hairpin looks placed.</summary>
        public const float MinimumSpawnRadius = 40f;

        /// <summary>
        /// Flattens every lane of every segment and intersection, and wires up which lane leads on to
        /// which.
        ///
        /// The wiring leans on the authoring side rather than working out geometry a second time:
        /// <see cref="StreetSegment.CollectContinuations"/> already answers "what carries on here", and
        /// the lane objects it hands back are the very ones being baked, so they can be looked up by
        /// reference.
        /// </summary>
        public static StreetNetworkBakeResult Collect(
            IReadOnlyList<StreetSegment> segments,
            IReadOnlyList<Intersection> intersections)
        {
            StreetNetworkBakeResult result = new StreetNetworkBakeResult();

            // Reference identity, not value: two lanes with the same samples are still two lanes.
            Dictionary<StreetLane, int> laneIndices = new Dictionary<StreetLane, int>();

            // Which segment lane leaves an intersection by which socket, so an intersection path can
            // find what it leads to. Sockets stay passive, so this is the reverse lookup that
            // authoring deliberately does not store.
            Dictionary<IntersectionSocket, int> socketExits = new Dictionary<IntersectionSocket, int>();

            EmitSegments(segments, result, laneIndices, socketExits);
            EmitIntersections(intersections, result, laneIndices);
            LinkSegments(segments, result, laneIndices);
            LinkIntersections(intersections, result, laneIndices, socketExits);

            StreetPathCrossings.Apply(result);
            MarkSpawnable(result);

            return result;
        }

        /// <summary>
        /// Marks the lanes ambient traffic may be placed on: ordinary road lanes, long enough to fit a
        /// car with room to accelerate, and straight enough that a car appearing there does not
        /// immediately look wrong.
        /// </summary>
        public static void MarkSpawnable(StreetNetworkBakeResult result)
        {
            for (int index = 0; index < result.lanes.Count; index++)
            {
                StreetNetworkLane lane = result.lanes[index];

                bool spawnable = lane.intersection < 0 && lane.length >= MinimumSpawnLength;

                for (int sample = 0; spawnable && sample < lane.sampleCount; sample++)
                {
                    if (result.samples[lane.firstSample + sample].radius < MinimumSpawnRadius)
                    {
                        spawnable = false;
                    }
                }

                lane.spawnable = spawnable;
                result.lanes[index] = lane;
            }
        }

        private static void EmitSegments(
            IReadOnlyList<StreetSegment> segments,
            StreetNetworkBakeResult result,
            Dictionary<StreetLane, int> laneIndices,
            Dictionary<IntersectionSocket, int> socketExits)
        {
            for (int index = 0; index < segments.Count; index++)
            {
                StreetSegment segment = segments[index];
                if (segment == null || segment.Lanes.Count < 2)
                {
                    continue;
                }

                for (int side = 0; side < segment.Lanes.Count; side++)
                {
                    StreetLane lane = segment.Lanes[side];
                    laneIndices[lane] = result.lanes.Count;

                    Emit(result, lane, segment.transform, segment.SpeedLimit, -1, -1, StreetTurn.Straight, segment);
                }

                // A lane leads away from an intersection when it starts at the socket. The forward lane
                // does that at the segment's start, the backward lane at its end.
                RecordSocketExit(segment, StreetEnd.Start, laneIndices[segment.Lanes[0]], socketExits);
                RecordSocketExit(segment, StreetEnd.End, laneIndices[segment.Lanes[1]], socketExits);
            }
        }

        private static void RecordSocketExit(
            StreetSegment segment,
            StreetEnd end,
            int lane,
            Dictionary<IntersectionSocket, int> socketExits)
        {
            StreetEndConnector connector = segment.ConnectorAt(end);

            if (connector != null && connector.socket != null)
            {
                socketExits[connector.socket] = lane;
            }
        }

        private static void EmitIntersections(
            IReadOnlyList<Intersection> intersections,
            StreetNetworkBakeResult result,
            Dictionary<StreetLane, int> laneIndices)
        {
            for (int index = 0; index < intersections.Count; index++)
            {
                Intersection intersection = intersections[index];
                if (intersection == null)
                {
                    continue;
                }

                for (int path = 0; path < intersection.Connections.Count; path++)
                {
                    IntersectionConnection connection = intersection.Connections[path];
                    laneIndices[connection.Lane] = result.lanes.Count;

                    // Speed across an intersection is set by its geometry, not by a limit, so the
                    // cornering speed decides. The limit here only has to be high enough not to be
                    // the thing that binds.
                    Emit(
                        result,
                        connection.Lane,
                        intersection.transform,
                        IntersectionSpeedLimit,
                        index,
                        path,
                        connection.Turn,
                        intersection);
                }
            }
        }

        private static void Emit(
            StreetNetworkBakeResult result,
            StreetLane lane,
            Transform space,
            float speedLimit,
            int intersection,
            int pathIndex,
            StreetTurn turn,
            Object source)
        {
            StreetLaneSample[] laneSamples = lane.Samples;

            StreetNetworkLane baked = new StreetNetworkLane
            {
                firstSample = result.samples.Count,
                sampleCount = laneSamples.Length,
                length = lane.Length,
                speedLimit = speedLimit,
                intersection = intersection,
                pathIndex = pathIndex,
                crossingMask = 0,
                firstExit = 0,
                exitCount = 0,
                turn = turn,
                spawnable = false,
            };

            for (int index = 0; index < laneSamples.Length; index++)
            {
                StreetLaneSample sample = laneSamples[index];

                result.samples.Add(new StreetNetworkSample
                {
                    position = space.TransformPoint(sample.position),
                    direction = space.TransformDirection(sample.direction).normalized,
                    distance = sample.distance,
                    radius = sample.radius,
                });
            }

            result.lanes.Add(baked);
            result.laneSources.Add(source);
        }

        private static void LinkSegments(
            IReadOnlyList<StreetSegment> segments,
            StreetNetworkBakeResult result,
            Dictionary<StreetLane, int> laneIndices)
        {
            List<StreetContinuation> continuations = new List<StreetContinuation>();

            for (int index = 0; index < segments.Count; index++)
            {
                StreetSegment segment = segments[index];
                if (segment == null || segment.Lanes.Count < 2)
                {
                    continue;
                }

                // The forward lane runs out at the segment's end, the backward lane at its start.
                LinkLane(segment, StreetEnd.End, segment.Lanes[0], result, laneIndices, continuations);
                LinkLane(segment, StreetEnd.Start, segment.Lanes[1], result, laneIndices, continuations);
            }
        }

        private static void LinkLane(
            StreetSegment segment,
            StreetEnd end,
            StreetLane lane,
            StreetNetworkBakeResult result,
            Dictionary<StreetLane, int> laneIndices,
            List<StreetContinuation> continuations)
        {
            if (!laneIndices.TryGetValue(lane, out int laneIndex))
            {
                return;
            }

            segment.CollectContinuations(end, continuations);

            int first = result.exits.Count;
            int count = 0;

            for (int index = 0; index < continuations.Count; index++)
            {
                if (laneIndices.TryGetValue(continuations[index].lane, out int next))
                {
                    result.exits.Add(next);
                    count++;
                }
            }

            StreetNetworkLane baked = result.lanes[laneIndex];
            baked.firstExit = first;
            baked.exitCount = count;
            result.lanes[laneIndex] = baked;
        }

        private static void LinkIntersections(
            IReadOnlyList<Intersection> intersections,
            StreetNetworkBakeResult result,
            Dictionary<StreetLane, int> laneIndices,
            Dictionary<IntersectionSocket, int> socketExits)
        {
            for (int index = 0; index < intersections.Count; index++)
            {
                Intersection intersection = intersections[index];
                if (intersection == null)
                {
                    continue;
                }

                for (int path = 0; path < intersection.Connections.Count; path++)
                {
                    IntersectionConnection connection = intersection.Connections[path];

                    if (!laneIndices.TryGetValue(connection.Lane, out int laneIndex))
                    {
                        continue;
                    }

                    int first = result.exits.Count;
                    int count = 0;

                    if (connection.ToSocket >= 0 && connection.ToSocket < intersection.Sockets.Count)
                    {
                        IntersectionSocket socket = intersection.Sockets[connection.ToSocket];

                        if (socket != null && socketExits.TryGetValue(socket, out int next))
                        {
                            result.exits.Add(next);
                            count++;
                        }
                    }

                    StreetNetworkLane baked = result.lanes[laneIndex];
                    baked.firstExit = first;
                    baked.exitCount = count;
                    result.lanes[laneIndex] = baked;
                }
            }
        }
    }
}
