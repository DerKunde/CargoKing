using R3;
using UnityEngine;

public class AIDriver : MonoBehaviour
{
    private CarController carController;

    public float reachedTargetDistance = 1f;

    /// <summary>Heading error band in degrees inside which a sign flip is ignored.</summary>
    public float hysteresisThreshold = 0.8f;

    private float lastSign = 0f;

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

        //To far away from Target, keep moving
        if (distanceToTarget > reachedTargetDistance)
        {
            float steerInput = 0f;
            float throttleInput = 0.2f;
            float brakeInput = 0f;
            bool handbrakeInput = false;
            GearShift shiftInput = GearShift.None;
            //Determin when braking to start
            if (distanceToTarget <= CalculateBrakingDistance(carController.CarSpeedInMS()))
            {
                brakeInput = 1f;
                throttleInput = 0f;
            }
            float dot = Vector3.Dot(flatForward, dirToMovePosition);
            if (dot > 0)
            {
                if (carController.carEngine.currentGear == 0)
                {
                    shiftInput = GearShift.Up;
                }
            }
            else
            {
                if (carController.carEngine.currentGear == 1)
                {
                    shiftInput = GearShift.Down;
                }
            }

            // TODO: the wheels snap to the commanded angle in a single physics step;
            // a steering rate limit in CarController would make this less abrupt.
            float angleToDirection = Vector3.SignedAngle(flatForward, dirToMovePosition, Vector3.up);
            steerInput = CalculateNeededSteeringInput(angleToDirection);
            DrivingInput input = new DrivingInput(steerInput, throttleInput, brakeInput, handbrakeInput, shiftInput);
            carController.Drive(input);
        }
        else
        {
            //Target reached -> Stop
            DrivingInput inputToStop = new DrivingInput(0f, 0f, 1f, false, GearShift.None);
            carController.Drive(inputToStop);
        }
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