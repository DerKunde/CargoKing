using CargoKing.Input;
using CargoKing.UI;
using UnityEngine;

namespace CargoKing.Car
{
    /// <summary>
    /// Owns the car's physics step. Runs after the drivers (PlayerDriver, AIDriver call
    /// <see cref="Drive"/> from their own FixedUpdate), so each step works with this step's input.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class CarController : MonoBehaviour
    {
        public Rigidbody carBody;
        public CarEngine carEngine;
        public Transform frontLeftWheel;
        public Transform frontRightWheel;
        public Transform rearLeftWheel;
        public Transform rearRightWheel;

        public float maxSteerAngle = 25f;

        [Header("Curves")]
        public AnimationCurve steeringInputCurve;

        [Header("Balance")]
        [Tooltip("Set the centre of mass explicitly instead of letting Unity derive it from the colliders.")]
        public bool overrideCenterOfMass = true;

        /// <summary>
        /// Centre of mass in body local space. Height is what decides whether a wheel lifts: the inside
        /// pair leaves the road at a lateral acceleration of g * track / (2 * height above ground), so
        /// halving the height doubles the margin. Unity's implicit centre sits wherever the colliders
        /// happen to average out, which for a body box is far too high.
        /// </summary>
        [Tooltip("Centre of mass in body local space. Lower means less roll and later wheel lift.")]
        public Vector3 centerOfMass = Vector3.zero;

        /// <summary>
        /// Anti roll bar strength in newtons per unit of difference in strut extension between the two
        /// wheels of an axle.
        ///
        /// Worth being clear about what this does and does not do: it takes the lean out of the body, but
        /// it does not stop a wheel lifting - it raises the load transfer at the axle it stiffens. That is
        /// also why the front bar is the stronger one here: more load transfer at the front makes the car
        /// run wide rather than snap round, which is the forgiving way for a delivery vehicle to fail.
        /// </summary>
        [Header("Anti Roll")]
        public float frontAntiRollStiffness = 2500f;

        public float rearAntiRollStiffness = 1200f;

        /// <summary>
        /// How many times per physics step engine, clutch and wheel spin are advanced. The tire's
        /// longitudinal force is stiff against the wheel's small inertia; one 50 Hz step would
        /// either oscillate or need a cap so strong the Pacejka curve never showed. 20 matched a
        /// 400-sub-step reference to 0.01% of stopping distance in the quarter-car sweep, and is
        /// where the wheel-side cap stops mattering; 10 was still within 0.5%.
        /// </summary>
        [Header("Drivetrain")]
        [Min(1)]
        public int drivetrainSubSteps = 20;

        private Suspension[] brakedWheels;
        private Suspension[] wheels;
        private Suspension frontLeftSuspension;
        private Suspension frontRightSuspension;
        private Suspension rearLeftSuspension;
        private Suspension rearRightSuspension;

        // Stored by Drive(), consumed by the next physics step.
        private float throttleInput;
        private bool clutchInput;
        private bool restartEngineInput;

        private void Awake()
        {
            // Looked up once instead of on every physics step.
            frontLeftSuspension = frontLeftWheel.GetComponent<Suspension>();
            frontRightSuspension = frontRightWheel.GetComponent<Suspension>();
            rearLeftSuspension = rearLeftWheel.GetComponent<Suspension>();
            rearRightSuspension = rearRightWheel.GetComponent<Suspension>();

            brakedWheels = new[]
            {
                frontLeftSuspension,
                frontRightSuspension,
                rearLeftSuspension,
                rearRightSuspension,
            };

            wheels = new[]
            {
                frontLeftSuspension,
                frontRightSuspension,
                rearLeftSuspension,
                rearRightSuspension,
            };

            ApplyCenterOfMass();
        }

        private void OnValidate()
        {
            ApplyCenterOfMass();
        }

        /// <summary>
        /// The whole physics step, in an order that used to be left to Unity: contact first, so
        /// the anti roll bars and the tires see this step's suspension; then the drivetrain in
        /// sub-steps with the body's velocity frozen; then each tire's averaged force onto the body.
        /// </summary>
        private void FixedUpdate()
        {
            float deltaTime = Time.fixedDeltaTime;

            foreach (Suspension wheel in wheels)
            {
                wheel.UpdateContact(deltaTime);
            }

            // Separate from Drive(): the bars work off suspension travel, not off driver input, and have
            // to keep working while nobody is steering.
            ApplyAntiRoll(frontLeftSuspension, frontRightSuspension, frontAntiRollStiffness);
            ApplyAntiRoll(rearLeftSuspension, rearRightSuspension, rearAntiRollStiffness);

            StepDrivetrain(deltaTime);

            foreach (Suspension wheel in wheels)
            {
                wheel.ApplyTireForce();
            }

            PlotDrivetrain();
        }

        /// <summary>
        /// Engine, clutch, open differential and all four wheels, advanced together in
        /// <see cref="drivetrainSubSteps"/> sub-steps. Each sub-step evaluates the tires first, so
        /// the clutch holds engine and driven wheels together against the road's reaction from the
        /// same sub-step - the one-step lag this coupling used to carry is gone.
        /// </summary>
        private void StepDrivetrain(float deltaTime)
        {
            int subSteps = Mathf.Max(1, drivetrainSubSteps);
            float subStepDeltaTime = deltaTime / subSteps;

            for (int i = 0; i < subSteps; i++)
            {
                foreach (Suspension wheel in wheels)
                {
                    wheel.EvaluateTire(subStepDeltaTime);
                }

                DrivelineLoad driveline = DriveTrainMath.OpenDifferentialLoad(
                    rearLeftSuspension.wheelAngularVelocity, rearRightSuspension.wheelAngularVelocity,
                    rearLeftSuspension.WheelInertia, rearRightSuspension.WheelInertia,
                    rearLeftSuspension.ExternalTorque, rearRightSuspension.ExternalTorque);

                float carrierTorque = carEngine.Tick(throttleInput, driveline, clutchInput, restartEngineInput, subStepDeltaTime);
                float wheelTorque = DriveTrainMath.OpenDifferentialWheelTorque(carrierTorque);
                rearLeftSuspension.driveTorque = wheelTorque;
                rearRightSuspension.driveTorque = wheelTorque;

                foreach (Suspension wheel in wheels)
                {
                    wheel.IntegrateWheel(subStepDeltaTime);
                }
            }
        }

        private void ApplyCenterOfMass()
        {
            if (carBody == null)
            {
                return;
            }

            if (overrideCenterOfMass)
            {
                carBody.centerOfMass = centerOfMass;
            }
            else
            {
                carBody.automaticCenterOfMass = true;
            }
        }

        /// <summary>
        /// Applies the moment of one anti roll bar: the more the two wheels of an axle have parted, the
        /// harder it pushes the compressed side up and the extended side down.
        /// </summary>
        private void ApplyAntiRoll(Suspension left, Suspension right, float stiffness)
        {
            if (left == null || right == null || stiffness <= 0f)
            {
                return;
            }

            float force = (left.ExtensionRatio - right.ExtensionRatio) * stiffness;

            // Only a wheel on the ground has anything to push against. A bar with one wheel in the air
            // would otherwise shove the body around with nothing carrying the reaction.
            if (left.isGrounded)
            {
                carBody.AddForceAtPosition(left.transform.up * -force, left.transform.position);
            }

            if (right.isGrounded)
            {
                carBody.AddForceAtPosition(right.transform.up * force, right.transform.position);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!overrideCenterOfMass)
            {
                return;
            }

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.TransformPoint(centerOfMass), 0.08f);
        }

