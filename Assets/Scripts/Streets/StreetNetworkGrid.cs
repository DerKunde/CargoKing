using System.Collections.Generic;
using UnityEngine;

namespace CargoKing.Streets
{
    /// <summary>
    /// A uniform grid over the network in the ground plane. Built once per bake, read every time a
    /// world point has to be turned into a place on a lane.
    ///
    /// Flat and static, like the other builders here: it takes arrays and returns arrays, so it can
    /// be checked without a scene.
    /// </summary>
    public static class StreetNetworkGrid
    {
        /// <summary>Edge length of a cell in metres. Wide enough that a query touches few cells,
        /// narrow enough that a cell holds few lanes.</summary>
        public const float DefaultCellSize = 25f;

        /// <summary>Cells are padded by this much so a query at the very edge still lands inside.</summary>
        private const float BoundsPadding = 1f;

        private static readonly HashSet<long> occupied = new HashSet<long>();
        private static readonly List<long> ordered = new List<long>();

        /// <summary>
        /// Builds the index. Every cell a lane passes through lists that lane exactly once.
        /// </summary>
        public static StreetNetworkGridData Build(
            StreetNetworkSample[] samples,
            StreetNetworkLane[] lanes,
            float cellSize)
        {
            StreetNetworkGridData grid = default;

            if (samples == null || lanes == null || samples.Length == 0 || lanes.Length == 0 || cellSize <= 0f)
            {
                return grid;
            }

            Vector3 minimum = new Vector3(float.MaxValue, 0f, float.MaxValue);
            Vector3 maximum = new Vector3(float.MinValue, 0f, float.MinValue);

            for (int index = 0; index < samples.Length; index++)
            {
                Vector3 position = samples[index].position;
                minimum.x = Mathf.Min(minimum.x, position.x);
                minimum.z = Mathf.Min(minimum.z, position.z);
                maximum.x = Mathf.Max(maximum.x, position.x);
                maximum.z = Mathf.Max(maximum.z, position.z);
            }

            grid.origin = new Vector3(minimum.x - BoundsPadding, 0f, minimum.z - BoundsPadding);
            grid.cellSize = cellSize;
            grid.columns = Mathf.Max(1, Mathf.CeilToInt((maximum.x - minimum.x + BoundsPadding * 2f) / cellSize));
            grid.rows = Mathf.Max(1, Mathf.CeilToInt((maximum.z - minimum.z + BoundsPadding * 2f) / cellSize));

            occupied.Clear();
            ordered.Clear();

            for (int lane = 0; lane < lanes.Length; lane++)
            {
                MarkLane(grid, samples, lanes[lane], lane);
            }

            int cellCount = grid.columns * grid.rows;
            int[] counts = new int[cellCount + 1];

            for (int index = 0; index < ordered.Count; index++)
            {
                counts[CellOf(ordered[index])]++;
            }

            grid.cellStarts = new int[cellCount + 1];
            int running = 0;
            for (int cell = 0; cell < cellCount; cell++)
            {
                grid.cellStarts[cell] = running;
                running += counts[cell];
            }

            grid.cellStarts[cellCount] = running;

            grid.cellLanes = new int[running];
            int[] cursor = new int[cellCount];

            for (int index = 0; index < ordered.Count; index++)
            {
                int cell = CellOf(ordered[index]);
                grid.cellLanes[grid.cellStarts[cell] + cursor[cell]] = LaneOf(ordered[index]);
                cursor[cell]++;
            }

            return grid;
        }

        /// <summary>
        /// Collects every lane whose cells are touched by a circle around the point. The result is a
        /// candidate list, not an answer: a lane in it may still be further away than the radius.
        /// </summary>
        public static void Overlapping(
            in StreetNetworkGridData grid,
            Vector3 point,
            float radius,
            List<int> results)
        {
            results.Clear();

            if (!grid.IsValid)
            {
                return;
            }

            int minimumColumn = Mathf.Clamp(ColumnOf(grid, point.x - radius), 0, grid.columns - 1);
            int maximumColumn = Mathf.Clamp(ColumnOf(grid, point.x + radius), 0, grid.columns - 1);
            int minimumRow = Mathf.Clamp(RowOf(grid, point.z - radius), 0, grid.rows - 1);
            int maximumRow = Mathf.Clamp(RowOf(grid, point.z + radius), 0, grid.rows - 1);

            for (int row = minimumRow; row <= maximumRow; row++)
            {
                for (int column = minimumColumn; column <= maximumColumn; column++)
                {
                    int cell = row * grid.columns + column;

                    for (int index = grid.cellStarts[cell]; index < grid.cellStarts[cell + 1]; index++)
                    {
                        int lane = grid.cellLanes[index];

                        // A lane spanning several of the visited cells would otherwise be tested once
                        // per cell. The list is short, so a scan beats a set.
                        if (!results.Contains(lane))
                        {
                            results.Add(lane);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Marks every cell the lane passes through. Walks the line between consecutive samples in
        /// half-cell steps rather than only marking the samples: a straight road is sampled as coarsely
        /// as every 20 m and would otherwise skip cells it plainly runs across.
        /// </summary>
        private static void MarkLane(
            in StreetNetworkGridData grid,
            StreetNetworkSample[] samples,
            StreetNetworkLane lane,
            int laneIndex)
        {
            if (lane.sampleCount <= 0)
            {
                return;
            }

            float step = grid.cellSize * 0.5f;

            for (int index = 0; index < lane.sampleCount; index++)
            {
                Vector3 position = samples[lane.firstSample + index].position;
                Mark(grid, position, laneIndex);

                if (index + 1 >= lane.sampleCount)
                {
                    break;
                }

                Vector3 next = samples[lane.firstSample + index + 1].position;
                float span = Vector3.Distance(position, next);

                for (float walked = step; walked < span; walked += step)
                {
                    Mark(grid, Vector3.Lerp(position, next, walked / span), laneIndex);
                }
            }
        }

        private static void Mark(in StreetNetworkGridData grid, Vector3 position, int lane)
        {
            int column = Mathf.Clamp(ColumnOf(grid, position.x), 0, grid.columns - 1);
            int row = Mathf.Clamp(RowOf(grid, position.z), 0, grid.rows - 1);

            long key = ((long)(row * grid.columns + column) << 32) | (uint)lane;
            if (occupied.Add(key))
            {
                ordered.Add(key);
            }
        }

        private static int ColumnOf(in StreetNetworkGridData grid, float x)
        {
            return Mathf.FloorToInt((x - grid.origin.x) / grid.cellSize);
        }

        private static int RowOf(in StreetNetworkGridData grid, float z)
        {
            return Mathf.FloorToInt((z - grid.origin.z) / grid.cellSize);
        }

        private static int CellOf(long key)
        {
            return (int)(key >> 32);
        }

        private static int LaneOf(long key)
        {
            return (int)(key & 0xFFFFFFFF);
        }
    }
}
