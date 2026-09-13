using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CargoKing.Streets.Editor
{
    /// <summary>
    /// Keeps speed signs on their street through operations that cut, join or turn streets around.
    ///
    /// None of those operations moves the road itself, so every sign still belongs where it stood in the
    /// world; only its owner, side and distance may be out of date. So instead of arithmetic per
    /// operation there is one rule: take the positions before the operation, and re-file every sign from
    /// its position afterwards.
    /// </summary>
    public static class StreetSignFiling
    {
        /// <summary>Signs and where they stood, taken before an operation changed anything.</summary>
        public sealed class Snapshot
        {
            internal readonly List<StreetSpeedSign> signs = new List<StreetSpeedSign>();
            internal readonly List<Vector3> positions = new List<Vector3>();

            public int Count => signs.Count;
        }

        /// <summary>Remembers where every sign on these streets stands.</summary>
        public static Snapshot Capture(params StreetSegment[] segments)
        {
            Snapshot snapshot = new Snapshot();

            for (int index = 0; index < segments.Length; index++)
            {
                StreetSegment segment = segments[index];
                if (segment == null)
                {
                    continue;
                }

                Transform transform = segment.transform;

                for (int child = 0; child < transform.childCount; child++)
                {
                    StreetSpeedSign sign = transform.GetChild(child).GetComponent<StreetSpeedSign>();
                    if (sign == null)
                    {
                        continue;
                    }

                    // From the stored side and distance, not the transform: the transform lags behind
                    // until the segment's next rebuild.
                    Vector3 position = segment.TryGetSignSlot(sign.side, sign.distance, out StreetSignSlot slot)
                        ? slot.position
                        : sign.transform.position;

                    snapshot.signs.Add(sign);
                    snapshot.positions.Add(position);
                }
            }

            return snapshot;
        }

        /// <summary>
        /// Files every captured sign under the nearest of these streets, with the side and distance of
        /// where it stood. A sign the road no longer reaches moves to the slot at the end it fell off,
        /// and says so - it is never deleted.
        /// </summary>
        public static void Refile(Snapshot snapshot, params StreetSegment[] segments)
        {
            if (snapshot == null || snapshot.Count == 0)
            {
                // Nothing to file, nothing touched: an operation on a street without signs behaves
                // exactly as it did before signs existed.
                return;
            }

            for (int index = 0; index < snapshot.signs.Count; index++)
            {
                StreetSpeedSign sign = snapshot.signs[index];
                if (sign == null)
                {
                    continue;
                }

                if (!TryFindNearest(segments, snapshot.positions[index], out StreetSegment best,
                        out StreetSide side, out float distance, out bool beyondEnd))
                {
                    continue;
                }

                if (beyondEnd)
                {
                    distance = distance <= 0f ? 0f : StreetSignSlots.LastSlot(best.CentreLineLength);

                    Debug.Log(
                        $"Speed sign '{sign.name}' stood where '{best.name}' no longer runs and moved to "
                        + $"its slot at {distance:0} m.",
                        sign);
                }

                if (sign.transform.parent != best.transform)
                {
                    Undo.SetTransformParent(sign.transform, best.transform, "Refile Speed Signs");
                }

                Undo.RecordObject(sign, "Refile Speed Signs");
                sign.side = side;
                sign.distance = distance;
                EditorUtility.SetDirty(sign);
            }

            for (int index = 0; index < segments.Length; index++)
            {
                if (segments[index] != null)
                {
                    segments[index].Rebuild();
                }
            }
        }

        private static bool TryFindNearest(
            StreetSegment[] segments,
            Vector3 position,
            out StreetSegment best,
            out StreetSide side,
            out float distance,
            out bool beyondEnd)
        {
            best = null;
            side = StreetSide.Right;
            distance = 0f;
            beyondEnd = false;

            float bestAway = float.PositiveInfinity;

            for (int index = 0; index < segments.Length; index++)
            {
                StreetSegment segment = segments[index];
                if (segment == null)
                {
                    continue;
                }

                float away = segment.LocateSign(position, out StreetSide foundSide, out float foundDistance, out bool foundBeyond);

                if (away < bestAway)
                {
                    bestAway = away;
                    best = segment;
                    side = foundSide;
                    distance = foundDistance;
                    beyondEnd = foundBeyond;
                }
            }

            return best != null;
        }
    }
}