        /// <summary>
        /// Takes this step's driver commands. Steering, gear and brake go to their targets right
        /// away; throttle, clutch and restart are stored for <see cref="StepDrivetrain"/>, which
        /// runs in this component's FixedUpdate after the drivers.
        /// </summary>
        public void Drive(in DrivingInput input)
        {
            PlotDriverInput(input);

            ApplyGearShift(input.Shift);
            throttleInput = input.Throttle;
            clutchInput = input.Clutch;
            restartEngineInput = input.RestartEngine;
            ApplySteering(input.Steer);
            ApplyBraking(input.Brake);

            carEngine.speedInKmH = Vector3.Dot(carBody.linearVelocity, transform.forward) * 3.6f;

            // Speed and gear alongside the drivetrain values: without them a telemetry log cannot
            // be read back at all, because there is no way to find which stretch of it was the
            // manoeuvre in question.
            DebugGraph.Plot("Vehicle", "Speed kmh", carEngine.speedInKmH, -50f, 200f);
            DebugGraph.Plot("Vehicle", "Gear", carEngine.currentGear, 0f, 5f);
        }

        private void PlotDrivetrain()
        {
            DebugGraph.Plot("Throttle", throttleInput, 0f, 1f);
            DebugGraph.Plot("Drivetrain", "Engine RPM", carEngine.revolutionsPerMinute, 0f, carEngine.maxRevolutions);
            DebugGraph.Plot("Drivetrain", "Clutch Reaction Torque", carEngine.debugClutchReactionTorque, -230f, 230f);
            DebugGraph.Plot("Drivetrain", "Combustion Torque", carEngine.debugCombustionTorque, -50f, 300f);
            DebugGraph.Plot("Drivetrain", "Friction Torque", carEngine.debugFrictionTorque, 0f, 50f);
            DebugGraph.Plot("Drivetrain", "Rear L Wheel RPM", DriveTrainMath.AngularVelocityToRpm(rearLeftSuspension.wheelAngularVelocity), -2000f, 8000f);
            DebugGraph.Plot("Drivetrain", "Rear R Wheel RPM", DriveTrainMath.AngularVelocityToRpm(rearRightSuspension.wheelAngularVelocity), -2000f, 8000f);

            // Reads zero whenever the clutch is closed, because a closed clutch is solved as a
            // rigid constraint. Anything else means it is slipping - so this one line separates
            // "the engine is being dragged down through the clutch" from "the driven wheels
            // themselves are slowing and the engine is faithfully following them".
            DebugGraph.Plot("Drivetrain", "Clutch Gap RPM", carEngine.revolutionsPerMinute - carEngine.gearboxRevolutions, -2000f, 2000f);

            // Whether the launch assist is in charge and what it lets through - so a log shows
            // where a launch ended and whether the clutch was slipping on its take-up or holding.
            DebugGraph.Plot("Drivetrain", "Launch Assist", carEngine.launchAssistActive ? 1f : 0f, 0f, 1f);
            DebugGraph.Plot("Drivetrain", "Clutch Capacity", carEngine.debugClutchCapacity, 0f, 230f);

            // Per wheel, what the tire is doing and how close to its limit: grip usage 1 means the
            // contact patch is at the edge of its friction ellipse, whatever mix of cornering,
            // braking and drive put it there.
            PlotTire("FL", frontLeftSuspension);
            PlotTire("FR", frontRightSuspension);
            PlotTire("RL", rearLeftSuspension);
            PlotTire("RR", rearRightSuspension);
        }

