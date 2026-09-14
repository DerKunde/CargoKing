using UnityEngine;

namespace CargoKing.Traffic
{
    /// <summary>
    /// Every number that makes one kind of driver: how close to the limit, how hard into a bend, how
    /// early on the brakes, when to change gear. The pipeline is the same for every driver; only this
    /// differs.
    ///
    /// The built-in values are the traffic profile. They are starting points for tuning, not results,
    /// and live in an asset so they can be turned in play mode.
    /// </summary>
    [CreateAssetMenu(menuName = "CargoKing/Driving Profile", fileName = "DrivingProfile")]
    public class DrivingProfile : ScriptableObject
    {
        [Header("Speed")]
        [Tooltip("Share of the posted limit this driver aims for.")]
        public float speedLimitFactor = 0.95f;

        [Tooltip("Lateral acceleration this driver accepts in a bend, in g.")]
        public float maxLateralAccelerationG = 0.35f;

        [Tooltip("Deceleration this driver plans its braking with, m/s². Never more than the car's brakes give.")]
        public float comfortDeceleration = 3f;

        [Tooltip("Road looked at beyond the braking distance, metres.")]
        public float extraLookDistance = 10f;

        [Tooltip("Spacing of the probes along the route ahead, metres. Smaller misses less, costs more.")]
        public float probeStep = 2f;

        [Header("Steering")]
        [Tooltip("The steering aims this many seconds of driving ahead.")]
        public float lookAheadTime = 1.1f;

        [Tooltip("Shortest look-ahead, metres. Below this the steering chases the road under the bonnet.")]
        public float minimumLookAhead = 4f;

        [Tooltip("Longest look-ahead, metres. Beyond this the steering cuts corners.")]
        public float maximumLookAhead = 30f;

        [Header("Pedals")]
        [Tooltip("Speed error, m/s, inside which the driver neither accelerates nor brakes.")]
        public float speedDeadBand = 0.5f;

        [Tooltip("Throttle per m/s of speed missing.")]
        public float throttleGain = 0.5f;

        [Tooltip("Brake per m/s of speed too much.")]
        public float brakeGain = 0.3f;

        [Header("Gears")]
        [Tooltip("Shift up at this engine speed under throttle.")]
        public float upshiftRpm = 4000f;

        [Tooltip("Shift down below this engine speed.")]
        public float downshiftRpm = 1500f;

        [Tooltip("Seconds after a shift before the next upshift. Downshifts do not wait.")]
        public float minimumShiftInterval = 1f;

        [Tooltip("Below this speed, when not accelerating, the clutch is held so the engine cannot stall.")]
        public float clutchInSpeedKmh = 10f;

        [Header("Manoeuvres")]
        [Tooltip("Throttle while backing up.")]
        public float reverseThrottle = 0.2f;

        /// <summary>The lateral budget in m/s².</summary>
        public float MaxLateralAcceleration => maxLateralAccelerationG * DrivingMath.Gravity;

        /// <summary>The clutch-in speed in m/s.</summary>
        public float ClutchInSpeed => clutchInSpeedKmh / 3.6f;
    }
}
