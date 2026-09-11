using UnityEngine;

namespace CargoKing.Car
{
    /// <summary>
    /// One wheel: raycast suspension, tire contact patch and wheel spin.
    ///
    /// No FixedUpdate of its own. The drivetrain runs in sub-steps across the engine and all
    /// wheels together, and only one place can own that loop - CarController calls the phases of
    /// each physics step in order: <see cref="UpdateContact"/> once, then
    /// <see cref="EvaluateTire"/> and <see cref="IntegrateWheel"/> once per sub-step, then
    /// <see cref="ApplyTireForce"/> once.
    /// </summary>
    public class Suspension : MonoBehaviour
    {
        [Header("References")]
        public Rigidbody carBody;

        [Header("Tire")]
        [Tooltip("Pacejka curves for this wheel. Left empty, built-in defaults are used and a warning is logged.")]
        public TireProfile tireProfile;

        [Header("Suspension Settings")]
        private float restLength = 0.6f;
        public float springStrength = 300;
        public float damping = 25;

        // Unused since the Pacejka tire model (2026-09-10): the lateral force now comes from the
        // slip-angle curve in tireProfile, and the deadbeat term this used to tune lives on as the
        // low-speed stability cap (TireMath.StabilityFraction, fixed at the 0.4 this was set to).
        // Left in place rather than deleted, per project convention on dead fields.
        public float tireGripFactor = 0.9f;

        [Header("Brakes")]
        /// <summary>
        /// Brake force at the tire radius, N. Applied as a torque on the wheel (this times the
        /// radius), so a brake stronger than the grip locks the wheel rather than being clamped at
        /// the body. Kept in newtons rather than N*m so the prefab's front/rear split carries over.
        /// </summary>
        public float maxBrakeForce = 2200f;

        // Unused since the Pacejka tire model (2026-09-10): the grip limit is D in tireProfile.
        // Left in place rather than deleted, per project convention on dead fields.
        public float brakeFrictionCoefficient = 0.9f;

        [SerializeField] private float brakeInput;

        [Header("Rollwiderstand")]
        public float rollingResistanceCoefficient = 0.015f;
        private float tireMass = 1f;
        private float wheelRadius = 0.1f;
        private float wheelInertia = 0.05f;
        public LayerMask groundMask = ~0;

        [Header("Wheel Visual")]
        public Transform wheelMesh;
        public Transform wheelmeshToRotate;

        public Vector3 suspensionForce;
        public Vector3 lateralAxis;
        public Vector3 tireWorldVelocity;
        public Vector3 tireSlip;

        [Header("Debug / Visualisierung (nur Anzeige, keine Physik)")]
        public Vector3 rollDirection;
        public Vector3 tireLongitudinalForce;
        public float brakeForceDemand;
        public float gripLimit;
        public Vector3 tireForce;
        public float rollResistanceLimit;

        /// <summary>
        /// The longitudinal stability cap against the body this step, N - the most force one step
        /// may use to close the gap between tire surface and ground (see TireMath.CapForce).
        /// </summary>
        public float rollStopLimit;

        /// <summary>
        /// True when a stability cap rather than the Pacejka curve set the longitudinal force in
        /// at least one sub-step - near standstill, where the tire behaves as it did before the
        /// Pacejka model.
        /// </summary>
        public bool rollForceAtStopLimit;

        /// <summary>Slip ratio in the last sub-step: 0 free rolling, positive driving, negative braking.</summary>
        public float slipRatio;

        /// <summary>Slip angle in the last sub-step, degrees.</summary>
        public float slipAngle;

        /// <summary>
        /// How much of the contact patch's grip the tire used in the last sub-step: 1 at the
        /// limit of the (load-scaled) friction ellipse, lower with grip to spare.
        /// </summary>
        public float gripUsage;

        // Unused since the Pacejka tire model (2026-09-10): combined slip no longer scales the
        // forces down after the fact, so there is no scale factor left to show - gripUsage above
        // reports the same thing from the other side. Left in place rather than deleted.
        public float gripScale = 1f;

        public bool isGrounded;

        [Header("Wheel Rotation")]
        /// <summary>
        /// Wheel + tire + brake assembly mass, kg. Deliberately separate from tireMass (= carBody
        /// mass / 4, tuned for body-force clamps): the wheel's own rotational inertia has nothing
        /// to do with a quarter of the car's mass, and using that there made the wheel roughly
        /// 10x too sluggish to spin down under braking or rolling resistance.
        /// </summary>
        public float wheelAssemblyMass = 20f;

        // Unused since the Pacejka tire model (2026-09-10): the longitudinal force comes from the
        // slip-ratio curve in tireProfile, integrated in sub-steps so a realistic tire stiffness
        // no longer oscillates against the wheel's small inertia. Left in place rather than
        // deleted, per project convention on dead fields.
        public float driveSlipStiffness = 250f;

