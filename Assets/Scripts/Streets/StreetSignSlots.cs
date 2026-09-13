using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace CargoKing.Streets
{
    /// <summary>One place a sign can stand beside a street.</summary>
    public struct StreetSignSlot
    {
        /// <summary>Where the sign stands, at the roadside.</summary>
        public Vector3 position;

        /// <summary>The sign's rotation. Its +Z is the way the face points: towards the traffic it governs.</summary>
        public Quaternion rotation;

        /// <summary>The point on the centre line the slot belongs to.</summary>
        public Vector3 centre;
    }

    /// <summary>
    /// The grid of places a speed sign can stand: one slot every <see cref="Spacing"/> metres along the
    /// centre line, on both sides, just outside the carriageway. Everything here works in the spline's
    /// own space; <see cref="StreetSegment"/> converts to the world.
    /// </summary>
    public static class StreetSignSlots
    {
        /// <summary>Metres between two slots along the centre line. A first guess, checked in Unity.</summary>
        public const float Spacing = 5f;

        /// <summary>Metres between the edge of the carriageway and a sign.</summary>
        public const float Clearance = 1f;

        /// <summary>Slack so a length that is a multiple of the spacing keeps its last slot.</summary>
        private const float SlotEpsilon = 0.0001f;

        /// <summary>How far along the road a point has to lie past an end to count as beyond it, in metres.</summary>
        private const float EndTolerance = 0.05f;

        /// <summary>How close a nearest-point parameter has to be to 0 or 1 to count as an end.</summary>
        private const float EndParameterTolerance = 0.001f;

        private const int NearestResolution = 16;
        private const int NearestIterations = 4;

        /// <summary>Distance of the last slot on a road of this length.</summary>
        public static float LastSlot(float length)
        {
            return Mathf.Floor(Mathf.Max(length, 0f) / Spacing + SlotEpsilon) * Spacing;
        }

        /// <summary>How many slots one side of a road of this length has.</summary>
        public static int SlotCount(float length)
        {
            return Mathf.RoundToInt(LastSlot(length) / Spacing) + 1;
        }

        /// <summary>The nearest slot to a distance, never past the road's last one.</summary>
        public static float Snap(float distance, float length)
        {
            return Mathf.Clamp(Mathf.Round(distance / Spacing) * Spacing, 0f, LastSlot(length));
        }

        /// <summary>
        /// The slot for a side and a distance. A distance past the end is placed at the end; the caller
        /// keeps its own number, so a road lengthened again brings the sign back.
        /// </summary>
        public static StreetSignSlot At(ISpline spline, float roadWidth, StreetSide side, float distance)
        {
            float length = spline.GetLength();
            float t = length > 0f ? Mathf.Clamp01(distance / length) : 0f;

            // t is normalised by arc length, so distance over length lands at that distance.
            spline.Evaluate(t, out float3 position, out float3 tangent, out float3 up);
            Quaternion frame = StreetFrame.At(tangent, up);

            Vector3 centre = new Vector3(position.x, position.y, position.z);
            Vector3 forward = frame * Vector3.forward;
            Vector3 right = frame * Vector3.right;

            float offset = roadWidth * 0.5f + Clearance;
            bool onRight = side == StreetSide.Right;

            return new StreetSignSlot
            {
                position = centre + right * (onRight ? offset : -offset),

                // Traffic on the right runs along the spline and meets the sign head on, so the face
                // looks back down it; on the left it is the other way round.
                rotation = Quaternion.LookRotation(onRight ? -forward : forward, frame * Vector3.up),
                centre = centre,
            };
        }

        /// <summary>
        /// Where a point stands relative to the road, not snapped: the side, the distance along the
        /// centre line of the nearest point on it, and whether the point lies past either end.
        /// </summary>
        /// <returns>Distance from the point to the centre line, in the spline's units.</returns>
        public static float Locate(
            ISpline spline,
            Vector3 point,
            out StreetSide side,
            out float distance,
            out bool beyondEnd)
        {
            float away = SplineUtility.GetNearestPoint(
                spline,
                new float3(point.x, point.y, point.z),
                out float3 nearest,
                out float t,
                NearestResolution,
                NearestIterations);

            spline.Evaluate(t, out float3 _, out float3 tangent, out float3 up);
            Quaternion frame = StreetFrame.At(tangent, up);

            Vector3 offset = point - new Vector3(nearest.x, nearest.y, nearest.z);
            float along = Vector3.Dot(offset, frame * Vector3.forward);

            side = Vector3.Dot(offset, frame * Vector3.right) >= 0f ? StreetSide.Right : StreetSide.Left;
            distance = Mathf.Clamp01(t) * spline.GetLength();

            // Beside the road the nearest point lies square to it. Only at an end can the point still
            // lie further along - which means the road no longer reaches it.
            beyondEnd = (t <= EndParameterTolerance && along < -EndTolerance)
                || (t >= 1f - EndParameterTolerance && along > EndTolerance);

            return away;
        }
    }
}
