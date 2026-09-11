using UnityEngine;

namespace CargoKing.Car
{
    /// <summary>
    /// The driven wheels seen as one rotating mass, which is all the engine needs to know about
    /// them. Gathered by CarController from the driven Suspensions and handed to CarEngine.Tick.
    ///
    /// Deliberately the average velocity but the SUM of the inertias: torque is split evenly
    /// between the two wheels, so as long as they turn together this is exact, and when they do
    /// not (one wheel spinning up) it is the usual open-differential lump.
    /// </summary>
    public readonly struct DrivelineLoad
    {
        /// <summary>Mean angular velocity of the driven wheels, rad/s.</summary>
        public readonly float AngularVelocity;

        /// <summary>Combined rotational inertia of the driven wheels, kg*m^2.</summary>
        public readonly float Inertia;

        /// <summary>
        /// Everything other than the clutch acting on those wheels this step, N*m - the ground's
        /// reaction to the drive force, plus brakes and rolling resistance. Needed because a
        /// clutch that has to hold engine and wheels together has to know what else is pulling
        /// them apart; without it the coupling drifts under load.
        /// </summary>
        public readonly float ExternalTorque;

        public DrivelineLoad(float angularVelocity, float inertia, float externalTorque)
        {
            AngularVelocity = angularVelocity;
            Inertia = inertia;
            ExternalTorque = externalTorque;
        }
    }

    /// <summary>
    /// Pure drivetrain math with no MonoBehaviour or scene dependency, so it can run under a
    /// plain EditMode test without a Play Mode session.
    /// </summary>
    public static class DriveTrainMath
    {
        public static float AngularVelocityToRpm(float angularVelocityRadPerSec)
        {
            return angularVelocityRadPerSec * 60f / (2f * Mathf.PI);
        }

        public static bool IsStalled(float engineRpm, float stallRpm)
        {
            return engineRpm < stallRpm;
        }

        // Friction and pumping loss does not follow a single power of engine speed. The standard
        // description is a polynomial in speed - a constant term (bearing, ring and valvetrain
        // friction, near enough speed-independent), a linear term and a quadratic one. These three
        // shares reproduce FMEP + PMEP figures for a 1.2 l naturally aspirated four (about 16 N*m
        // at 1000 rpm, 21 at 3000, 30 at 6000) and sum to 1 at max RPM, so
        // frictionTorqueAtMaxRpm stays the one value worth tuning.
        //
        // A pure square, which is what stood here before, has no constant term at all and so goes
        // to almost nothing at the bottom of the range - and idle sits exactly there. That left
        // the idle equilibrium so slack it took some ten seconds to settle, which is what drove
        // the friction value up to three times anything real just to make idle behave.
        private const float FrictionConstantShare = 0.47f;
        private const float FrictionLinearShare = 0.22f;
        private const float FrictionQuadraticShare = 0.31f;

        /// <summary>
        /// Engine internal friction and pumping loss (M_reib) at a given RPM, N*m, rising
        /// monotonically from roughly half of <paramref name="frictionTorqueAtMaxRpm"/> at
        /// standstill to the full value at max RPM.
        /// </summary>
        public static float EngineFrictionTorque(float rpm, float maxRpm, float frictionTorqueAtMaxRpm)
        {
            float rpmFraction = rpm / maxRpm;
            return frictionTorqueAtMaxRpm * (FrictionConstantShare
                + FrictionLinearShare * rpmFraction
                + FrictionQuadraticShare * rpmFraction * rpmFraction);
        }

        /// <summary>
        /// What the idle circuit asks for at zero throttle, N*m: a baseline that exactly balances
        /// friction at the idle target, plus a proportional pull-back the further the engine is
        /// dragged below it. Never negative - closing down to its own baseline is all the
        /// authority an idle circuit has; slowing the engine is friction's job.
        ///
        /// The baseline alone makes idleRpm an equilibrium (friction rises past it above, falls
        /// short of it below), but a passive balance like that is only as stiff as the slope of
        /// the friction curve, which near idle is very nearly flat - it took about ten seconds to
        /// settle. A real idle circuit is an active controller, so this is one too, and the gain
        /// sets the settling time directly instead of the friction value having to do that job on
        /// the side.
        /// </summary>
        public static float IdleGovernorTorque(float rpm, float idleRpm, float maxRpm, float frictionTorqueAtMaxRpm, float gain)
        {
            float baseline = EngineFrictionTorque(idleRpm, maxRpm, frictionTorqueAtMaxRpm);
            return Mathf.Max(0f, baseline + gain * (idleRpm - rpm));
        }

