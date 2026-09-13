using System.Collections.Generic;
using UnityEngine;

namespace CargoKing.Streets
{
    /// <summary>
    /// Everything a vehicle asks of the street network, over one baked asset.
    ///
    /// Holds no state about any particular vehicle - a driver keeps its own route and position and
    /// hands them back in. One runtime is shared by every vehicle on the same network.
    /// </summary>
    public class StreetNetworkRuntime
    {
        /// <summary>Rings of cells the projection widens through before giving up.</summary>
        private static readonly float[] SearchRadii = { 1f, 2f, 6f, 20f };

        private readonly StreetNetworkAsset asset;
        private readonly List<int> candidates = new List<int>();
        private readonly List<int> exits = new List<int>();

        public StreetNetworkRuntime(StreetNetworkAsset asset)
        {
            this.asset = asset;
        }

        public StreetNetworkAsset Asset => asset;

        /// <summary>
        /// Finds the closest place on the network to a world point.
        ///
        /// Widens the search until it finds something, so a vehicle that has left the road is still
        /// located rather than reported missing. It searches the whole network as a last resort, which
        /// is expensive - callers that have a previous position should search around that instead.
        /// </summary>
        public bool TryProject(Vector3 point, out StreetRoutePosition position)
        {
            position = StreetRoutePosition.None;

            if (asset == null || asset.IsEmpty)
            {
                return false;
            }

            float cellSize = asset.Grid.IsValid ? asset.Grid.cellSize : StreetNetworkGrid.DefaultCellSize;

            for (int attempt = 0; attempt < SearchRadii.Length; attempt++)
            {
                StreetNetworkGrid.Overlapping(asset.Grid, point, SearchRadii[attempt] * cellSize, candidates);

                if (TryProjectOnto(candidates, point, out position))
                {
                    return true;
                }
            }

            // Nothing anywhere near. A network small enough to have empty rings everywhere is small
            // enough to scan whole.
            candidates.Clear();
            for (int lane = 0; lane < asset.Lanes.Length; lane++)
            {
                candidates.Add(lane);
            }

            return TryProjectOnto(candidates, point, out position);
        }

        /// <summary>
        /// Finds the closest place on the network near a place already known, without searching the
        /// whole map: only the lane itself and the lanes it leads to are considered.
        ///
        /// This is what a driver calls every step. It failing is meaningful - it means the vehicle is
        /// no longer where its route thinks it is, and the driver should fall back to
        /// <see cref="TryProject"/>.
        /// </summary>
        /// <param name="window">How far from the point a lane may be and still count, in metres.</param>
        public bool TryProjectNear(StreetRoutePosition hint, Vector3 point, float window, out StreetRoutePosition position)
        {
            position = StreetRoutePosition.None;

            if (asset == null || asset.IsEmpty || !hint.IsValid || hint.lane >= asset.Lanes.Length)
            {
                return false;
            }

            candidates.Clear();
            candidates.Add(hint.lane);

            asset.ExitsOf(hint.lane, exits);
            for (int index = 0; index < exits.Count; index++)
            {
                candidates.Add(exits[index]);
            }

            if (!TryProjectOnto(candidates, point, out StreetRoutePosition found))
            {
                return false;
            }

            StreetNetworkSample sample = StreetLaneGeometry.SampleAt(
                asset.Samples, asset.Lanes[found.lane], found.distance);

            if (Vector3.Distance(sample.position, point) > window)
            {
                return false;
            }

            position = found;
            return true;
        }

        /// <summary>
        /// Finds a route between two places on the network.
        /// </summary>
        /// <param name="route">Filled with the lanes to drive. Cleared first, empty when there is none.</param>
        public bool TryFindRoute(StreetRoutePosition from, StreetRoutePosition to, List<int> route)
        {
            route.Clear();

            if (asset == null || !from.IsValid || !to.IsValid)
            {
                return false;
            }

            return StreetRouteSearch.TryFind(asset, from.lane, from.distance, to.lane, route);
        }

        /// <summary>
        /// Reads the route a given distance ahead of where the vehicle is now, carrying on across lane
        /// boundaries. This is the point the steering aims at and the curve the speed is chosen for.
        /// </summary>
        /// <returns>False when the position is not on the route at all. Running past the end of the
        /// route is not a failure - the last point of the last lane is returned.</returns>
        public bool TrySampleAhead(
            IReadOnlyList<int> route,
            StreetRoutePosition position,
            float distance,
            out StreetNetworkSample sample)
        {
            sample = default;

            if (asset == null || asset.IsEmpty || route == null || !position.IsValid)
            {
                return false;
            }

            int index = IndexOnRoute(route, position.lane);
            if (index < 0)
            {
                return false;
            }

            float remaining = distance;
            float along = position.distance;

            while (index < route.Count)
            {
                int lane = route[index];
                if (lane < 0 || lane >= asset.Lanes.Length)
                {
                    return false;
                }

                StreetNetworkLane entry = asset.Lanes[lane];
                float left = entry.length - along;

                if (remaining <= left || index == route.Count - 1)
                {
                    sample = StreetLaneGeometry.SampleAt(asset.Samples, entry, along + remaining);
                    return true;
                }

                remaining -= left;
                along = 0f;
                index++;
            }

            return false;
        }

        private static int IndexOnRoute(IReadOnlyList<int> route, int lane)
        {
            for (int index = 0; index < route.Count; index++)
            {
                if (route[index] == lane)
                {
                    return index;
                }
            }

            return -1;
        }

        private bool TryProjectOnto(List<int> lanes, Vector3 point, out StreetRoutePosition position)
        {
            position = StreetRoutePosition.None;

            float best = float.PositiveInfinity;

            for (int index = 0; index < lanes.Count; index++)
            {
                int lane = lanes[index];

                float squared = StreetLaneGeometry.ClosestPoint(
                    asset.Samples, asset.Lanes[lane], point, out float distanceAlongLane);

                if (squared < best)
                {
                    best = squared;
                    position = new StreetRoutePosition { lane = lane, distance = distanceAlongLane };
                }
            }

            return position.IsValid;
        }
    }
}
