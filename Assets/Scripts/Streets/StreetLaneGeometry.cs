using UnityEngine;

namespace CargoKing.Streets
{
    /// <summary>
    /// Where something is on a lane, as a lane and a distance along it. Everything else - position,
    /// direction, radius - is read back from the lane with that distance, so this stays two numbers.
    /// </summary>
    public struct StreetRoutePosition
    {
        /// <summary>Index into the network's lane array, or -1 when nothing is meant.</summary>
        public int lane;

        /// <summary>Distance from the start of that lane, in metres.</summary>
        public float distance;

        public bool IsValid => lane >= 0;

        public static StreetRoutePosition None => new StreetRoutePosition { lane = -1, distance = 0f };
    }

    /// <summary>
    /// Polyline maths on a baked lane. Lanes are stored as sampled point sequences rather than as
    /// splines precisely so this can be an array walk instead of a curve evaluation - a route follower
    /// asks for it every fixed step.
    /// </summary>
    public static class StreetLaneGeometry
    {
        /// <summary>
        /// Closest point on the lane to a world point.
        /// </summary>
        /// <returns>Squared distance from the point to the lane, or infinity for an empty lane.</returns>
        public static float ClosestPoint(
            StreetNetworkSample[] samples,
            in StreetNetworkLane lane,
            Vector3 point,
            out float distanceAlongLane)
        {
            distanceAlongLane = 0f;

            if (samples == null || lane.sampleCount <= 0)
            {
                return float.PositiveInfinity;
            }

            if (lane.sampleCount == 1)
            {
                return (samples[lane.firstSample].position - point).sqrMagnitude;
            }

            float best = float.PositiveInfinity;

            for (int index = 0; index < lane.sampleCount - 1; index++)
            {
                StreetNetworkSample from = samples[lane.firstSample + index];
                StreetNetworkSample to = samples[lane.firstSample + index + 1];

                Vector3 along = to.position - from.position;
                float lengthSquared = along.sqrMagnitude;

                float t = lengthSquared > 0f
                    ? Mathf.Clamp01(Vector3.Dot(point - from.position, along) / lengthSquared)
                    : 0f;

                Vector3 candidate = from.position + along * t;
                float squared = (candidate - point).sqrMagnitude;

                if (squared < best)
                {
                    best = squared;
                    distanceAlongLane = Mathf.Lerp(from.distance, to.distance, t);
                }
            }

            return best;
        }

        /// <summary>
        /// The lane's state at a distance along it, interpolated between the two samples around it.
        /// Distances outside the lane are clamped to its ends.
        /// </summary>
        public static StreetNetworkSample SampleAt(
            StreetNetworkSample[] samples,
            in StreetNetworkLane lane,
            float distanceAlongLane)
        {
            if (samples == null || lane.sampleCount <= 0)
            {
                return default;
            }

            if (lane.sampleCount == 1 || distanceAlongLane <= 0f)
            {
                return samples[lane.firstSample];
            }

            int last = lane.firstSample + lane.sampleCount - 1;
            if (distanceAlongLane >= samples[last].distance)
            {
                return samples[last];
            }

            int index = FindSampleBefore(samples, lane, distanceAlongLane);

            StreetNetworkSample from = samples[index];
            StreetNetworkSample to = samples[index + 1];

            float span = to.distance - from.distance;
            float t = span > 0f ? (distanceAlongLane - from.distance) / span : 0f;

            return new StreetNetworkSample
            {
                position = Vector3.Lerp(from.position, to.position, t),
                direction = Vector3.Slerp(from.direction, to.direction, t),
                distance = distanceAlongLane,

                // Not interpolated: a radius runs to infinity on a straight, and lerping towards
                // infinity produces nonsense. The tighter of the two is the safe answer for a speed.
                radius = Mathf.Min(from.radius, to.radius),

                // The limit of the sample at or before this distance. A limit is a step: it changes at
                // a sign, and blending it would slow a car down before it reaches one.
                speedLimit = from.speedLimit,
            };
        }

        /// <summary>
        /// Seconds from a distance along the lane to its end at the posted limits. Each stretch between
        /// two samples is driven at the limit of the sample it starts from.
        /// </summary>
        public static float TravelTime(
            StreetNetworkSample[] samples,
            in StreetNetworkLane lane,
            float fromDistance)
        {
            if (samples == null || lane.sampleCount < 2)
            {
                return 0f;
            }

            float time = 0f;

            for (int index = 0; index < lane.sampleCount - 1; index++)
            {
                StreetNetworkSample from = samples[lane.firstSample + index];
                StreetNetworkSample to = samples[lane.firstSample + index + 1];

                float start = Mathf.Max(from.distance, fromDistance);
                if (to.distance <= start)
                {
                    continue;
                }

                time += (to.distance - start) / Mathf.Max(from.speedLimit, StreetProfile.MinimumSpeedLimit);
            }

            return time;
        }

        /// <summary>Index of the last sample at or before that distance. Binary search - a long lane
        /// has hundreds of samples and this is called every fixed step.</summary>
        private static int FindSampleBefore(
            StreetNetworkSample[] samples,
            in StreetNetworkLane lane,
            float distanceAlongLane)
        {
            int low = lane.firstSample;
            int high = lane.firstSample + lane.sampleCount - 1;

            while (low < high - 1)
            {
                int middle = (low + high) / 2;

                if (samples[middle].distance <= distanceAlongLane)
                {
                    low = middle;
                }
                else
                {
                    high = middle;
                }
            }

            return low;
        }
    }
}