        /// <summary>
        /// Indicated torque (M_ind) the cylinders produce, N*m - the figure friction is then taken
        /// off of, so it is deliberately NOT what a spec sheet quotes.
        ///
        /// <paramref name="brakeTorqueFromCurve"/> IS what a spec sheet quotes: a published torque
        /// curve is brake torque, measured at the crank with friction and pumping already
        /// subtracted. Feeding it straight in as indicated torque and then taking friction off
        /// again charges the engine for its losses twice. Adding friction back on here means full
        /// throttle nets out to exactly the curve the real engine makes, whatever the friction
        /// value is set to - which leaves that value free to describe engine braking honestly
        /// instead of being tuned around the torque budget.
        ///
        /// The idle floor keeps a light dab of gas from producing less than no gas at all: low
        /// down, curve * throttle can easily land under what the idle circuit already supplies.
        /// </summary>
        public static float CombustionTorque(float brakeTorqueFromCurve, float frictionTorque, float idleGovernorTorque, float throttle)
        {
            float indicated = throttle * (brakeTorqueFromCurve + frictionTorque);
            return Mathf.Max(indicated, idleGovernorTorque);
        }

        /// <summary>
        /// One integration step for a freely-turning (not rigidly locked) engine: net torque over
        /// inertia gives an angular acceleration, applied directly - no separate rate cap. An
        /// earlier version also clamped the step via Mathf.MoveTowards at a fixed RPM/s, which
        /// created an artificial two-phase response (torque-limited, then suddenly capped) with
        /// no physical basis; the torque/inertia calculation alone already gives a believable,
        /// continuous response.
        /// </summary>
        public static float IntegrateEngineRpm(float currentRpm, float engineNetTorque, float clutchReactionTorque, float engineInertia, float deltaTime)
        {
            float netTorque = engineNetTorque - clutchReactionTorque;
            float rpmAccelPerSecond = netTorque / engineInertia * 60f / (2f * Mathf.PI);
            return currentRpm + rpmAccelPerSecond * deltaTime;
        }

        /// <summary>
        /// The clutch torque that makes engine and driveline behave as ONE rigid rotating mass for
        /// this step - i.e. what a fully engaged clutch plate does. Returned unclamped: the caller
        /// compares it against the plate's capacity, and a result beyond that capacity is precisely
        /// what "the clutch slips" means, so no separate state machine or lock epsilon is needed.
        ///
        /// Derivation. With the constraint omega_engine = ratio * omega_driveline, so that both
        /// sides must share one acceleration:
        ///
        ///     (M_engine - T) / J_e  =  ratio * (T * ratio * eff + M_external) / J_d
        ///
        /// solved for T, plus a velocity term that also closes whatever gap is already open, in
        /// one step. Both together:
        ///
        ///             (w_e - ratio * w_d) / dt  +  M_engine / J_e  -  ratio * M_external / J_d
        ///     T  =  -------------------------------------------------------------------------
        ///                          1 / J_e  +  ratio^2 * eff / J_d
        ///
        /// This replaced a proportional coupling (torque from the RPM gap times a stiffness). That
        /// one was a spring between two small inertias with a large ratio between them, and
        /// explicit integration at a 50 Hz fixed step diverged for the short gears: below roughly
        /// second gear the step exceeded twice the loop's time constant and the clutch torque
        /// oscillated between its limits every step, flipping the driven wheels' direction of
        /// rotation. A constraint has no such stiffness limit - it is solved, not integrated - so
        /// this is stable for any gear and any step size. A proportional coupling also needs a
        /// permanent RPM gap to carry torque at all (gap = torque / stiffness, several hundred RPM
        /// in normal driving); here a closed clutch means the gap is genuinely zero.
        ///
        /// <paramref name="totalRatio"/> is signed - gear times final drive times drive direction -
        /// so reverse falls out of the same expression.
        /// </summary>
        public static float LockedClutchTorque(
            float engineAngularVelocity,
            float drivelineAngularVelocity,
            float engineNetTorque,
            float drivelineExternalTorque,
            float engineInertia,
            float drivelineInertia,
            float totalRatio,
            float efficiency,
            float deltaTime)
        {
            float velocityGap = engineAngularVelocity - totalRatio * drivelineAngularVelocity;

            float demand = velocityGap / deltaTime
                + engineNetTorque / engineInertia
                - totalRatio * drivelineExternalTorque / drivelineInertia;

            float coupledInverseInertia = 1f / engineInertia
                + totalRatio * totalRatio * efficiency / drivelineInertia;

            return demand / coupledInverseInertia;
        }

