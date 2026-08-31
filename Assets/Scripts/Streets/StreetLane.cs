using UnityEngine;

namespace CargoKing.Streets
{
    /// <summary>
    /// Which way a lane runs relative to the spline that produced it.
    /// </summary>
    public enum StreetLaneDirection
    {
        /// <summary>Runs along the spline, from its first knot towards its last.</summary>
        Forward,

        /// <summary>Runs against the spline. Its samples are stored in its own driving order.</summary>
        Backward,
    }

    /// <summary>
    /// One point along a lane, in the local space of the segment that owns it.
    /// </summary>
    public struct StreetLaneSample
    {
        /// <summary>Centre of the lane at this point.</summary>
        public Vector3 position;

        /// <summary>Unit vector pointing the way traffic moves here.</summary>
        public Vector3 direction;

        /// <summary>Distance from the start of this lane, measured along the lane itself.</summary>
        public float distance;

        /// <summary>
        /// Curve radius of this lane here, in metres, or infinity where it runs straight. This is the
        /// lane's own radius, not the centre line's - the inner lane of a bend is the tighter one,
        /// and that difference is what a cornering speed has to be based on.
        /// </summary>
        public float radius;
    }

    /// <summary>
    /// A single driveable path along one street segment: the centre line, offset sideways and given
    /// a direction. Two of them make up a road with one lane each way.
    /// </summary>
    public class StreetLane
    {
        public StreetLane(StreetLaneDirection direction, StreetLaneSample[] samples)
        {
            this.direction = direction;
            this.samples = samples;
        }

        private readonly StreetLaneDirection direction;
        private readonly StreetLaneSample[] samples;

        public StreetLaneDirection Direction => direction;

        public StreetLaneSample[] Samples => samples;

        /// <summary>Length of the lane in metres, along the lane rather than the centre line.</summary>
        public float Length => samples.Length > 0 ? samples[samples.Length - 1].distance : 0f;
    }
}
