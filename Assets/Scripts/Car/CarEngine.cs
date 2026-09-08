using CargoKing.Input;
using UnityEngine;

namespace CargoKing.Car
{
    public enum ClutchState
    {
        Held,
        Slipping,
        Locked
    }

    public class CarEngine : MonoBehaviour
    {
        public AnimationCurve torqueCurve;
        [Header("Gearbox")]
        public float[] gears = {3.2f, 1.9f, 1.3f, 1.0f, 0.8f};
        public float reverseRation = 3.5f;
        public float CurrentGearRation => currentGear == 0 ? reverseRation : gears[currentGear -1];
        public float DriveDirection => currentGear == 0 ? -1f : 1f;
        public float maxReverseShiftSpeed = 1f;
        public float axleRatio = 4f;
        public float efficiency = 0.85f;

        // Unused since the drivetrain rework (2026-09-08): engine RPM and drive torque are now
        // derived from the driven wheels' own angular velocity (Suspension.wheelAngularVelocity),
        // not from tire circumference against car body speed. Left in place rather than deleted -
        // flag for an explicit decision (e.g. reuse to validate this stays consistent with the
        // wheel prefab's actual radius) rather than silently removing it.
        public float tireRadius = 0.31f;

        public float idleRevolutions = 1000f;
        public float maxRevolutions = 6000f;
        public float rpmChangeSpeed = 3000f;

        [Header("Engine (rotating mass)")]
        public float engineInertia = 0.15f;

        /// <summary>
        /// Internal engine friction/pumping loss, N*m at max RPM, scaled linearly down to 0 at
        /// standstill. Always opposes engine rotation, throttle or not - without it a declutched,
        /// off-throttle engine is a frictionless flywheel and never returns to idle. This is not
        /// the full engine-braking feature (GitHub #9, still deferred - that one feeds drag into
        /// the wheels while the clutch is locked); this only acts on the engine's own RPM.
        /// </summary>
        public float engineFrictionTorque = 10f;

        [Header("Clutch")]
        public float clutchStiffness = 0.2f;
        public float maxClutchTorque = 220f;
        public float lockEpsilonRpm = 50f;
        public float stallRpm = 600f;
        public float restartDelay = 1f;
        public float gearShiftClutchBlendDuration = 0.2f;

        [Header("Drehzahlbegrenzer")]
        public float revLimiterFadeRange = 300f;

        [Header("Calculated Values !!! Do not change !!!")]
        public float revolutionsPerMinute;
        public float speedInKmH;
        public int currentGear = 1;
        public bool isOnGasPadle = false;
        public ClutchState clutchState = ClutchState.Locked;
        public bool engineStalled = false;
        public float gearboxRevolutions;
        public float revLimiterFactor = 1f;
        public float debugCombustionTorque;
        public float debugFrictionTorque;
        public float debugClutchReactionTorque;

        private float restartTimer = -1f;
        private float shiftBlendTimer = 0f;

        private int rpmDivisor = 10000;
        private int torqueFactor = 1000;

        private void Awake()
        {
            revolutionsPerMinute = idleRevolutions;
        }

