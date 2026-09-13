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
