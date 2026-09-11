using UnityEngine;

namespace CargoKing.Car
{
    /// <summary>
    /// One engine: its full-load torque curve and what shapes its rpm on its own - speed range,
    /// rotating mass, losses, idle circuit. Shared by reference, like <see cref="TireProfile"/>, so
    /// the values live in exactly one place and can be swapped between cars.
    ///
    /// <see cref="CarEngine"/> reads it on every step, so edits made in Play Mode apply on the next
    /// one - and persist after leaving Play Mode, as with any asset. That is intended: tune while
    /// driving, keep the result.
    /// </summary>
    [CreateAssetMenu(menuName = "CargoKing/Engine Profile", fileName = "EngineProfile")]
    public class EngineProfile : ScriptableObject
    {
        [Header("Torque curve (full load)")]
        public TorqueCurve torque = TorqueCurve.PuntoReference;

        [Header("Speed range")]
        public float idleRevolutions = 1000f;
        public float maxRevolutions = 6000f;

        [Tooltip("Below this the engine stalls.")]
        public float stallRpm = 600f;

        [Tooltip("Rpm band below max rpm over which the rev limiter fades the torque out.")]
        public float revLimiterFadeRange = 300f;

        [Header("Rotating mass and losses")]
        [Tooltip("Rotational inertia of crank, flywheel and clutch, kg*m^2.")]
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
        [Tooltip("Friction and pumping loss at max rpm, N*m. Always opposes engine rotation.")]
        public float engineFrictionTorque = 30f;

        /// <summary>
        /// Idle circuit gain, N*m per RPM below the idle target. Sets how quickly idle settles:
        /// 0.05 brings the engine onto the target in about a second from just above it, and gives
        /// the circuit some 35 N*m of authority at the stall threshold - enough to hold a clean
        /// idle, nowhere near enough to save a dumped clutch.
        /// </summary>
        [Tooltip("Idle circuit gain, N*m per rpm below idle.")]
        public float idleGovernorGain = 0.05f;

        /// <summary>Full-load torque at <paramref name="rpm"/>, N*m.</summary>
        public float TorqueAt(float rpm)
        {
            return EngineMath.Torque(rpm, torque, idleRevolutions, maxRevolutions);
        }
    }
}
