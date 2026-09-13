using System.Collections.Generic;
using UnityEngine;

namespace CargoKing.Streets
{
    /// <summary>
    /// A* over the lane graph. Lanes are the nodes, the exits of a lane are its edges.
    ///
    /// Cost is time rather than distance: a motorway detour that arrives sooner is the better route,
    /// and a driver picking the shortest way through slow side streets looks lost. Turning costs
    /// extra, because a left turn across oncoming traffic is not free even where the geometry is.
    /// </summary>
    public static class StreetRouteSearch
    {
        /// <summary>Seconds added for crossing an intersection, by the way the vehicle turns.</summary>
        private const float StraightPenalty = 0.5f;
        private const float RightPenalty = 2f;
        private const float LeftPenalty = 4f;

        /// <summary>Cap so a broken graph cannot spin forever. Far above any sane map.</summary>
        private const int MaximumVisits = 20000;

        private static readonly List<int> exits = new List<int>();
        private static readonly Dictionary<int, float> best = new Dictionary<int, float>();
        private static readonly Dictionary<int, int> cameFrom = new Dictionary<int, int>();
        private static readonly List<int> open = new List<int>();
        private static readonly Dictionary<int, float> estimated = new Dictionary<int, float>();

        /// <summary>Seconds added for a turn of that kind.</summary>
        public static float TurnPenalty(StreetTurn turn)
        {
            switch (turn)
            {
                case StreetTurn.Left:
                    return LeftPenalty;
                case StreetTurn.Right:
                    return RightPenalty;
                default:
                    return StraightPenalty;
            }
        }

        /// <summary>
        /// Finds a route from a place on one lane to another lane.
        /// </summary>
        /// <param name="fromDistance">How far along the starting lane the vehicle already is; only the
        /// remainder of that lane is charged.</param>
        /// <param name="route">Filled with the lanes to drive, starting lane first. Cleared first, and
        /// left empty when there is no route.</param>
        /// <returns>False when either lane index is out of range or no route exists.</returns>
        public static bool TryFind(
            StreetNetworkAsset asset,
            int fromLane,
            float fromDistance,
            int toLane,
            List<int> route)
        {
            route.Clear();

            if (asset == null || asset.IsEmpty)
            {
                return false;
            }

            StreetNetworkLane[] lanes = asset.Lanes;

            if (fromLane < 0 || fromLane >= lanes.Length || toLane < 0 || toLane >= lanes.Length)
            {
                return false;
            }

            if (fromLane == toLane)
            {
                route.Add(fromLane);
                return true;
            }

            best.Clear();
            cameFrom.Clear();
            estimated.Clear();
            open.Clear();

            float fastest = FastestSpeed(asset);

            // Only the part still ahead, at the limits posted along it.
            best[fromLane] = StreetLaneGeometry.TravelTime(asset.Samples, lanes[fromLane], fromDistance)
                + TurnPenalty(lanes[fromLane].turn);
            estimated[fromLane] = best[fromLane] + Heuristic(asset, fromLane, toLane, fastest);
            open.Add(fromLane);

            int visits = 0;

            while (open.Count > 0 && visits++ < MaximumVisits)
            {
                int current = TakeCheapest();

                if (current == toLane)
                {
                    Reconstruct(current, fromLane, route);
                    return true;
                }

                asset.ExitsOf(current, exits);

                for (int index = 0; index < exits.Count; index++)
                {
                    int next = exits[index];
                    if (next < 0 || next >= lanes.Length)
                    {
                        continue;
                    }

                    float cost = best[current] + TravelTime(lanes[next]);

                    if (best.TryGetValue(next, out float known) && known <= cost)
                    {
                        continue;
                    }

                    best[next] = cost;
                    cameFrom[next] = current;
                    estimated[next] = cost + Heuristic(asset, next, toLane, fastest);

                    if (!open.Contains(next))
                    {
                        open.Add(next);
                    }
                }
            }

            return false;
        }

        /// <summary>Seconds to drive a whole lane at its posted limits, turn included.</summary>
        private static float TravelTime(in StreetNetworkLane lane)
        {
            // An asset baked before travel times existed carries zero here, and charged as-is every
            // lane would be free. Length over the lane's limit is what such an asset was searched by.
            float time = lane.travelTime > 0f
                ? lane.travelTime
                : lane.length / Mathf.Max(lane.speedLimit, StreetProfile.MinimumSpeedLimit);

            return time + TurnPenalty(lane.turn);
        }

        /// <summary>
        /// Straight line from the start of one lane to the start of the other, divided by the fastest
        /// speed anywhere on the network. Dividing by the fastest keeps it from ever overestimating,
        /// which is what makes the result the actual cheapest route rather than merely a route.
        /// </summary>
        private static float Heuristic(StreetNetworkAsset asset, int fromLane, int toLane, float fastest)
        {
            StreetNetworkSample[] samples = asset.Samples;
            StreetNetworkLane from = asset.Lanes[fromLane];
            StreetNetworkLane to = asset.Lanes[toLane];

            if (from.sampleCount <= 0 || to.sampleCount <= 0)
            {
                return 0f;
            }

            float straight = Vector3.Distance(
                samples[from.firstSample].position,
                samples[to.firstSample].position);

            return straight / fastest;
        }

        /// <summary>
        /// Highest speed limit anywhere on the network. Measured once per search rather than per
        /// estimate - it is a property of the map, not of the pair being compared.
        /// </summary>
        private static float FastestSpeed(StreetNetworkAsset asset)
        {
            float fastest = StreetProfile.MinimumSpeedLimit;

            for (int index = 0; index < asset.Lanes.Length; index++)
            {
                fastest = Mathf.Max(fastest, asset.Lanes[index].speedLimit);
            }

            return fastest;
        }

        private static int TakeCheapest()
        {
            int bestIndex = 0;
            float bestCost = float.PositiveInfinity;

            for (int index = 0; index < open.Count; index++)
            {
                float cost = estimated[open[index]];
                if (cost < bestCost)
                {
                    bestCost = cost;
                    bestIndex = index;
                }
            }

            int lane = open[bestIndex];
            open.RemoveAt(bestIndex);
            return lane;
        }

        private static void Reconstruct(int goal, int start, List<int> route)
        {
            route.Clear();
            int current = goal;

            while (current != start)
            {
                route.Add(current);
                current = cameFrom[current];
            }

            route.Add(start);
            route.Reverse();
        }
    }
}
