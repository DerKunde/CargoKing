using System;
using UnityEngine;

namespace CargoKing.Streets
{
    /// <summary>Which end of a street segment is meant.</summary>
    public enum StreetEnd
    {
        Start,
        End,
    }

    /// <summary>
    /// One end of a street segment and what it docks to. Three states: open, docked to an intersection
    /// socket, docked to an end of another segment.
    ///
    /// What is stored is the reference, never a position. That is what lets a moved intersection keep
    /// its streets instead of silently losing them.
    ///
    /// Sockets stay passive - only segments record connections. An intersection is a prefab, and having
    /// every arriving street write scene references back into it is the kind of two-way bookkeeping
    /// that goes out of sync the first time something is deleted.
    /// </summary>
    [Serializable]
    public class StreetEndConnector
    {
        [Tooltip("Intersection socket this end docks to.")]
        public IntersectionSocket socket;

        [Tooltip("Other street segment this end docks to.")]
        public StreetSegment segment;

        [Tooltip("Which end of the other segment this docks to.")]
        public StreetEnd segmentEnd = StreetEnd.Start;

        /// <summary>
        /// Whether this end takes its position from its counterpart. Exactly one side of a seam is
        /// driven; if both were, they would chase each other every frame.
        /// </summary>
        [Tooltip("This end is positioned from its counterpart. Exactly one side of a seam drives.")]
        public bool driven = true;

        public bool IsConnected => socket != null || segment != null;

        public void Clear()
        {
            socket = null;
            segment = null;
        }
    }

    /// <summary>
    /// A lane that carries on where another one ends, together with the transform its samples are
    /// local to.
    /// </summary>
    public struct StreetContinuation
    {
        /// <summary>Transform the lane's samples are expressed in.</summary>
        public Transform space;

        public StreetLane lane;
    }
}