        /// <summary>
        /// Radians/second, positive = tire surface moving in the same direction as
        /// <see cref="rollDirection"/>. Integrated from net torque every sub-step - not
        /// re-derived from ground velocity, so it keeps turning under drive torque even with no
        /// ground contact.
        /// </summary>
        public float wheelAngularVelocity;

        /// <summary>
        /// Torque handed in by <see cref="CarController"/> from the driveline each sub-step, N*m.
        /// Left at 0 for wheels that are not driven (front wheels today).
        /// </summary>
        [HideInInspector] public float driveTorque;

        private static TireProfile defaultProfile;

        private TireCurves curves;
        private TireForces tireForces;
        private float normalForce;
        private float forwardVelocity;
        private float lateralVelocity;
        private float lateralReferenceMass;
        private float stepDeltaTime;
        private float longitudinalImpulse;
        private float lateralImpulse;
        private bool longitudinalCapped;

        /// <summary>
        /// This wheel's rotational inertia, kg*m^2. Read by <see cref="CarController"/> so the
        /// engine can treat the driven wheels as part of one rotating mass while the clutch is
        /// closed - see <see cref="DriveTrainMath.LockedClutchTorque"/>.
        /// </summary>
        public float WheelInertia => wheelInertia;

        /// <summary>
        /// Everything except the driveline acting on this wheel's spin in the current sub-step,
        /// N*m, signed the same way as <see cref="wheelAngularVelocity"/>: the ground's reaction
        /// to the tire force, plus brakes and rolling resistance.
        /// </summary>
        public float ExternalTorque => -tireForces.Longitudinal * wheelRadius - Mathf.Sign(wheelAngularVelocity) * FrictionTorque;

        /// <summary>
        /// How far the strut is extended: 0 fully compressed, 1 at rest length or in the air. The
        /// anti roll bar reads it to see how far the two wheels of an axle have parted.
        /// </summary>
        public float ExtensionRatio => extensionRatio;

        /// <summary>Load on the tire in the last physics step, N. Zero in the air.</summary>
        public float NormalLoad => normalForce;

        /// <summary>
        /// Wheel radius, m, from the wheel mesh's scale - the same number Awake stores, so previews
        /// outside Play Mode get it too.
        /// </summary>
        public float WheelRadius => wheelMesh != null ? wheelMesh.localScale.z / 2f : wheelRadius;

        /// <summary>Brake plus rolling resistance on the wheel's spin, N*m. Rolling resistance is zero in the air.</summary>
        private float FrictionTorque => (brakeInput * maxBrakeForce + rollingResistanceCoefficient * normalForce) * wheelRadius;

        private float extensionRatio = 1f;
        private Vector3 _suspensionForcePointLocal;
        private Vector3 _tireForcePointLocal;
        private Vector3 _contactPointLocal;
        private float _spinAngle;
        private Quaternion _baseLocalRotation;

        public Vector3 suspensionForcePoint => transform.TransformPoint(_suspensionForcePointLocal);
        public Vector3 tireForcePoint => transform.TransformPoint(_tireForcePointLocal);
        public Vector3 contactPoint => transform.TransformPoint(_contactPointLocal);
        private Quaternion _debugFrame = Quaternion.identity;

        private Vector3 ToCurrentPose(Vector3 frozenWorldVector)
        {
            return transform.rotation * Quaternion.Inverse(_debugFrame) * frozenWorldVector;
        }

        public Vector3 displaySuspensionForce => ToCurrentPose(suspensionForce);
        public Vector3 displayLateralAxis => ToCurrentPose(lateralAxis);
        public Vector3 displayRollDirection => ToCurrentPose(rollDirection);
        public Vector3 displayTireLongitudinalForce => ToCurrentPose(tireLongitudinalForce);
        public Vector3 displayTireSlipForce => ToCurrentPose(tireSlip);
        public Vector3 displayTireForce => ToCurrentPose(tireForce);

        void Awake()
        {
            if (wheelMesh != null)
            {
                wheelRadius = WheelRadius; // From the wheel mesh, in meters
                tireMass = carBody.mass / 4;
                // Solid-disk approximation using the wheel's own assembly mass, not tireMass.
                wheelInertia = 0.5f * wheelAssemblyMass * wheelRadius * wheelRadius;
                _baseLocalRotation = wheelmeshToRotate.localRotation;
            }

            // Keeps the car driveable before the prefab has a profile assigned, rather than
            // throwing on the first physics step.
            if (tireProfile == null)
            {
                if (defaultProfile == null)
                {
                    defaultProfile = ScriptableObject.CreateInstance<TireProfile>();
                    defaultProfile.hideFlags = HideFlags.DontSave;
                }

                tireProfile = defaultProfile;
                Debug.LogWarning($"{name}: no TireProfile assigned, using built-in defaults. Create one via Create > CargoKing > Tire Profile.", this);
            }
        }

