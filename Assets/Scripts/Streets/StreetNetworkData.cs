using System;
using UnityEngine;

namespace CargoKing.Streets
{
    /// <summary>
    /// One point along a baked lane, in world space.
    ///
    /// The baked twin of <see cref="StreetLaneSample"/>. Separate on purpose: the authoring sample is
    /// local to whatever object produced it and is rebuilt constantly, while this one is world space,
    /// serialised, and never moves after a bake.
    /// </summary>
    [Serializable]
    public struct StreetNetworkSample
    {
        public Vector3 position;

        /// <summary>Unit vector pointing the way traffic moves here.</summary>
        public Vector3 direction;

        /// <summary>Distance from the start of the lane, along the lane.</summary>
        public float distance;

        /// <summary>Curve radius of this lane here in metres, or infinity where it runs straight.</summary>
        public float radius;

        /// <summary>
        /// Speed limit in force from this sample on, in metres per second. A step, not a gradient: it
        /// holds until the next sample and is never interpolated, because a limit changes at a sign.
        /// </summary>
        public float speedLimit;
    }

    /// <summary>
    /// One driveable lane in the baked network: a slice of the shared sample array plus everything a
    /// driver or the route search needs to know about it.
    /// </summary>
    [Serializable]
    public struct StreetNetworkLane
    {
        /// <summary>Index of this lane's first sample in the network's sample array.</summary>
        public int firstSample;

        public int sampleCount;

        /// <summary>Length in metres, measured along this lane rather than along a centre line.</summary>
        public float length;

        /// <summary>
        /// Highest speed limit anywhere on this lane, in metres per second. The route search divides by
        /// the fastest limit on the map for its estimate, so this must never be lower than any limit on
        /// the lane. The limit at a given place is on the samples.
        /// </summary>
        public float speedLimit;

        /// <summary>Seconds to drive the whole lane at its posted limits. No turn penalty included.</summary>
        public float travelTime;

        /// <summary>Index of the intersection this lane crosses, or -1 when it is an ordinary road lane.</summary>
        public int intersection;

        /// <summary>
        /// Bit position of this lane within its own intersection, or -1 for a road lane. Only the
        /// paths of one intersection are ever compared, so five bits' worth is plenty.
        /// </summary>
        public int pathIndex;

        /// <summary>
        /// Bits of the paths of the same intersection whose course conflicts with this one. Two lanes
        /// whose bits overlap must never be occupied at the same time. Computed at bake time so the
        /// runtime check is one AND.
        /// </summary>
        public int crossingMask;

        /// <summary>Index of this lane's first entry in the network's exit array.</summary>
        public int firstExit;

        public int exitCount;

        /// <summary>Which way a vehicle turns along this lane. Always Straight for a road lane.</summary>
        public StreetTurn turn;

        /// <summary>Whether ambient traffic may be placed on this lane.</summary>
        public bool spawnable;
    }

    /// <summary>
    /// A uniform grid over the network in the ground plane, mapping a cell to the lanes that pass
    /// through it. Flat arrays rather than a jagged one, because Unity does not serialise those.
    ///
    /// Cell (column, row) owns the lane indices in cellLanes between cellStarts[cell] and
    /// cellStarts[cell + 1], which is why cellStarts is one longer than the cell count.
    /// </summary>
    [Serializable]
    public struct StreetNetworkGridData
    {
        /// <summary>Corner of cell (0, 0). Only x and z are used.</summary>
        public Vector3 origin;

        public float cellSize;
        public int columns;
        public int rows;

        public int[] cellStarts;
        public int[] cellLanes;

        public bool IsValid => cellSize > 0f && columns > 0 && rows > 0
            && cellStarts != null && cellStarts.Length == columns * rows + 1;
    }
}
