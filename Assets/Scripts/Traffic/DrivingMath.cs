using UnityEngine;

namespace CargoKing.Traffic
{
    /// <summary>Throttle and brake for one step, each 0 to 1. Never both at once.</summary>
    public struct Pedals
    {
        public float throttle;
        public float brake;
    }

    /// <summary>
    /// The arithmetic of driving along a road: how fast a bend may be taken, how fast the car may be
    /// now to still slow down in time, where to aim, how hard to press.
    ///
    /// Knows nothing of a car or a scene, so every rule of the driving pipeline is checkable with plain
    /// numbers - the same split DriveTrainMath makes for the drivetrain.
    /// </summary>
    public static class DrivingMath
    {
        public const float Gravity = 9.81f;

        /// <summary>
        /// Highest speed a bend of this radius can be taken at within a lateral acceleration budget, in
        /// m/s. Infinity on a straight.
        /// </summary>
        public static float CorneringSpeed(float maxLateralAcceleration, float radius)
        {
            if (float.IsPositiveInfinity(radius))
            {
                return float.PositiveInfinity;
            }

            return Mathf.Sqrt(Mathf.Max(0f, maxLateralAcceleration * radius));
        }

        /// <summary>
        /// Highest speed the car may have now so that braking at the given deceleration still brings it
        /// down to speedThere over the distance. This is what makes a car brake before a bend rather than
        /// in it.
        /// </summary>
        public static float SpeedToReach(float speedThere, float deceleration, float distance)
        {
            return Mathf.Sqrt(speedThere * speedThere + 2f * deceleration * Mathf.Max(0f, distance));
        }

        /// <summary>How far ahead along the route the steering aims: further the faster the car goes.</summary>
        public static float LookAheadDistance(float speed, float time, float minimum, float maximum)
        {
            return Mathf.Clamp(speed * time, minimum, maximum);
        }

        /// <summary>
        /// Pure pursuit: the steer angle, in degrees, that puts the rear axle on a circle through the
        /// target. Positive steers right.
        /// </summary>
        /// <param name="target">The target in the car's frame, measured from the middle of the rear axle:
        /// x to the right, z forward. Height is ignored.</param>
        public static float PurePursuitSteerAngle(Vector3 target, float wheelbase)
        {
            float lookAhead = Mathf.Sqrt(target.x * target.x + target.z * target.z);
            if (lookAhead < 0.001f)
            {
                return 0f;
            }

            float alpha = Mathf.Atan2(target.x, target.z);
            return Mathf.Atan(2f * wheelbase * Mathf.Sin(alpha) / lookAhead) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// A proportional controller with a dead band: throttle when too slow, brake when too fast,
        /// neither in between. No integral term - it would wind up during clutch work and gear changes.
        /// </summary>
        public static Pedals PedalsFor(float desiredSpeed, float speed, float deadBand, float throttleGain, float brakeGain)
        {
            float error = desiredSpeed - speed;

            if (error > deadBand)
            {
                return new Pedals { throttle = Mathf.Clamp01(error * throttleGain) };
            }

            if (error < -deadBand)
            {
                return new Pedals { brake = Mathf.Clamp01(-error * brakeGain) };
            }

            return default;
        }
    }
}
