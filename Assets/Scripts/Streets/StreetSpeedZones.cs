using System.Collections.Generic;
using UnityEngine;

namespace CargoKing.Streets
{
    /// <summary>A speed sign as the zone computation sees it: which lane, where along it, what limit.</summary>
    public struct StreetSpeedSignPlacement
    {
        /// <summary>Index of the lane the sign governs.</summary>
        public int lane;

        /// <summary>Metres from the start of that lane, measured along the lane.</summary>
        public float distance;

        /// <summary>The posted limit in metres per second.</summary>
        public float speedLimit;
    }

    /// <summary>
    /// Turns speed signs into a limit per sample. A sign governs its lane from where it stands, carries on
    /// across docked streets, and ends at the next sign or at an intersection; a path through an
    /// intersection keeps the limit it was entered with.
    ///
    /// Pure and free of scene objects, so the bake and the editor's speed band compute exactly the same
    /// thing and the rules can be tested without a scene.
    /// </summary>
    public static class StreetSpeedZones
    {
        /// <summary>Two distances closer than this are the same place, in metres.</summary>
        private const float SampleEpsilon = 0.001f;

        /// <summary>
        /// Applies the signs to a baked network.
        /// </summary>
        /// <param name="defaultLimits">Per lane, in m/s: the limit a road lane starts with when no
        /// street leads into it, and the limit of an intersection path no street leads into.</param>
        /// <returns>The samples with one extra at every sign that stands between two samples. The lanes
        /// are rewritten in place to point into it, with their highest limit and travel time.</returns>
        public static StreetNetworkSample[] Apply(
            StreetNetworkSample[] samples,
            StreetNetworkLane[] lanes,
            int[] exits,
            float[] defaultLimits,
            IReadOnlyList<StreetSpeedSignPlacement> signs)
        {
            List<StreetSpeedSignPlacement>[] signsByLane = SortByLane(lanes, signs);
            StreetNetworkSample[] result = InsertSignSamples(samples, lanes, signsByLane);
            int[] predecessors = FindRoadPredecessors(lanes, exits);
            float[] startLimits = ResolveStartLimits(lanes, predecessors, defaultLimits, signsByLane);

            WriteLimits(result, lanes, startLimits, signsByLane);
            return result;
        }

        /// <summary>
        /// The signs of each road lane, ordered by distance. Two signs at the same distance keep the
        /// order they were given in, so the later one wins and the result never depends on a sort.
        /// </summary>
        private static List<StreetSpeedSignPlacement>[] SortByLane(
            StreetNetworkLane[] lanes,
            IReadOnlyList<StreetSpeedSignPlacement> signs)
        {
            List<StreetSpeedSignPlacement>[] byLane = new List<StreetSpeedSignPlacement>[lanes.Length];
            if (signs == null)
            {
                return byLane;
            }

            for (int index = 0; index < signs.Count; index++)
            {
                StreetSpeedSignPlacement sign = signs[index];

                // An intersection path never takes a sign: a junction is governed by the road leading in.
                if (sign.lane < 0 || sign.lane >= lanes.Length || lanes[sign.lane].intersection >= 0)
                {
                    continue;
                }

                sign.distance = Mathf.Clamp(sign.distance, 0f, lanes[sign.lane].length);

                List<StreetSpeedSignPlacement> list = byLane[sign.lane] ??= new List<StreetSpeedSignPlacement>();

                int at = list.Count;
                while (at > 0 && list[at - 1].distance > sign.distance)
                {
                    at--;
                }

                list.Insert(at, sign);
            }

            return byLane;
        }

        /// <summary>
        /// Copies every lane's samples into a new array and puts one more exactly where each sign
        /// stands, so the limit changes beside the sign and not at the next sample metres further on.
        /// </summary>
        private static StreetNetworkSample[] InsertSignSamples(
            StreetNetworkSample[] samples,
            StreetNetworkLane[] lanes,
            List<StreetSpeedSignPlacement>[] signsByLane)
        {
            List<StreetNetworkSample> result = new List<StreetNetworkSample>(samples.Length + 16);

            for (int laneIndex = 0; laneIndex < lanes.Length; laneIndex++)
            {
                StreetNetworkLane lane = lanes[laneIndex];
                List<StreetSpeedSignPlacement> signs = signsByLane[laneIndex];

                int first = result.Count;
                int next = 0;

                for (int index = 0; index < lane.sampleCount; index++)
                {
                    StreetNetworkSample sample = samples[lane.firstSample + index];

                    while (signs != null && next < signs.Count && signs[next].distance < sample.distance + SampleEpsilon)
                    {
                        float distance = signs[next].distance;

                        bool onThisSample = Mathf.Abs(sample.distance - distance) <= SampleEpsilon;
                        bool onLastAdded = result.Count > first
                            && Mathf.Abs(result[result.Count - 1].distance - distance) <= SampleEpsilon;

                        if (!onThisSample && !onLastAdded && index > 0)
                        {
                            // Read from the lane as it was, with the same interpolation every other
                            // reader of a lane uses.
                            result.Add(StreetLaneGeometry.SampleAt(samples, lane, distance));
                        }

                        next++;
                    }

                    result.Add(sample);
                }

                lane.firstSample = first;
                lane.sampleCount = result.Count - first;
                lanes[laneIndex] = lane;
            }

            return result.ToArray();
        }