        /// <summary>
        /// Phase 1, once per physics step: raycast, spring force, and everything the tire needs
        /// about its contact patch for the sub-steps that follow - load, contact frame, and the
        /// contact point's velocity, which stays frozen across the sub-steps.
        /// </summary>
        public void UpdateContact(float deltaTime)
        {
            stepDeltaTime = deltaTime;
            curves = tireProfile.Curves;
            tireForces = default;
            longitudinalImpulse = 0f;
            lateralImpulse = 0f;
            longitudinalCapped = false;

            Vector3 springDirection = transform.up;

            Vector3 origin = transform.position;
            Vector3 rayDirection = -transform.up;
            float maxDist = restLength + wheelRadius;

            if (Physics.Raycast(origin, rayDirection, out RaycastHit hit, maxDist, groundMask))
            {
                float offset = restLength - (hit.distance - wheelRadius);

                Vector3 wheelVelocity = carBody.GetPointVelocity(wheelMesh.position);
                float velocity = Vector3.Dot(springDirection, wheelVelocity);

                float force = CalculateSpringForce(offset, velocity);

                isGrounded = true;
                extensionRatio = Mathf.Clamp01((hit.distance - wheelRadius) / restLength);
                _contactPointLocal = transform.InverseTransformPoint(hit.point);

                Vector3 springForcePoint = transform.position;
                _suspensionForcePointLocal = transform.InverseTransformPoint(springForcePoint);
                carBody.AddForceAtPosition(transform.up * (float)force, springForcePoint);
                wheelMesh.position = transform.position - transform.up * (hit.distance - wheelRadius);
                wheelmeshToRotate.position = transform.position - transform.up * (hit.distance - wheelRadius);

                Vector3 gripForcePoint = wheelMesh.position;
                _tireForcePointLocal = transform.InverseTransformPoint(gripForcePoint);

                lateralAxis = wheelMesh.up;
                rollDirection = wheelMesh.forward;
                tireWorldVelocity = carBody.GetPointVelocity(gripForcePoint);
                forwardVelocity = Vector3.Dot(rollDirection, tireWorldVelocity);
                lateralVelocity = Vector3.Dot(lateralAxis, tireWorldVelocity);
                normalForce = Mathf.Max(0f, suspensionForce.y);

                // EffectiveMassAt, not tireMass: this is the reference the old deadbeat lateral
                // term was stable with, and the lateral cap is that term.
                lateralReferenceMass = EffectiveMassAt(gripForcePoint, lateralAxis);

                _debugFrame = transform.rotation;
            }
            else
            {
                // Wheel in the air: reset the display values, or the old arrows stay put. No load
                // means no tire force, so the wheel keeps spinning under driveTorque alone (or
                // coasts under its own inertia) - the brake still stops it.
                isGrounded = false;
                extensionRatio = 1f;
                normalForce = 0f;
                _debugFrame = transform.rotation;
                suspensionForce = Vector3.zero;
                tireSlip = Vector3.zero;
                tireLongitudinalForce = Vector3.zero;
                brakeForceDemand = 0f;
                gripLimit = 0f;
                tireForce = Vector3.zero;
                rollResistanceLimit = 0f;
                rollStopLimit = 0f;
                rollForceAtStopLimit = false;
                slipRatio = 0f;
                slipAngle = 0f;
                gripUsage = 0f;
            }
        }

        /// <summary>
        /// Phase 2a, once per sub-step: the tire force from the wheel's current spin, accumulated
        /// for <see cref="ApplyTireForce"/>. Must run before the engine's sub-step so the clutch
        /// sees this sub-step's <see cref="ExternalTorque"/>.
        /// </summary>
        public void EvaluateTire(float subStepDeltaTime)
        {
            if (!isGrounded)
            {
                tireForces = default;
                return;
            }

            // tireMass as the body reference, not EffectiveMassAt: four wheels act on the same
            // body, so each may only claim its quarter of the total impulse. With EffectiveMassAt
            // the four together push with a multiple of it and the car oscillates.
            tireForces = TireMath.EvaluateTire(curves, wheelAngularVelocity, wheelRadius, wheelInertia,
                forwardVelocity, lateralVelocity, normalForce, tireMass, lateralReferenceMass,
                stepDeltaTime, subStepDeltaTime);

            longitudinalImpulse += tireForces.Longitudinal * subStepDeltaTime;
            lateralImpulse += tireForces.Lateral * subStepDeltaTime;
            longitudinalCapped |= tireForces.LongitudinalCapped;
        }

