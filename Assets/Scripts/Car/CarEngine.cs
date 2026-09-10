using CargoKing.Input;
using UnityEngine;

namespace CargoKing.Car
{
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

        // Unused since removing the artificial RPM/s rate cap from DriveTrainMath.IntegrateEngineRpm:
        // it created a two-phase (torque-limited, then suddenly capped) response with no physical
        // basis. Left in place rather than deleted, per project convention on dead fields.
        public float rpmChangeSpeed = 3000f;

        [Header("Engine (rotating mass)")]
        public float engineInertia = 0.15f;

        /// <summary>
        /// Engine friction and pumping loss at max RPM, N*m, falling off towards standstill along
        /// the shape in DriveTrainMath.EngineFrictionTorque. Always opposes engine rotation,
        /// throttle or not - without it a declutched, off-throttle engine is a frictionless
        /// flywheel and never returns to idle.
        ///
        /// The default is the real closed-throttle drag of a 1.2 l naturally aspirated four
        /// (FMEP + PMEP, roughly 3.2 bar at 6000 rpm), cross-checked against how fast such an
        /// engine actually drops from 4000 rpm to idle. It used to sit at three times this, for
        /// two reasons that have both since been dealt with: the torque curve was being charged
        /// for friction twice (see DriveTrainMath.CombustionTorque), and idle was held by a
        /// passive friction balance that needed a steep curve to settle at all (see
        /// DriveTrainMath.IdleGovernorTorque).
        ///
        /// This is not the full engine-braking feature (GitHub #9, still deferred - that one
        /// feeds drag into the wheels while the clutch is closed); this only acts on the engine's
        /// own RPM.
        /// </summary>
        public float engineFrictionTorque = 30f;

        /// <summary>
        /// Idle circuit gain, N*m per RPM below the idle target. Sets how quickly idle settles:
        /// 0.05 brings the engine onto the target in about a second from just above it, and gives
        /// the circuit some 35 N*m of authority at the stall threshold - enough to hold a clean
        /// idle, nowhere near enough to save a dumped clutch.
        /// </summary>
        public float idleGovernorGain = 0.05f;

        [Header("Clutch")]

        // Unused since the clutch became a solved constraint rather than a spring (see Tick):
        // an engaged clutch has no stiffness to speak of, it simply holds. Left in place rather
        // than deleted, per project convention on dead fields.
        public float clutchStiffness = 0.2f;

        public float maxClutchTorque = 220f;

        /// <summary>
        /// Launch assist: while the car stands or the clutch key is held, the clutch takes up
        /// with engine speed instead of snapping shut - nothing at this rpm, the whole plate at
        /// <see cref="launchFullRpm"/> (see DriveTrainMath.LaunchClutchCapacity). Just above idle,
        /// so letting go of the key at idle cannot stall the engine.
        /// </summary>
        [Tooltip("Launch assist: engine rpm at which the clutch starts to bite. Just above idle, so letting go at idle cannot stall.")]
        public float launchEngageRpm = 1100f;

        [Tooltip("Launch assist: engine rpm from which the clutch passes on its full capacity.")]
        public float launchFullRpm = 2200f;

        // Unused since the clutch became a solved constraint (see Tick): whether it is locked is
        // answered by whether the constraint torque fits inside maxClutchTorque, so there is no
        // state left to detect with an epsilon check. Left in place rather than deleted.
        public float lockEpsilonRpm = 50f;

        public float stallRpm = 600f;
        public float restartDelay = 1f;

        // Unused since the clutch became a solved constraint (see Tick): a gear shift changes the
        // ratio, the constraint then asks for more than the plate can hold, and it slips until the
        // two sides are back together - no special-cased blend window needed. Left in place rather
        // than deleted.
        public float gearShiftClutchBlendDuration = 0.2f;

        [Header("Drehzahlbegrenzer")]
        public float revLimiterFadeRange = 300f;

        [Header("Calculated Values !!! Do not change !!!")]
        public float revolutionsPerMinute;
        public float speedInKmH;
        public int currentGear = 1;
        public bool isOnGasPadle = false;
        public bool engineStalled = false;
        public float gearboxRevolutions;
        public float revLimiterFactor = 1f;
        public float debugCombustionTorque;
        public float debugFrictionTorque;
        public float debugClutchReactionTorque;

        /// <summary>True while the launch assist sets the clutch capacity - from standstill until the clutch first holds.</summary>
        public bool launchAssistActive = true;

        /// <summary>What the clutch could pass on this step, N*m - 0 with the key held, the take-up during a launch, maxClutchTorque otherwise.</summary>
        public float debugClutchCapacity;

        // Below this the car counts as standing and the launch assist takes over again.
        private const float LaunchStandstillKmh = 1f;