        /// <summary>
        /// The road lane leading into each lane, or -1. Lanes of intersection paths are never recorded
        /// as predecessors, so a road lane after a junction has none. Should the data ever show more than
        /// one, the lowest lane index wins: it is the first one written while counting upwards.
        /// </summary>
        private static int[] FindRoadPredecessors(StreetNetworkLane[] lanes, int[] exits)
        {
            int[] predecessors = new int[lanes.Length];
            for (int index = 0; index < predecessors.Length; index++)
            {
                predecessors[index] = -1;
            }

            for (int lane = 0; lane < lanes.Length; lane++)
            {
                if (lanes[lane].intersection >= 0)
                {
                    continue;
                }

                for (int exit = 0; exit < lanes[lane].exitCount; exit++)
                {
                    int next = exits[lanes[lane].firstExit + exit];

                    if (next >= 0 && next < lanes.Length && predecessors[next] < 0)
                    {
                        predecessors[next] = lane;
                    }
                }
            }

            return predecessors;
        }

        /// <summary>
        /// The limit each lane starts with. A road lane inherits the limit in force at the end of the
        /// road lane before it; one without starts from its default. The chain is walked back to a known
        /// start and resolved forwards, so a long street of many segments costs one pass.
        /// </summary>
        private static float[] ResolveStartLimits(
            StreetNetworkLane[] lanes,
            int[] predecessors,
            float[] defaultLimits,
            List<StreetSpeedSignPlacement>[] signsByLane)
        {
            float[] start = new float[lanes.Length];
            bool[] resolved = new bool[lanes.Length];
            bool[] onChain = new bool[lanes.Length];
            List<int> chain = new List<int>();

            for (int lane = 0; lane < lanes.Length; lane++)
            {
                if (resolved[lane] || lanes[lane].intersection >= 0)
                {
                    continue;
                }

                chain.Clear();
                int current = lane;

                while (!resolved[current])
                {
                    if (onChain[current])
                    {
                        // A ring of streets with no junction: the walk has come back round. The lane it
                        // closes on starts from its own default, which is what ends the walk.
                        start[current] = defaultLimits[current];
                        resolved[current] = true;
                        break;
                    }

                    onChain[current] = true;
                    chain.Add(current);

                    int previous = predecessors[current];
                    if (previous < 0)
                    {
                        start[current] = defaultLimits[current];
                        resolved[current] = true;
                        break;
                    }

                    current = previous;
                }

                for (int index = chain.Count - 1; index >= 0; index--)
                {
                    int link = chain[index];
                    onChain[link] = false;

                    if (!resolved[link])
                    {
                        start[link] = EndLimit(start, predecessors[link], signsByLane);
                        resolved[link] = true;
                    }
                }
            }

            // Every road lane is known now, so an intersection path reads the lane that enters it.
            for (int lane = 0; lane < lanes.Length; lane++)
            {
                if (lanes[lane].intersection >= 0)
                {
                    start[lane] = predecessors[lane] >= 0
                        ? EndLimit(start, predecessors[lane], signsByLane)
                        : defaultLimits[lane];
                }
            }

            return start;
        }

        /// <summary>The limit in force at the end of a resolved road lane: its last sign's, or its start.</summary>
        private static float EndLimit(float[] start, int lane, List<StreetSpeedSignPlacement>[] signsByLane)
        {
            List<StreetSpeedSignPlacement> signs = signsByLane[lane];
            return signs != null && signs.Count > 0 ? signs[signs.Count - 1].speedLimit : start[lane];
        }

        private static void WriteLimits(
            StreetNetworkSample[] samples,
            StreetNetworkLane[] lanes,
            float[] startLimits,
            List<StreetSpeedSignPlacement>[] signsByLane)
        {
            for (int laneIndex = 0; laneIndex < lanes.Length; laneIndex++)
            {
                StreetNetworkLane lane = lanes[laneIndex];
                List<StreetSpeedSignPlacement> signs = signsByLane[laneIndex];

                float limit = startLimits[laneIndex];
                float highest = 0f;
                int next = 0;

                for (int index = 0; index < lane.sampleCount; index++)
                {
                    int at = lane.firstSample + index;

                    // Every sign at or before this sample has taken effect by now; the last one wins.
                    while (signs != null && next < signs.Count && signs[next].distance <= samples[at].distance + SampleEpsilon)
                    {
                        limit = signs[next].speedLimit;
                        next++;
                    }

                    samples[at].speedLimit = limit;
                    highest = Mathf.Max(highest, limit);
                }

                lane.speedLimit = Mathf.Max(highest, StreetProfile.MinimumSpeedLimit);
                lane.travelTime = StreetLaneGeometry.TravelTime(samples, lane, 0f);
                lanes[laneIndex] = lane;
            }
        }
    }
}
