using System.Collections.Generic;
using UnityEngine;

namespace CargoKing.Streets.Editor
{
    /// <summary>Where a junction goes at a knot, and which of its sockets the two halves dock to.</summary>
    public struct JunctionAlignment
    {
        /// <summary>Socket the half arriving at the knot docks to. Looks back down the road.</summary>
        public IntersectionSocket entry;

        /// <summary>Socket the half leaving the knot docks to.</summary>
        public IntersectionSocket exit;

        /// <summary>
        /// Position of <see cref="entry"/> in the socket list gathered by
        /// <c>GetComponentsInChildren(false, sockets)</c> on the junction's root.
        /// </summary>
        public int entryIndex;

        /// <summary>
        /// Position of <see cref="exit"/> in that same list.
        /// </summary>
        public int exitIndex;

        /// <summary>World position for the junction's root.</summary>
        public Vector3 position;

        /// <summary>World rotation for the junction's root.</summary>
        public Quaternion rotation;

        /// <summary>Distance from the middle of the through pair to either of its sockets, in metres.</summary>
        public float socketOffset;
    }

    /// <summary>
    /// Works out how a junction prefab has to sit to replace a knot on a street.
    ///
    /// Pure arithmetic: it reads the prefab's sockets and returns numbers. Nothing is instantiated,
    /// nothing in the scene is touched, so the part of junction insertion that is easy to get subtly
    /// wrong is also the part that can be tested on its own.
    /// </summary>
    public static class JunctionPlacement
    {
        /// <summary>How nearly opposite two sockets have to look to count as a way through.</summary>
        public const float OpposingThreshold = -0.9f;

        public static bool TryAlign(
            GameObject junction,
            Vector3 knotPosition,
            Vector3 roadDirection,
            Vector3 roadUp,
            bool flipped,
            out JunctionAlignment alignment,
            out string problem)
        {
            alignment = default;
            problem = null;

            if (junction == null)
            {
                problem = "No junction prefab.";
                return false;
            }

            List<IntersectionSocket> sockets = new List<IntersectionSocket>();
            junction.GetComponentsInChildren(false, sockets);

            if (sockets.Count < 2)
            {
                problem = $"'{junction.name}' has fewer than two active sockets.";
                return false;
            }

            Transform root = junction.transform;

            // The way through is the pair that looks most nearly opposite. On a crossing there are two
            // such pairs and they are interchangeable; on a T junction there is exactly one, and the
            // socket left over is the stem.
            int firstIndex = -1;
            int secondIndex = -1;
            float bestDot = OpposingThreshold;

            for (int a = 0; a < sockets.Count; a++)
            {
                Vector3 outwardA = root.InverseTransformDirection(sockets[a].Outward).normalized;

                for (int b = a + 1; b < sockets.Count; b++)
                {
                    Vector3 outwardB = root.InverseTransformDirection(sockets[b].Outward).normalized;
                    float dot = Vector3.Dot(outwardA, outwardB);

                    if (dot < bestDot)
                    {
                        bestDot = dot;
                        firstIndex = a;
                        secondIndex = b;
                    }
                }
            }

            if (firstIndex < 0)
            {
                problem = $"'{junction.name}' has no two sockets facing opposite ways, so no street can "
                    + "pass through it.";
                return false;
            }

            // The pair is reported as list positions as well as references. The references belong to
            // the prefab asset; whoever instantiates it has to dock the copy's sockets, and the only
            // reliable way across is the position in a list gathered the same way on the copy.
            int chosenEntryIndex = flipped ? secondIndex : firstIndex;
            int chosenExitIndex = flipped ? firstIndex : secondIndex;

            IntersectionSocket entry = sockets[chosenEntryIndex];
            IntersectionSocket exit = sockets[chosenExitIndex];

            Vector3 entryPositionLocal = root.InverseTransformPoint(entry.transform.position);
            Vector3 exitPositionLocal = root.InverseTransformPoint(exit.transform.position);
            Vector3 entryOutwardLocal = root.InverseTransformDirection(entry.Outward).normalized;
            Vector3 entryUpLocal = root.InverseTransformDirection(entry.transform.up).normalized;

            // The entry socket has to end up looking back down the road the traffic came from.
            Quaternion rotation = Quaternion.LookRotation(-roadDirection.normalized, roadUp)
                * Quaternion.Inverse(Quaternion.LookRotation(entryOutwardLocal, entryUpLocal));

            Vector3 middleLocal = (entryPositionLocal + exitPositionLocal) * 0.5f;

            alignment = new JunctionAlignment
            {
                entry = entry,
                exit = exit,
                entryIndex = chosenEntryIndex,
                exitIndex = chosenExitIndex,
                rotation = rotation,
                position = knotPosition - rotation * middleLocal,
                socketOffset = Vector3.Distance(entryPositionLocal, middleLocal),
            };

            return true;
        }
    }
}