        /// <summary>
        /// Advances the engine and clutch by one physics step and returns the torque (N*m) the
        /// driveline should apply at the gearbox output - CarController splits this between the
        /// driven wheels and hands it to Suspension.driveTorque.
        /// </summary>
        public float Tick(float throttle, float driveWheelAngularVelocityAvg, bool clutchHeld, bool restartRequested, float deltaTime)
        {
            isOnGasPadle = throttle > 0f;

            if (HandleStallAndRestart(restartRequested, deltaTime))
            {
                revolutionsPerMinute = 0f;
                return 0f;
            }

            gearboxRevolutions = DriveTrainMath.AngularVelocityToRpm(driveWheelAngularVelocityAvg) * DriveDirection * axleRatio * CurrentGearRation;

            if (shiftBlendTimer > 0f)
            {
                shiftBlendTimer -= deltaTime;
            }

            UpdateClutchState(clutchHeld);

            float combustionTorque = GetMaxTorqueForRPM(revolutionsPerMinute) * throttle;

            if (clutchState == ClutchState.Locked)
            {
                // Rigid coupling: engine and driveline turn as one, exactly like a car with the
                // clutch fully home. No slip loss, so wheel torque follows the torque curve
                // directly through the gear stack - same formula the old CalculateWheelTorque used.
                revolutionsPerMinute = Mathf.Clamp(gearboxRevolutions, idleRevolutions, maxRevolutions);
                revLimiterFactor = Mathf.Clamp01((maxRevolutions - gearboxRevolutions) / revLimiterFadeRange);
                float lockedTorque = GetMaxTorqueForRPM(revolutionsPerMinute) * throttle * revLimiterFactor;
                return lockedTorque * CurrentGearRation * axleRatio * efficiency * DriveDirection;
            }

            // Held or Slipping: engine is free to move on its own inertia; the clutch only
            // transfers whatever the plate's slip torque allows.
            revLimiterFactor = Mathf.Clamp01((maxRevolutions - revolutionsPerMinute) / revLimiterFadeRange);
            float clutchReactionTorque = clutchState == ClutchState.Held
                ? 0f
                : DriveTrainMath.ClutchTorque(revolutionsPerMinute, gearboxRevolutions, clutchStiffness, maxClutchTorque);

            // Internal friction always opposes rotation, throttle or not - this is what lets a
            // held/slipping (declutched) engine actually settle back towards idle instead of
            // holding whatever RPM it was last revved to.
            float friction = engineFrictionTorque * (revolutionsPerMinute / maxRevolutions);

            debugCombustionTorque = combustionTorque * revLimiterFactor;
            debugFrictionTorque = friction;
            debugClutchReactionTorque = clutchReactionTorque;

            // Idle governor: with the clutch fully disengaged there is no load pulling the engine
            // down at all, so a real idle circuit holds it at idleRevolutions no matter how low
            // throttle goes - it never just decays to a stop. While Slipping there IS a load (the
            // driveline dragging through the clutch), so the floor stays at 0 there: that load is
            // exactly what can pull RPM down into a stall, which is the point of the mechanic.
            float rpmFloor = clutchState == ClutchState.Held ? idleRevolutions : 0f;

            revolutionsPerMinute = Mathf.Clamp(
                DriveTrainMath.IntegrateEngineRpm(revolutionsPerMinute, combustionTorque * revLimiterFactor - friction, clutchReactionTorque, engineInertia, rpmChangeSpeed, deltaTime),
                rpmFloor,
                maxRevolutions);

            if (DriveTrainMath.IsStalled(revolutionsPerMinute, stallRpm))
            {
                engineStalled = true;
                revolutionsPerMinute = 0f;
                return 0f;
            }

            return clutchReactionTorque * CurrentGearRation * axleRatio * efficiency * DriveDirection;
        }

        private void UpdateClutchState(bool clutchHeld)
        {
            if (clutchHeld)
            {
                clutchState = ClutchState.Held;
                return;
            }

            // The step the pedal comes up counts as slipping even if the RPMs already happen to
            // match - a real clutch does not snap to fully locked the instant it starts biting.
            bool justReleased = clutchState == ClutchState.Held;
            bool locked = !justReleased && shiftBlendTimer <= 0f
                && DriveTrainMath.IsLocked(revolutionsPerMinute, gearboxRevolutions, lockEpsilonRpm);

            clutchState = locked ? ClutchState.Locked : ClutchState.Slipping;
        }

        private bool HandleStallAndRestart(bool restartRequested, float deltaTime)
        {
            if (!engineStalled)
            {
                return false;
            }

            if (restartTimer < 0f)
            {
                if (restartRequested)
                {
                    restartTimer = restartDelay;
                }
                return true;
            }

            restartTimer -= deltaTime;
            if (restartTimer <= 0f)
            {
                engineStalled = false;
                restartTimer = -1f;
                revolutionsPerMinute = idleRevolutions;
                clutchState = ClutchState.Locked;
                return false;
            }

            return true;
        }

        /// <summary>Marks the driveline for a brief auto-clutch blend, e.g. right after a gear change.</summary>
        public void BeginAutoClutchBlend()
        {
            shiftBlendTimer = gearShiftClutchBlendDuration;
        }

        private float GetMaxTorqueForRPM(float currentRPM)
        {
            return LookUpOnTorqueCurve(torqueCurve, currentRPM);
        }

        /// <summary>Returns true if the gear actually changed, so the caller knows to start an auto-clutch blend.</summary>
        public bool ChangeGear(GearShift direction, float currentSpeedMS)
        {
            int previousGear = currentGear;

            if(direction == GearShift.Up && currentGear < gears.Length)
            {
                currentGear += 1;
            }
            if(direction == GearShift.Down && currentGear > 0)
            {
                if(currentGear == 1 && Mathf.Abs(currentSpeedMS) > maxReverseShiftSpeed) return false;
                currentGear -= 1;
            }

            return currentGear != previousGear;
        }

        private float LookUpOnTorqueCurve(AnimationCurve curve, float xValueToLookAt)
        {
            return curve.Evaluate(xValueToLookAt / rpmDivisor) * torqueFactor;
        }
    }
}
