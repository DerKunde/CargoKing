using System.Collections.Generic;
using CargoKing.Streets;
using UnityEngine;

namespace CargoKing.Traffic
{
    /// <summary>
    /// How fast the car may be going now, from what lies ahead on its route.
    ///
    /// Not the cornering speed here but the lowest speed anything within braking distance allows,
    /// carried back to the car's position - otherwise every car would brake inside the bend. Reads the
    /// route by probing at a fixed step; a stretch shorter than the step can fall between two probes,
    /// which is the accepted cost of not reading every baked sample.
    /// </summary>
    public static class SpeedPlanner
    {
        /// <summary>
        /// The speed allowed now, in m/s.
        /// </summary>
        /// <param name="route">The route, with the lane the car is on first.</param>
        /// <param name="speed">Current speed, which sets how far ahead is looked at.</param>
        /// <param name="distanceToGoal">Metres along the route to where the car has to stop, or infinity.</param>
        /// <param name="deceleration">Braking the plan counts on, m/s² - the profile's, capped by the car's.</param>
        /// <returns>Zero when the position is not on the route: a car that does not know where it is
        /// should not be told to go.</returns>
        public static float AllowedSpeed(
            StreetNetworkRuntime runtime,
            IReadOnlyList<int> route,
            StreetRoutePosition position,
            float speed,
            float distanceToGoal,
            DrivingProfile profile,
            float deceleration)
        {
            if (runtime == null || profile == null || deceleration <= 0f)
            {
                return 0f;
            }

            float step = Mathf.Max(0.1f, profile.probeStep);
            float horizon = speed * speed / (2f * deceleration) + profile.extraLookDistance;
            float lateral = profile.MaxLateralAcceleration;

            float allowed = DrivingMath.SpeedToReach(0f, deceleration, distanceToGoal);

            for (float ahead = 0f; ahead <= horizon && ahead <= distanceToGoal; ahead += step)
            {
                if (!runtime.TrySampleAhead(route, position, ahead, out StreetNetworkSample sample))
                {
                    return 0f;
                }

                float limit = Mathf.Max(sample.speedLimit, StreetProfile.MinimumSpeedLimit) * profile.speedLimitFactor;
                float there = Mathf.Min(limit, DrivingMath.CorneringSpeed(lateral, sample.radius));

                allowed = Mathf.Min(allowed, DrivingMath.SpeedToReach(there, deceleration, ahead));
            }

            return allowed;
        }

        /// <summary>
        /// Metres along the route from the car to a goal on the route's last lane.
        /// </summary>
        /// <param name="route">The route, with the lane the car is on first.</param>
        /// <returns>Never negative; infinity when the route does not start on the car's lane.</returns>
        public static float DistanceToGoal(
            StreetNetworkAsset asset,
            IReadOnlyList<int> route,
            StreetRoutePosition position,
            float goalDistance)
        {
            if (asset == null || route == null || route.Count == 0 || route[0] != position.lane)
            {
                return float.PositiveInfinity;
            }

            if (route.Count == 1)
            {
                return Mathf.Max(0f, goalDistance - position.distance);
            }

            float distance = asset.Lanes[route[0]].length - position.distance;

            for (int index = 1; index < route.Count - 1; index++)
            {
                distance += asset.Lanes[route[index]].length;
            }

            return Mathf.Max(0f, distance + goalDistance);
        }
    }
}
