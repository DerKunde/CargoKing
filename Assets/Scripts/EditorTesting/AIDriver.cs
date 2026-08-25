using R3;
using UnityEngine;

public class AIDriver : MonoBehaviour
{
    /// <summary>
    /// A target inside the turning circles cannot be reached going forward, so the car has to
    /// back up. The gearbox only engages reverse near standstill, which is why the two
    /// stopping states exist - they are a required step of the manoeuvre, not a courtesy.
    /// </summary>
    private enum ManeuverState
    {
        Forward,
        StoppingToReverse,
        Reversing,
        StoppingToForward,
    }

    private const int ReverseGear = 0;
    private const int FirstGear = 1;
    private const float DrivingThrottle = 0.2f;

    /// <summary>Shift only below this fraction of maxReverseShiftSpeed, so the gearbox does not refuse.</summary>
    private const float ShiftSpeedSafety = 0.8f;

    private CarController carController;

    public float reachedTargetDistance = 1f;

    /// <summary>Heading error band in degrees inside which a sign flip is ignored.</summary>
    public float hysteresisThreshold = 0.8f;

    /// <summary>The target counts as reachable again once it clears the turning circle by this factor.</summary>
    public float reverseExitMargin = 1.15f;

    /// <summary>Reversing is given up after this long, so a car that is wedged or not making
    /// progress returns to driving forward instead of backing up indefinitely.</summary>
    public float maxReverseDuration = 4f;

    /// <summary>After a given-up reverse, drive forward at least this long before trying again.</summary>
    public float reverseCooldown = 3f;

    private float lastSign = 0f;
    private ManeuverState state = ManeuverState.Forward;
    private float reverseStartedAt;
    private float reverseBlockedUntil;

    private Vector3 target;
    private MouseToFloorPositioning targetProvider;

    private void Awake()
    {
        targetProvider = FindFirstObjectByType<MouseToFloorPositioning>();
        carController = GetComponent<CarController>();

        if (targetProvider != null)
        {
            targetProvider.carAITarget.Subscribe(mouseSetTarget =>
            {
                target = mouseSetTarget;
            }).AddTo(this);
        }
    }

    private void FixedUpdate()
    {
        // Flattened onto the ground plane before any angle is taken: the car origin sits
        // above the floor while the target is a floor hit point, so the raw 3D direction
        // carries a pitch component that would leak into the heading error.
        Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        Vector3 dirToMovePosition = Vector3.ProjectOnPlane(target - transform.position, Vector3.up).normalized;
        float distanceToTarget = Vector3.Distance(transform.position, target);

        if (distanceToTarget <= reachedTargetDistance)
        {
            // Target reached -> stop. Reverse has to be left behind as well, otherwise the
            // next target would be chased in the wrong gear.
            state = ManeuverState.Forward;
            StopForGearChange(FirstGear);
            return;
        }

        float angleToDirection = Vector3.SignedAngle(flatForward, dirToMovePosition, Vector3.up);
        state = NextState();

        switch (state)
        {
            case ManeuverState.Forward:
                DriveForward(distanceToTarget, angleToDirection);
                break;
            case ManeuverState.StoppingToReverse:
                StopForGearChange(ReverseGear);
                break;
            case ManeuverState.Reversing:
                DriveInReverse(angleToDirection);
                break;
            case ManeuverState.StoppingToForward:
                StopForGearChange(FirstGear);
                break;
        }
    }

    /// <summary>
    /// The single place the manoeuvre state changes. Every driving method below answers only
    /// the question of what to hand the car this step, never where to go next.
    /// </summary>
    private ManeuverState NextState()
    {
        switch (state)
        {
            case ManeuverState.Forward:
                bool mayReverse = Time.time >= reverseBlockedUntil;
                return mayReverse && IsTargetInsideTurningCircle(1f)
                    ? ManeuverState.StoppingToReverse
                    : ManeuverState.Forward;

            case ManeuverState.StoppingToReverse:
                if (carController.carEngine.currentGear != ReverseGear)
                {
                    return state;
                }
                reverseStartedAt = Time.time;
                return ManeuverState.Reversing;

            case ManeuverState.Reversing:
                if (!IsTargetInsideTurningCircle(reverseExitMargin))
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
                return carController.carEngine.currentGear == FirstGear
                    ? ManeuverState.Forward
                    : state;

            default:
                return ManeuverState.Forward;
        }
    }

