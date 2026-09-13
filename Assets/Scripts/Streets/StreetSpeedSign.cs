using UnityEngine;

namespace CargoKing.Streets
{
    /// <summary>Which side of its street a sign stands on, seen along the spline's own direction.</summary>
    public enum StreetSide
    {
        /// <summary>Right of the spline. Governs the forward lane.</summary>
        Right,

        /// <summary>Left of the spline. Governs the backward lane.</summary>
        Left,
    }

    /// <summary>
    /// A speed limit sign at the roadside, always a child of the <see cref="StreetSegment"/> it stands
    /// on. The segment puts it on its slot at every rebuild, so it follows the road wherever the road
    /// is moved.
    ///
    /// It stores where it stands in the road's own terms - a side and a distance along the centre
    /// line - rather than as a position. That is what keeps it on the road through every edit, and
    /// what makes the lane it governs unambiguous.
    /// </summary>
    // Hidden from Add Component: a sign is made through a street's Place Speed Signs mode, so none
    // ever starts out without a street.
    [AddComponentMenu("")]
    public class StreetSpeedSign : MonoBehaviour
    {
        [Tooltip("Posted limit in km/h.")]
        [Min(5f)]
        public float limitKmh = 50f;

        [Tooltip("Side of the street, seen along the spline. Right governs the forward lane, left the backward one.")]
        public StreetSide side;

        [Tooltip("Metres along the street's centre line from its first knot.")]
        [Min(0f)]
        public float distance;

        /// <summary>The posted limit in metres per second, never zero.</summary>
        public float SpeedLimit => Mathf.Max(limitKmh / 3.6f, StreetProfile.MinimumSpeedLimit);

        /// <summary>The street this sign stands on, or null once it has been moved out from under one.</summary>
        public StreetSegment Segment =>
            transform.parent != null ? transform.parent.GetComponent<StreetSegment>() : null;

        private void OnValidate()
        {
            // Only flagged. OnValidate may not move objects, so the segment moves the sign on its next tick.
            StreetSegment segment = Segment;
            if (segment != null)
            {
                segment.MarkDirty();
            }
        }
    }
}
