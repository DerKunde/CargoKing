using UnityEditor;
using UnityEngine;

namespace CargoKing.Streets.Editor
{
    /// <summary>
    /// Creating and moving speed signs on a street's slots. Everything that puts a sign somewhere goes
    /// through here, so a sign always stands on a slot of a street.
    /// </summary>
    public static class StreetSignPlacement
    {
        private const string LimitPreferenceKey = "CargoKing.Streets.LastSignLimit";
        private const float DefaultLimitKmh = 50f;
        private const float MinimumLimitKmh = 5f;

        /// <summary>Two distances closer than this are the same slot, in metres.</summary>
        private const float SlotTolerance = 0.01f;

        /// <summary>
        /// The limit a new sign gets: the one last placed or edited, 50 km/h the first time. Signs come
        /// in runs of the same number, so re-typing it for every one would be the tedious part.
        /// </summary>
        public static float LastLimitKmh
        {
            get => EditorPrefs.GetFloat(LimitPreferenceKey, DefaultLimitKmh);
            set => EditorPrefs.SetFloat(LimitPreferenceKey, Mathf.Max(value, MinimumLimitKmh));
        }

        /// <summary>Puts a new sign on a slot of a street, with Undo. The distance is snapped to the grid.</summary>
        public static StreetSpeedSign Create(StreetSegment segment, StreetSide side, float distance, float limitKmh)
        {
            GameObject gameObject = new GameObject($"Speed Sign {limitKmh:0}");
            Undo.RegisterCreatedObjectUndo(gameObject, "Place Speed Sign");
            gameObject.transform.SetParent(segment.transform, false);

            StreetSpeedSign sign = gameObject.AddComponent<StreetSpeedSign>();
            sign.limitKmh = limitKmh;
            sign.side = side;
            sign.distance = StreetSignSlots.Snap(distance, segment.CentreLineLength);

            segment.Rebuild();
            return sign;
        }

        /// <summary>Whether no sign of this street stands on that slot, not counting the one given.</summary>
        public static bool IsFree(StreetSegment segment, StreetSide side, float distance, StreetSpeedSign ignore = null)
        {
            Transform transform = segment.transform;

            for (int index = 0; index < transform.childCount; index++)
            {
                StreetSpeedSign sign = transform.GetChild(index).GetComponent<StreetSpeedSign>();

                if (sign != null && sign != ignore && sign.side == side
                    && Mathf.Abs(sign.distance - distance) < SlotTolerance)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>The slot of this street nearest a world point, snapped to the grid.</summary>
        public static void NearestSlot(StreetSegment segment, Vector3 worldPoint, out StreetSide side, out float distance)
        {
            segment.LocateSign(worldPoint, out side, out float along, out _);
            distance = StreetSignSlots.Snap(along, segment.CentreLineLength);
        }

        /// <summary>Moves a sign to another slot of its own street, with Undo.</summary>
        /// <returns>False when it already stands there, the slot is taken, or it is on no street.</returns>
        public static bool MoveTo(StreetSpeedSign sign, StreetSide side, float distance)
        {
            StreetSegment segment = sign.Segment;
            if (segment == null)
            {
                return false;
            }

            if (sign.side == side && Mathf.Abs(sign.distance - distance) < SlotTolerance)
            {
                return false;
            }

            if (!IsFree(segment, side, distance, sign))
            {
                return false;
            }

            Undo.RecordObject(sign, "Move Speed Sign");
            sign.side = side;
            sign.distance = distance;
            EditorUtility.SetDirty(sign);

            segment.Rebuild();
            return true;
        }
    }
}