    private void DriveForward(float distanceToTarget, float angleToDirection)
    {
        float throttleInput = DrivingThrottle;
        float brakeInput = 0f;

        //Determin when braking to start
        if (distanceToTarget <= CalculateBrakingDistance(carController.CarSpeedInMS()))
        {
            brakeInput = 1f;
            throttleInput = 0f;
        }

        float steerInput = CalculateNeededSteeringInput(angleToDirection);
        carController.Drive(new DrivingInput(steerInput, throttleInput, brakeInput, false, GearShift.None));
    }

    private void DriveInReverse(float angleToDirection)
    {
        // Yaw rate is (v / wheelbase) * tan(steerAngle), so a negative v turns the car the
        // other way for the same command. Inverting it keeps pulling the nose towards the
        // target while the rear backs away from it - the three-point-turn motion.
        float steerInput = -CalculateNeededSteeringInput(angleToDirection);
        carController.Drive(new DrivingInput(steerInput, DrivingThrottle, 0f, false, GearShift.None));
    }

    /// <summary>
    /// Full brake, plus one shift towards the wanted gear once slow enough. The gearbox
    /// refuses to engage reverse above maxReverseShiftSpeed.
    /// </summary>
    private void StopForGearChange(int targetGear)
    {
        int currentGear = carController.carEngine.currentGear;
        bool slowEnough = carController.CarSpeedInMS() < carController.carEngine.maxReverseShiftSpeed * ShiftSpeedSafety;

        GearShift shift = GearShift.None;
        if (slowEnough && currentGear != targetGear)
        {
            shift = currentGear > targetGear ? GearShift.Down : GearShift.Up;
        }

        carController.Drive(new DrivingInput(0f, 0f, 1f, false, shift));
    }

    private bool IsTargetInsideTurningCircle(float radiusFactor)
    {
        Vector3 rearAxleCenter = (carController.rearLeftWheel.position + carController.rearRightWheel.position) * 0.5f;
        return IsInsideTurningCircle(rearAxleCenter, transform.right, target, MinimumTurningRadius(), radiusFactor);
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

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        return Vector3.ProjectOnPlane(a - b, Vector3.up).magnitude;
    }

    /// <summary>
    /// Bicycle model: wheelbase / tan(steerAngle). Read from the wheel transforms rather than
    /// hard coded, so it still holds when scene and prefab disagree.
    /// </summary>
    private float MinimumTurningRadius()
    {
        float wheelbase = Vector3.Distance(
            Vector3.ProjectOnPlane(carController.frontLeftWheel.localPosition, Vector3.up),
            Vector3.ProjectOnPlane(carController.rearLeftWheel.localPosition, Vector3.up));

        return wheelbase / Mathf.Tan(carController.maxSteerAngle * Mathf.Deg2Rad);
    }

    private float CalculateBrakingDistance(float speedInMS)
    {
        float brakeDecelartion = carController.MaxBrakeDecelartion();
        float brakingDistance = Mathf.Pow(speedInMS, 2) / (2 * brakeDecelartion);
        return brakingDistance;
    }

    /// <summary>
    /// Proportional steering command from the heading error. A hysteresis band around zero
    /// suppresses sign flips of a near-zero error, so the wheels do not chatter left/right.
    /// </summary>
    private float CalculateNeededSteeringInput(float angleToDirection)
    {
        float currentSign = Mathf.Sign(angleToDirection);

        // Only the flip is blocked: a same-sign error inside the band still steers, and any
        // error outside the band passes through untouched.
        if (Mathf.Abs(angleToDirection) < hysteresisThreshold && currentSign != lastSign)
        {
            return 0f;
        }

        lastSign = currentSign;
        return Mathf.Clamp(angleToDirection / carController.maxSteerAngle, -1f, 1f);
    }
}