        /// <summary>
        /// The driven axle as the engine sees it through an open differential: the carrier turns
        /// at the mean of the two wheel speeds, and carries both wheels' inertia and everything
        /// the road does to them. For equal wheel inertias this is exact, not an approximation -
        /// with the torque split evenly (see <see cref="OpenDifferentialWheelTorque"/>) the mean
        /// wheel acceleration is (T + M_left + M_right) / (J_left + J_right).
        ///
        /// Kept as its own named step so a limited-slip or locked differential can replace it
        /// later without touching the tire model or the clutch.
        /// </summary>
        public static DrivelineLoad OpenDifferentialLoad(
            float leftAngularVelocity,
            float rightAngularVelocity,
            float leftInertia,
            float rightInertia,
            float leftExternalTorque,
            float rightExternalTorque)
        {
            return new DrivelineLoad(
                (leftAngularVelocity + rightAngularVelocity) * 0.5f,
                leftInertia + rightInertia,
                leftExternalTorque + rightExternalTorque);
        }

        /// <summary>
        /// Each wheel's share of the carrier torque. An open differential splits evenly whatever
        /// the grip, so a wheel spinning on nothing caps what the other one can put down.
        /// </summary>
        public static float OpenDifferentialWheelTorque(float carrierTorque)
        {
            return carrierTorque * 0.5f;
        }

        /// <summary>
        /// What the clutch may pass on during a launch, N*m, from engine speed alone - the take-up
        /// a driver does with the pedal, for a clutch that is only a key. Nothing at or below
        /// <paramref name="engageRpm"/>, so letting go at idle cannot stall the engine; the whole
        /// plate from <paramref name="fullRpm"/> up. Quadratic in between, so it bites softly
        /// first. With throttle the engine climbs until what it makes matches what the clutch
        /// takes, and the clutch slips there until the car has caught up with it.
        /// </summary>
        public static float LaunchClutchCapacity(float rpm, float engageRpm, float fullRpm, float maxClutchTorque)
        {
            if (fullRpm <= engageRpm)
            {
                return rpm > engageRpm ? maxClutchTorque : 0f;
            }

            float takeUp = Mathf.Clamp01((rpm - engageRpm) / (fullRpm - engageRpm));
            return maxClutchTorque * takeUp * takeUp;
        }

        /// <summary>
        /// Whether the launch assist stays in charge for the next step. It ends the first time the
        /// clutch holds - engine and wheels turning together is what "launched" means - and from
        /// then on the plate works with its full capacity, so the engine can be stalled again like
        /// any manual. Only a standstill or the clutch key brings it back: a clutch that slips for a
        /// moment during a gear shift is not a launch.
        /// </summary>
        public static bool LaunchAssistActive(bool wasActive, bool clutchHeld, bool standing, bool clutchSlipping)
        {
            if (clutchHeld || standing)
            {
                return true;
            }

            return wasActive && clutchSlipping;
        }

        /// <summary>
        /// Road speed, m/s, at which the driven wheels turn the engine at <paramref name="engineRpm"/>
        /// through <paramref name="totalRatio"/> (gear times final drive) with the clutch closed.
        /// Zero for a zero ratio - that gear does not exist.
        /// </summary>
        public static float EngineRpmToRoadSpeed(float engineRpm, float totalRatio, float wheelRadius)
        {
            if (Mathf.Approximately(totalRatio, 0f))
            {
                return 0f;
            }

            return engineRpm * 2f * Mathf.PI / 60f / Mathf.Abs(totalRatio) * wheelRadius;
        }
    }
}