        private float restartTimer = -1f;

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
        ///
        /// The clutch is not a Held/Slipping/Locked state machine and not a spring either. Engaged,
        /// it is solved as a rigid constraint: DriveTrainMath.LockedClutchTorque returns the torque
        /// that makes engine and driven wheels turn as one mass, and clamping that to
        /// maxClutchTorque is what slipping means - a plate can only pass on so much before it
        /// gives. So there is no state to track, no lock epsilon, and no special-cased blend after
        /// a gear shift: a shift changes the ratio, the constraint asks for more than the plate can
        /// hold, and it slips until the two are back together on its own.
        ///
        /// Held (pedal down) zeroes it and the engine is free to rev on its own inertia.
        ///
        /// Launch assist: from standstill, or with the key held, the capacity follows engine speed
        /// (DriveTrainMath.LaunchClutchCapacity) until the clutch first holds - the take-up a
        /// driver would do with the pedal. After that it is the full plate again, and the engine
        /// can be stalled like in any manual.
        /// </summary>
        public float Tick(float throttle, in DrivelineLoad driveline, bool clutchHeld, bool restartRequested, float deltaTime)
        {
            isOnGasPadle = throttle > 0f;

            if (HandleStallAndRestart(restartRequested, deltaTime))
            {
                revolutionsPerMinute = 0f;
                debugClutchCapacity = 0f;
                // A restart is a new launch, even if the car is still rolling.
                launchAssistActive = true;
                return 0f;
            }

            // Signed: gear, final drive and direction of travel in one number, so reverse needs no
            // separate handling anywhere below.
            float totalRatio = CurrentGearRation * axleRatio * DriveDirection;
            gearboxRevolutions = DriveTrainMath.AngularVelocityToRpm(driveline.AngularVelocity) * totalRatio;

            // Friction is taken off the indicated torque here AND added back into it inside
            // CombustionTorque, which is not redundant: it means full throttle nets out to exactly
            // the torque curve (a curve is brake torque, already net of losses) while friction
            // still governs idle and over-run. See DriveTrainMath.CombustionTorque.
            float friction = DriveTrainMath.EngineFrictionTorque(revolutionsPerMinute, maxRevolutions, engineFrictionTorque);
            float governorTorque = DriveTrainMath.IdleGovernorTorque(
                revolutionsPerMinute, idleRevolutions, maxRevolutions, engineFrictionTorque, idleGovernorGain);
            float combustionTorque = DriveTrainMath.CombustionTorque(
                GetMaxTorqueForRPM(revolutionsPerMinute), friction, governorTorque, throttle);

            revLimiterFactor = Mathf.Clamp01((maxRevolutions - revolutionsPerMinute) / revLimiterFadeRange);

            float engineNetTorque = combustionTorque * revLimiterFactor - friction;

            float lockedClutchTorque = DriveTrainMath.LockedClutchTorque(
                revolutionsPerMinute * 2f * Mathf.PI / 60f,
                driveline.AngularVelocity,
                engineNetTorque,
                driveline.ExternalTorque,
                engineInertia,
                driveline.Inertia,
                totalRatio,
                efficiency,
                deltaTime);

            float clutchCapacity = clutchHeld
                ? 0f
                : launchAssistActive
                    ? DriveTrainMath.LaunchClutchCapacity(revolutionsPerMinute, launchEngageRpm, launchFullRpm, maxClutchTorque)
                    : maxClutchTorque;

            float clutchReactionTorque = Mathf.Clamp(lockedClutchTorque, -clutchCapacity, clutchCapacity);

            // Slipping is exactly "the constraint asked for more than the plate could give" - the
            // same test that decides whether the clutch is locked at all.
            launchAssistActive = DriveTrainMath.LaunchAssistActive(
                launchAssistActive,
                clutchHeld,
                standing: Mathf.Abs(speedInKmH) < LaunchStandstillKmh,
                clutchSlipping: Mathf.Abs(lockedClutchTorque) > clutchCapacity);

            debugCombustionTorque = combustionTorque * revLimiterFactor;
            debugFrictionTorque = friction;
            debugClutchReactionTorque = clutchReactionTorque;
            debugClutchCapacity = clutchCapacity;

            revolutionsPerMinute = Mathf.Clamp(
                DriveTrainMath.IntegrateEngineRpm(revolutionsPerMinute, engineNetTorque, clutchReactionTorque, engineInertia, deltaTime),
                0f,
                maxRevolutions);

            if (DriveTrainMath.IsStalled(revolutionsPerMinute, stallRpm))
            {
                engineStalled = true;
                revolutionsPerMinute = 0f;
                return 0f;
            }

            return clutchReactionTorque * totalRatio * efficiency;
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
                return false;
            }

            return true;
        }

        private float GetMaxTorqueForRPM(float currentRPM)
        {
            return LookUpOnTorqueCurve(torqueCurve, currentRPM);
        }

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
