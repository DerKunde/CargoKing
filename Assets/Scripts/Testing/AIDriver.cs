using CargoKing.Car;
using CargoKing.Driving;
using CargoKing.Input;
using R3;
using UnityEngine;

namespace CargoKing.Testing
{
    /// <summary>
    /// Test driver chasing a world point set by the mouse. Launch sequence and reverse manoeuvre live in
    /// <see cref="CarManeuvers"/>, shared with the route-following VehicleAgent.
    /// </summary>
    public class AIDriver : MonoBehaviour
    {
        private const float DrivingThrottle = 0.2f;

        private CarController carController;
        private CarManeuvers maneuvers;

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

        private Vector3 target;
        private MouseToFloorPositioning targetProvider;

        private void Awake()
        {
            targetProvider = FindFirstObjectByType<MouseToFloorPositioning>();
            carController = GetComponent<CarController>();
            maneuvers = new CarManeuvers(carController);

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
                maneuvers.ResetToForward();
                carController.Drive(maneuvers.StopForGearChange(CarManeuvers.FirstGear));
                return;
            }

            float angleToDirection = Vector3.SignedAngle(flatForward, dirToMovePosition, Vector3.up);

            switch (maneuvers.Advance(target, reverseExitMargin, maxReverseDuration, reverseCooldown))
            {
                case ManeuverState.Forward:
                    DriveForward(distanceToTarget, angleToDirection);
                    break;
                case ManeuverState.StoppingToReverse:
                    carController.Drive(maneuvers.StopForGearChange(CarManeuvers.ReverseGear));
                    break;
                case ManeuverState.Reversing:
                    carController.Drive(maneuvers.DriveInReverse(CalculateNeededSteeringInput(angleToDirection), DrivingThrottle));
                    break;
                case ManeuverState.StoppingToForward:
                    carController.Drive(maneuvers.StopForGearChange(CarManeuvers.FirstGear));
                    break;
            }
        }

        private void DriveForward(float distanceToTarget, float angleToDirection)
        {
            float steerInput = CalculateNeededSteeringInput(angleToDirection);

            if (maneuvers.TryLaunch(steerInput, out DrivingInput launchInput))
            {
                carController.Drive(launchInput);
                return;
            }

            float throttleInput = DrivingThrottle;
            float brakeInput = 0f;

            if (distanceToTarget <= CalculateBrakingDistance(carController.CarSpeedInMS()))
            {
                brakeInput = 1f;
                throttleInput = 0f;
            }

            carController.Drive(new DrivingInput(steerInput, throttleInput, brakeInput, false, GearShift.None, false, false));
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
}
