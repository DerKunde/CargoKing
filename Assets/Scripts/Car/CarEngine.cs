using CargoKing.Input;
using UnityEngine;

namespace CargoKing.Car
{
    public class CarEngine : MonoBehaviour
    {
        [Header("Profiles")]
        [Tooltip("Torque curve, rpm range, rotating mass and losses. Left empty, built-in defaults are used and a warning is logged.")]
        public EngineProfile engineProfile;

        [Tooltip("Gear ratios, final drive and efficiency. Left empty, built-in defaults are used and a warning is logged.")]
        public GearboxProfile gearboxProfile;

        /// <summary>Ratio of the engaged gear; reverse is gear 0.</summary>
        public float CurrentGearRatio => gearboxProfile.Ratio(currentGear);
        public float DriveDirection => currentGear == 0 ? -1f : 1f;

        /// <summary>Signed: gear, final drive and direction of travel in one number.</summary>
        public float TotalRatio => CurrentGearRatio * gearboxProfile.axleRatio * DriveDirection;

        public float Efficiency => gearboxProfile.efficiency;
        public float MaxRevolutions => engineProfile.maxRevolutions;
        public float MaxReverseShiftSpeed => gearboxProfile.maxReverseShiftSpeed;
        public int GearCount => gearboxProfile.GearCount;

        [Header("Clutch")]
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

        public float restartDelay = 1f;

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

        // Keeps a fade range that is being typed in from dividing by zero.
        private const float MinRevLimiterFadeRange = 1f;

        private static EngineProfile defaultEngineProfile;
        private static GearboxProfile defaultGearboxProfile;

        private float restartTimer = -1f;

        private void Awake()
        {
            // Keeps the car driveable before the prefab has profiles assigned, as Suspension does
            // for its tire profile, rather than throwing on the first physics step.
            if (engineProfile == null)
            {
                if (defaultEngineProfile == null)
                {
                    defaultEngineProfile = ScriptableObject.CreateInstance<EngineProfile>();
                    defaultEngineProfile.hideFlags = HideFlags.DontSave;
                }

                engineProfile = defaultEngineProfile;
                Debug.LogWarning($"{name}: no EngineProfile assigned, using built-in defaults. Create one via Create > CargoKing > Engine Profile.", this);
            }

            if (gearboxProfile == null)
            {
                if (defaultGearboxProfile == null)
                {
                    defaultGearboxProfile = ScriptableObject.CreateInstance<GearboxProfile>();
                    defaultGearboxProfile.hideFlags = HideFlags.DontSave;
                }

                gearboxProfile = defaultGearboxProfile;
                Debug.LogWarning($"{name}: no GearboxProfile assigned, using built-in defaults. Create one via Create > CargoKing > Gearbox Profile.", this);
            }

            revolutionsPerMinute = engineProfile.idleRevolutions;
        }

        /// <summary>
        /// Advances the engine and clutch by one physics step and returns the torque (N*m) the
        /// driveline should apply at the gearbox output - CarController splits this between the
        /// driven wheels and hands it to Suspension.driveTorque.
        ///
        /// Reads both profiles on every call, so changes made to them while driving apply on the
        /// next step.
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
            EngineProfile engine = engineProfile;
            GearboxProfile gearbox = gearboxProfile;

            isOnGasPadle = throttle > 0f;

            // Gears removed from the profile while driving: carry on in the highest one left.
            if (currentGear > gearbox.GearCount)
            {
                currentGear = gearbox.GearCount;
            }

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
            float totalRatio = TotalRatio;
            gearboxRevolutions = DriveTrainMath.AngularVelocityToRpm(driveline.AngularVelocity) * totalRatio;

            // Friction is taken off the indicated torque here AND added back into it inside
            // CombustionTorque, which is not redundant: it means full throttle nets out to exactly
            // the torque curve (a curve is brake torque, already net of losses) while friction
            // still governs idle and over-run. See DriveTrainMath.CombustionTorque.
            float friction = DriveTrainMath.EngineFrictionTorque(revolutionsPerMinute, engine.maxRevolutions, engine.engineFrictionTorque);
            float governorTorque = DriveTrainMath.IdleGovernorTorque(
                revolutionsPerMinute, engine.idleRevolutions, engine.maxRevolutions, engine.engineFrictionTorque, engine.idleGovernorGain);
            float combustionTorque = DriveTrainMath.CombustionTorque(
                engine.TorqueAt(revolutionsPerMinute), friction, governorTorque, throttle);

            revLimiterFactor = Mathf.Clamp01(
                (engine.maxRevolutions - revolutionsPerMinute) / Mathf.Max(MinRevLimiterFadeRange, engine.revLimiterFadeRange));

            float engineNetTorque = combustionTorque * revLimiterFactor - friction;

            float lockedClutchTorque = DriveTrainMath.LockedClutchTorque(
                revolutionsPerMinute * 2f * Mathf.PI / 60f,
                driveline.AngularVelocity,
                engineNetTorque,
                driveline.ExternalTorque,
                engine.engineInertia,
                driveline.Inertia,
                totalRatio,
                gearbox.efficiency,
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
                DriveTrainMath.IntegrateEngineRpm(revolutionsPerMinute, engineNetTorque, clutchReactionTorque, engine.engineInertia, deltaTime),
                0f,
                engine.maxRevolutions);

            if (DriveTrainMath.IsStalled(revolutionsPerMinute, engine.stallRpm))
            {
                engineStalled = true;
                revolutionsPerMinute = 0f;
                return 0f;
            }

            return clutchReactionTorque * totalRatio * gearbox.efficiency;
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
                revolutionsPerMinute = engineProfile.idleRevolutions;
                return false;
            }

            return true;
        }

        public bool ChangeGear(GearShift direction, float currentSpeedMS)
        {
            int previousGear = currentGear;

            if (direction == GearShift.Up && currentGear < GearCount)
            {
                currentGear += 1;
            }
            if (direction == GearShift.Down && currentGear > 0)
            {
                if (currentGear == 1 && Mathf.Abs(currentSpeedMS) > MaxReverseShiftSpeed) return false;
                currentGear -= 1;
            }

            return currentGear != previousGear;
        }
    }
}
