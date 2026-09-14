using CargoKing.Car;
using CargoKing.Input;
using UnityEngine;

namespace CargoKing.Driving
{
    /// <summary>
    /// A target inside the turning circles cannot be reached going forward, so the car has to
    /// back up. The gearbox only engages reverse near standstill, which is why the two
    /// stopping states exist - they are a required step of the manoeuvre, not a courtesy.
    /// </summary>
    public enum ManeuverState
    {
        Forward,
        StoppingToReverse,
        Reversing,
        StoppingToForward,
    }

    /// <summary>
    /// What every AI driver needs to get a car moving and out of a tight spot: the launch sequence,
    /// stopping for a gear change, the turning-circle test and the reverse manoeuvre.
    ///
    /// Moved out of AIDriver so the route-following agent shares the behaviour already tuned against
    /// this drivetrain instead of a copy that drifts away from it. Hands back inputs rather than driving,
    /// so the caller stays the one place that talks to the car.
    /// </summary>
    public class CarManeuvers
    {
        public const int ReverseGear = 0;
        public const int FirstGear = 1;

        /// <summary>How long the deterministic launch sequence holds the clutch while ramping throttle.</summary>
        private const float LaunchRevTime = 0.6f;

        /// <summary>Below this speed a launch sequence may (re-)start.</summary>
        private const float LaunchStandstillSpeed = 0.1f;

        /// <summary>Shift only below this fraction of MaxReverseShiftSpeed, so the gearbox does not refuse.</summary>
        private const float ShiftSpeedSafety = 0.8f;

        private readonly CarController car;

        private ManeuverState state = ManeuverState.Forward;
        private float launchTimer = -1f;
        private float reverseStartedAt;
        private float reverseBlockedUntil;
        private float reverseHeldUntil;

        public CarManeuvers(CarController car)
        {
            this.car = car;
        }

        public ManeuverState State => state;

        /// <summary>Middle of the rear axle in world space - where pure pursuit and the turning circles measure from.</summary>
        public Vector3 RearAxleCenter => (car.rearLeftWheel.position + car.rearRightWheel.position) * 0.5f;

        /// <summary>
        /// Distance between the axles. Read from the wheel transforms rather than hard coded, so it still
        /// holds when scene and prefab disagree.
        /// </summary>
        public float Wheelbase => Vector3.Distance(
            Vector3.ProjectOnPlane(car.frontLeftWheel.localPosition, Vector3.up),
            Vector3.ProjectOnPlane(car.rearLeftWheel.localPosition, Vector3.up));

        public void ResetToForward()
        {
            state = ManeuverState.Forward;
        }

        /// <summary>
        /// Backs up for at least the given time wherever the target is - the way out when the car is
        /// wedged against something rather than facing a target it cannot turn to.
        /// </summary>
        /// <returns>False while already manoeuvring or while reverse is blocked after a given-up attempt.</returns>
        public bool StartReverse(float minimumDuration)
        {
            if (state != ManeuverState.Forward || Time.time < reverseBlockedUntil)
            {
                return false;
            }

            reverseHeldUntil = Time.time + minimumDuration;
            state = ManeuverState.StoppingToReverse;
            return true;
        }

        /// <summary>Moves the manoeuvre on by one step and returns the state to act on.</summary>
        /// <param name="exitMargin">The target counts as reachable again once it clears the turning
        /// circle by this factor.</param>
        /// <param name="maxReverseDuration">Reversing is given up after this long, so a car that is wedged
        /// or not making progress returns to driving forward instead of backing up indefinitely.</param>
        /// <param name="reverseCooldown">After a given-up reverse, drive forward at least this long before
        /// trying again.</param>
        public ManeuverState Advance(Vector3 target, float exitMargin, float maxReverseDuration, float reverseCooldown)
        {
            state = NextState(target, exitMargin, maxReverseDuration, reverseCooldown);
            return state;
        }

        /// <summary>
        /// The single place the manoeuvre state changes. Every method below answers only the question
        /// of what to hand the car this step, never where to go next.
        /// </summary>
        private ManeuverState NextState(Vector3 target, float exitMargin, float maxReverseDuration, float reverseCooldown)
        {
            switch (state)
            {
                case ManeuverState.Forward:
                    bool mayReverse = Time.time >= reverseBlockedUntil;
                    return mayReverse && IsTargetInsideTurningCircle(target, 1f)
                        ? ManeuverState.StoppingToReverse
                        : ManeuverState.Forward;

                case ManeuverState.StoppingToReverse:
                    if (car.carEngine.currentGear != ReverseGear)
                    {
                        return state;
                    }
                    reverseStartedAt = Time.time;
                    return ManeuverState.Reversing;

                case ManeuverState.Reversing:
                    if (!IsTargetInsideTurningCircle(target, exitMargin) && Time.time >= reverseHeldUntil)
                    {
                        return ManeuverState.StoppingToForward;
                    }

                    if (Time.time - reverseStartedAt >= maxReverseDuration)
                    {
                        // Backing up is not opening the geometry up - the car may be wedged. Block
                        // reverse for a while, otherwise the next step would re-enter it straight
                        // away and the car would never actually drive forward.
                        reverseBlockedUntil = Time.time + reverseCooldown;
                        return ManeuverState.StoppingToForward;
                    }

                    return ManeuverState.Reversing;

                case ManeuverState.StoppingToForward:
                    return car.carEngine.currentGear == FirstGear
                        ? ManeuverState.Forward
                        : state;

                default:
                    return ManeuverState.Forward;
            }
        }