        private static void PlotTire(string label, Suspension wheel)
        {
            DebugGraph.Plot("Tire", $"{label} Slip Ratio", wheel.slipRatio, -1f, 1f);
            DebugGraph.Plot("Tire", $"{label} Slip Angle", wheel.slipAngle, -20f, 20f);
            DebugGraph.Plot("Tire", $"{label} Grip Usage", wheel.gripUsage, 0f, 1.2f);
        }

        private void ApplyGearShift(GearShift shift)
        {
            if(shift != GearShift.None)
            {
                // ChangeGear wants m/s. Not carEngine.speedInKmH: wrong unit, and it is only
                // written further down in Drive(). The RPM jump a shift causes is absorbed by
                // the same continuous clutch coupling as any other slip (CarEngine.Tick) - no
                // separate blend step needed here any more.
                carEngine.ChangeGear(shift, CarSpeedInMS());
            }
        }

        /// <summary>
        /// Every driver command in one place, so a telemetry log always shows what was actually
        /// asked for next to what the car did with it. Taken straight from the input struct rather
        /// than from the individual Apply methods: those transform their value on the way through,
        /// and reading back a transformed input while chasing a bug is how you end up debugging
        /// the wrong end of the chain.
        ///
        /// Clutch in particular is not optional here - "Clutch Gap RPM" is only meaningful while
        /// the clutch is engaged, because a held clutch is supposed to let the two sides drift
        /// apart.
        /// </summary>
        private void PlotDriverInput(in DrivingInput input)
        {
            DebugGraph.Plot("Input", "Steer", input.Steer, -1f, 1f);
            DebugGraph.Plot("Input", "Throttle", input.Throttle, 0f, 1f);
            DebugGraph.Plot("Input", "Brake", input.Brake, 0f, 1f);
            DebugGraph.Plot("Input", "Clutch", input.Clutch ? 1f : 0f, 0f, 1f);
            DebugGraph.Plot("Input", "Handbrake", input.Handbrake ? 1f : 0f, 0f, 1f);
        }

        private void ApplySteering(float steer)
        {
            float steerAngle = maxSteerAngle * steer;
            frontLeftWheel.localEulerAngles = new Vector3(0, steerAngle, 0);
            frontRightWheel.localEulerAngles = new Vector3(0, steerAngle, 0);
        }

        private void ApplyBraking(float brake)
        {
            // Handed over every step, not only while the pedal is down - letting go has to reach
            // the wheels too. Suspension turns it into a torque on the wheel, where radius and
            // spin are known.
            foreach (Suspension wheel in brakedWheels)
            {
                wheel.SetBrakeInput(brake);
            }
        }

        public void ResetCar()
        {
            transform.Translate(0, 0.3f, 0);
            Vector3 currentEuler = transform.localEulerAngles;
            currentEuler.z = 0f;
            transform.localEulerAngles = currentEuler;
        }

        public float MaxBrakeDecelartion()
        {
            float total = 0f;
            foreach(Suspension wheel in brakedWheels)
            {
                total += wheel.AvailableBrakeForce();
            }
            return total / carBody.mass;
        }

        public float CarSpeedInMS()
        {
            return Mathf.Abs(Vector3.Dot(carBody.linearVelocity, transform.forward));
        }
    }
}