        /// <summary>
        /// Phase 2b, once per sub-step after the engine: the wheel's spin under
        /// <see cref="driveTorque"/>, the tire's reaction, brakes and rolling resistance.
        /// </summary>
        public void IntegrateWheel(float subStepDeltaTime)
        {
            wheelAngularVelocity = TireMath.IntegrateWheel(wheelAngularVelocity, driveTorque, tireForces.Longitudinal,
                wheelRadius, wheelInertia, FrictionTorque, subStepDeltaTime);
        }

        /// <summary>
        /// Phase 3, once per physics step: the tire force averaged over the sub-steps goes onto
        /// the body at the contact point.
        /// </summary>
        public void ApplyTireForce()
        {
            if (isGrounded)
            {
                float longitudinal = longitudinalImpulse / stepDeltaTime;
                float lateral = lateralImpulse / stepDeltaTime;

                tireLongitudinalForce = rollDirection * longitudinal;
                tireSlip = lateralAxis * lateral;
                tireForce = tireLongitudinalForce + tireSlip;
                carBody.AddForceAtPosition(tireForce, tireForcePoint);

                slipRatio = tireForces.SlipRatio;
                slipAngle = tireForces.SlipAngle;
                gripUsage = tireForces.GripUsage;
                brakeForceDemand = brakeInput * maxBrakeForce;
                gripLimit = curves.Longitudinal.d * TireMath.LoadFactor(normalForce, curves.NominalLoad, curves.LoadSensitivity) * normalForce;
                rollResistanceLimit = rollingResistanceCoefficient * normalForce;
                rollStopLimit = TireMath.StabilityFraction * tireMass
                    * Mathf.Abs(wheelAngularVelocity * wheelRadius - forwardVelocity) / stepDeltaTime;
                rollForceAtStopLimit = longitudinalCapped;
            }

            VisualWheelRotation();
        }

        private void VisualWheelRotation()
        {
            float deltaDeg = wheelAngularVelocity * Time.fixedDeltaTime * Mathf.Rad2Deg;
            _spinAngle = Mathf.Repeat(_spinAngle + deltaDeg, 360f);
            wheelmeshToRotate.localRotation = _baseLocalRotation * Quaternion.Euler(0f, -_spinAngle, 0f);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.coral;
            Vector3 origin = wheelMesh.position;
            Vector3 end = origin + new Vector3(0f, wheelMesh.localScale.z / 2, 0f) * -1f;

            Gizmos.DrawLine(origin, end);

            Gizmos.color = Color.yellow;
            Vector3 upperSuspensionJoint = transform.position;
            Vector3 wheelOrigin = wheelMesh.position;
            Gizmos.DrawSphere(upperSuspensionJoint, 0.02f);
            Gizmos.DrawSphere(wheelOrigin, 0.02f);
            Gizmos.DrawLine(upperSuspensionJoint, wheelOrigin);
        }

        public float CalculateSpringForce(float offset, float velocity)
        {
            var force = (offset * springStrength) - (velocity * damping);
            suspensionForce = new Vector3(0f, force, 0f);
            return (offset * springStrength) - (velocity * damping);
        }

        private float EffectiveMassAt(Vector3 point, Vector3 dir)
        {
            Vector3 r = point - carBody.worldCenterOfMass;
            Vector3 rxd = Vector3.Cross(r, dir);

            Quaternion tensorRot = carBody.rotation * carBody.inertiaTensorRotation;
            Vector3 local = Quaternion.Inverse(tensorRot) * rxd;
            Vector3 it = carBody.inertiaTensor;
            Vector3 scaled = new Vector3(local.x / it.x, local.y / it.y, local.z / it.z);

            float angular = Vector3.Dot(rxd, tensorRot * scaled);
            return 1f / (1f / carBody.mass + angular);
        }

        public void SetBrakeInput(float value)
        {
            brakeInput = Mathf.Clamp01(value);
        }

        /// <summary>
        /// The most braking force this wheel can pass to the road right now, N: the brake itself
        /// or the tire's peak grip, whichever is smaller. Read from the profile directly rather
        /// than from the per-step cache, so it answers before the first physics step as well.
        /// </summary>
        public float AvailableBrakeForce()
        {
            if (!isGrounded) return 0f;

            float load = Mathf.Max(0f, suspensionForce.y);
            float grip = tireProfile.longitudinal.d
                * TireMath.LoadFactor(load, tireProfile.nominalLoad, tireProfile.loadSensitivity) * load;
            return Mathf.Min(maxBrakeForce + rollingResistanceCoefficient * load, grip);
        }
    }
}