        /// <summary>Backing up at a steady throttle, steering so the nose pulls towards the target.</summary>
        /// <param name="forwardSteer">The steer input that would turn towards the target going forward.</param>
        public DrivingInput DriveInReverse(float forwardSteer, float throttle)
        {
            // Yaw rate is (v / wheelbase) * tan(steerAngle), so a negative v turns the car the
            // other way for the same command. Inverting keeps the nose pulling towards the target.
            float steer = -forwardSteer;

            if (TryLaunch(steer, out DrivingInput launchInput))
            {
                return launchInput;
            }

            return new DrivingInput(steer, throttle, 0f, false, GearShift.None, false, false);
        }

        /// <summary>
        /// A deterministic stand-in for the player's rev-and-release launch technique: hold the
        /// clutch, ramp the throttle open over a fixed time, then release. Not meant to be
        /// skillful - just reliable enough to exercise the full drivetrain (Suspension + CarEngine +
        /// CarController) from a standing start.
        /// </summary>
        public bool TryLaunch(float steerInput, out DrivingInput input)
        {
            bool atStandstill = car.CarSpeedInMS() < LaunchStandstillSpeed;

            if (atStandstill && launchTimer < 0f)
            {
                launchTimer = 0f;
            }
            else if (!atStandstill)
            {
                launchTimer = -1f;
            }

            if (launchTimer < 0f)
            {
                input = default;
                return false;
            }

            if (launchTimer >= LaunchRevTime)
            {
                launchTimer = -1f;
                input = default;
                return false;
            }

            launchTimer += Time.fixedDeltaTime;
            float throttle = Mathf.Clamp01(launchTimer / LaunchRevTime);
            input = new DrivingInput(steerInput, throttle, 0f, false, GearShift.None, true, false);
            return true;
        }

        /// <summary>
        /// Full brake, plus one shift towards the wanted gear once slow enough. The gearbox
        /// refuses to engage reverse above CarEngine.MaxReverseShiftSpeed.
        /// </summary>
        public DrivingInput StopForGearChange(int targetGear)
        {
            int currentGear = car.carEngine.currentGear;
            bool slowEnough = car.CarSpeedInMS() < car.carEngine.MaxReverseShiftSpeed * ShiftSpeedSafety;

            GearShift shift = GearShift.None;
            if (slowEnough && currentGear != targetGear)
            {
                shift = currentGear > targetGear ? GearShift.Down : GearShift.Up;
            }

            return new DrivingInput(0f, 0f, 1f, false, shift, false, false);
        }

        public bool IsTargetInsideTurningCircle(Vector3 target, float radiusFactor)
        {
            return IsInsideTurningCircle(RearAxleCenter, car.transform.right, target, MinimumTurningRadius(), radiusFactor);
        }

        /// <summary>
        /// True when no forward path can reach the target. At full lock the car traces one of two
        /// circles that touch its path at the rear axle; their interiors stay out of reach however
        /// long it drives, so only backing up opens the geometry up again.
        /// </summary>
        /// <param name="radiusFactor">Widens the tested radius. Leaving the manoeuvre at a larger
        /// value than the one that started it gives the state machine hysteresis at the boundary.</param>
        public static bool IsInsideTurningCircle(Vector3 rearAxleCenter, Vector3 right, Vector3 target, float turningRadius, float radiusFactor)
        {
            Vector3 flatRight = Vector3.ProjectOnPlane(right, Vector3.up).normalized;
            Vector3 leftCenter = rearAxleCenter - flatRight * turningRadius;
            Vector3 rightCenter = rearAxleCenter + flatRight * turningRadius;
            float reach = turningRadius * radiusFactor;

            return FlatDistance(target, leftCenter) < reach || FlatDistance(target, rightCenter) < reach;
        }

        /// <summary>Bicycle model: wheelbase / tan(steerAngle).</summary>
        public float MinimumTurningRadius()
        {
            return Wheelbase / Mathf.Tan(car.maxSteerAngle * Mathf.Deg2Rad);
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            return Vector3.ProjectOnPlane(a - b, Vector3.up).magnitude;
        }
    }
}
