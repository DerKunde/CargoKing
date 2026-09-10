using CargoKing.Car;
using static CargoKing.Car.DriveTrainMath;
using NUnit.Framework;
using UnityEngine;

namespace CargoKing.Tests
{
    public class DriveTrainMathTests
    {
        [Test]
        public void ClutchTorque_WithinCapacity_IsProportionalToRpmGap()
        {
            float torque = DriveTrainMath.ClutchTorque(engineRpm: 1500f, wheelRpmGeared: 1000f, clutchStiffness: 0.2f, maxClutchTorque: 220f);
            Assert.AreEqual(100f, torque, 0.01f);
        }

        [Test]
        public void ClutchTorque_LargeRpmGap_ClampsToMaxTorque()
        {
            float torque = DriveTrainMath.ClutchTorque(engineRpm: 4000f, wheelRpmGeared: 0f, clutchStiffness: 0.2f, maxClutchTorque: 220f);
            Assert.AreEqual(220f, torque, 0.01f);
        }

        [Test]
        public void ClutchTorque_EngineSlowerThanWheel_IsNegative()
        {
            float torque = DriveTrainMath.ClutchTorque(engineRpm: 1000f, wheelRpmGeared: 1500f, clutchStiffness: 0.2f, maxClutchTorque: 220f);
            Assert.AreEqual(-100f, torque, 0.01f);
        }

        [Test]
        public void IsLocked_WithinEpsilon_ReturnsTrue()
        {
            Assert.IsTrue(DriveTrainMath.IsLocked(2010f, 2000f, lockEpsilonRpm: 50f));
        }

        [Test]
        public void IsLocked_OutsideEpsilon_ReturnsFalse()
        {
            Assert.IsFalse(DriveTrainMath.IsLocked(2200f, 2000f, lockEpsilonRpm: 50f));
        }

        [Test]
        public void IsStalled_BelowStallRpm_ReturnsTrue()
        {
            Assert.IsTrue(DriveTrainMath.IsStalled(500f, stallRpm: 600f));
        }

        [Test]
        public void IsStalled_AtOrAboveStallRpm_ReturnsFalse()
        {
            Assert.IsFalse(DriveTrainMath.IsStalled(600f, stallRpm: 600f));
        }

        [Test]
        public void AngularVelocityToRpm_OneRadPerSecond_MatchesConversionFactor()
        {
            float rpm = DriveTrainMath.AngularVelocityToRpm(2f * Mathf.PI / 60f);
            Assert.AreEqual(1f, rpm, 0.001f);
        }

        [Test]
        public void IntegrateEngineRpm_PositiveNetTorque_IncreasesRpm()
        {
            float newRpm = DriveTrainMath.IntegrateEngineRpm(currentRpm: 1000f, engineNetTorque: 100f, clutchReactionTorque: 0f, engineInertia: 0.15f, deltaTime: 0.02f);
            Assert.Greater(newRpm, 1000f);
        }

        [Test]
        public void IntegrateEngineRpm_ReactionTorqueExceedsCombustion_DecreasesRpm()
        {
            float newRpm = DriveTrainMath.IntegrateEngineRpm(currentRpm: 1000f, engineNetTorque: 50f, clutchReactionTorque: 220f, engineInertia: 0.15f, deltaTime: 0.02f);
            Assert.Less(newRpm, 1000f);
        }

        [Test]
        public void IntegrateEngineRpm_MatchesTorqueOverInertiaDirectly()
        {
            // No separate rate cap any more - the result must equal the plain torque/inertia
            // integration, however large the net torque is.
            float newRpm = DriveTrainMath.IntegrateEngineRpm(currentRpm: 1000f, engineNetTorque: 10000f, clutchReactionTorque: 0f, engineInertia: 0.15f, deltaTime: 1f);
            float expectedRpm = 1000f + 10000f / 0.15f * 60f / (2f * Mathf.PI);

            // Relative tolerance: at ~637,000 rpm a float cannot resolve anything finer than
            // 0.0625, and the compiler folds the expected value in float while Unity's Mono keeps
            // the runtime intermediates in double - the two land one float step apart. A rate cap
            // would miss by thousands of rpm, so this still catches what the test is about.
            Assert.AreEqual(expectedRpm, newRpm, expectedRpm * 1e-6f);
        }

        [Test]
        public void EngineFrictionTorque_NearIdle_KeepsTheSpeedIndependentShare()
        {
            // A pure square of the RPM fraction goes to nothing at the bottom of the range - it
            // would put a 30 N*m engine at 0.8 N*m at 1000 rpm, where a 1.2 l four really loses
            // around 16 N*m to friction and pumping. That matters: idle sits exactly there.
            float torque = DriveTrainMath.EngineFrictionTorque(rpm: 1000f, maxRpm: 6000f, frictionTorqueAtMaxRpm: 30f);
            Assert.AreEqual(15.5f, torque, 0.5f);
        }

        [Test]
        public void EngineFrictionTorque_GrowsMonotonicallyWithRpm()
        {
            // The idle equilibrium argument rests on this and nothing else about the shape: the
            // governor holds a fixed torque, so friction must be the term that rises past it.
            float previous = -1f;
            for (float rpm = 0f; rpm <= 6000f; rpm += 250f)
            {
                float torque = DriveTrainMath.EngineFrictionTorque(rpm, maxRpm: 6000f, frictionTorqueAtMaxRpm: 30f);
                Assert.Greater(torque, previous, $"friction must keep rising, broke at {rpm} rpm");
                previous = torque;
            }
        }

        [Test]
        public void EngineFrictionTorque_AtMaxRpm_EqualsInputTorque()
        {
            float torque = DriveTrainMath.EngineFrictionTorque(rpm: 6000f, maxRpm: 6000f, frictionTorqueAtMaxRpm: 80f);
            Assert.AreEqual(80f, torque, 0.01f);
        }

        [Test]
        public void IdleGovernorTorque_AtIdleRpm_ExactlyBalancesFriction()
        {
            // The equilibrium the whole idle behaviour rests on: sitting at the target, the
            // governor asks for precisely what friction takes away, so net torque is zero.
            float torque = DriveTrainMath.IdleGovernorTorque(
                rpm: 1000f, idleRpm: 1000f, maxRpm: 6000f, frictionTorqueAtMaxRpm: 30f, gain: 0.022f);

            Assert.AreEqual(DriveTrainMath.EngineFrictionTorque(1000f, 6000f, 30f), torque, 0.01f);
        }

        [Test]
        public void IdleGovernorTorque_BelowIdle_AddsTorqueProportionalToTheShortfall()
        {
            // A real idle circuit is an active controller, not a fixed torque: the further the
            // engine is dragged below its target, the harder it pulls back.
            float atIdle = DriveTrainMath.IdleGovernorTorque(1000f, 1000f, 6000f, 30f, gain: 0.022f);
            float below = DriveTrainMath.IdleGovernorTorque(800f, 1000f, 6000f, 30f, gain: 0.022f);

            Assert.AreEqual(atIdle + 0.022f * 200f, below, 0.01f);
        }

        [Test]
        public void IdleGovernorTorque_WellAboveIdle_FallsToZero()
        {
            // It can only add torque, never remove it - closing down past its own baseline is all
            // the authority an idle circuit has. Slowing the engine is friction's job.
            float torque = DriveTrainMath.IdleGovernorTorque(4000f, 1000f, 6000f, 30f, gain: 0.022f);
            Assert.AreEqual(0f, torque, 0.001f);
        }

        // Shared engine constants for the idle-behaviour cases below: clutch held (no reaction
        // torque) and throttle shut, so combustion torque is whatever the governor asks for.
        private const float IdleRpm = 1000f;
        private const float MaxRpm = 6000f;
        private const float FrictionAtMax = 30f;
        private const float IdleGain = 0.05f;

        private static float CoastHeldEngine(float startRpm, int steps)
        {
            const float engineInertia = 0.15f;
            const float deltaTime = 0.02f;

            float rpm = startRpm;
            for (int i = 0; i < steps; i++)
            {
                float friction = DriveTrainMath.EngineFrictionTorque(rpm, MaxRpm, FrictionAtMax);
                float governor = DriveTrainMath.IdleGovernorTorque(rpm, IdleRpm, MaxRpm, FrictionAtMax, IdleGain);
                rpm = Mathf.Clamp(
                    DriveTrainMath.IntegrateEngineRpm(rpm, governor - friction, 0f, engineInertia, deltaTime),
                    0f,
                    MaxRpm);
            }

            return rpm;
        }

        [Test]
        public void HeldEngine_FromHighRpm_CoastsDownToTheIdleRegionInAboutTwoAndAHalfSeconds()
        {
            // Above the idle region the governor contributes nothing, so this leg is friction
            // alone - and it is the leg that has to look real: a declutched 1.2 l four falls from
            // 4000 rpm to idle in roughly two to three seconds. It must not undershoot either;
            // arriving below the target would mean friction is overstated.
            float rpm = CoastHeldEngine(startRpm: 4000f, steps: 125);

            Assert.That(rpm, Is.InRange(IdleRpm, 1300f));
        }

        [Test]
        public void HeldEngine_JustAboveIdle_SettlesOnTheTargetWithinASecond()
        {
            // The governor's own job, separate from the coast down. A passive friction balance
            // took some ten seconds here, which is what drove the friction value up to three
            // times anything real just to make idle behave.
            float rpm = CoastHeldEngine(startRpm: 1300f, steps: 60);

            Assert.AreEqual(IdleRpm, rpm, 10f);
        }

        [Test]
        public void CombustionTorque_AtFullThrottle_NetsOutToTheBrakeCurveValue()
        {
            // A published torque curve is brake torque - friction and pumping are already taken
            // off it. Subtracting friction again on top of that robs the engine twice, so the
            // curve value is put back up to an indicated figure here and the friction term then
            // brings it down to exactly where the curve said.
            const float brakeTorqueFromCurve = 108f;
            const float friction = 20f;

            float combustion = DriveTrainMath.CombustionTorque(
                brakeTorqueFromCurve, friction, idleGovernorTorque: 15f, throttle: 1f);

            Assert.AreEqual(brakeTorqueFromCurve, combustion - friction, 0.01f);
        }

        [Test]
        public void CombustionTorque_AtZeroThrottle_FallsBackToTheIdleGovernor()
        {
            float combustion = DriveTrainMath.CombustionTorque(
                brakeTorqueFromCurve: 108f, frictionTorque: 20f, idleGovernorTorque: 15f, throttle: 0f);

            Assert.AreEqual(15f, combustion, 0.01f);
        }

        [Test]
        public void CombustionTorque_AtLightThrottle_NeverDropsBelowTheIdleGovernor()
        {
            // A light dab of gas low down asks for less than the idle circuit already provides.
            // Without the floor it would produce less torque than no gas at all and could stall
            // the engine on its own, with no clutch or load involved.
            float combustion = DriveTrainMath.CombustionTorque(
                brakeTorqueFromCurve: 3f, frictionTorque: 15.5f, idleGovernorTorque: 15.5f, throttle: 0.05f);

            Assert.AreEqual(15.5f, combustion, 0.01f);
        }

        // --- Locked clutch (rigid engine + driveline) ---------------------------------------
        //
        // Shared reference case: engine 0.15 kg*m^2 against the two driven wheels (2 * 1.225),
        // first gear (3.2 * 4 axle), 85% driveline efficiency, one 50 Hz physics step.
        private const float Je = 0.15f;
        private const float Jd = 2.45f;
        private const float Ratio = 12.8f;
        private const float Eff = 0.85f;
        private const float Step = 0.02f;

        private static float EngineAcceleration(float clutchTorque, float engineNetTorque)
        {
            return (engineNetTorque - clutchTorque) / Je;
        }

        private static float DrivelineAcceleration(float clutchTorque, float drivelineExternalTorque)
        {
            return (clutchTorque * Ratio * Eff + drivelineExternalTorque) / Jd;
        }

        [Test]
        public void LockedClutchTorque_ZeroGapUnderEngineTorque_CouplesBothSidesEqually()
        {
            const float engineNetTorque = 100f;
            float drivelineAngularVelocity = 10f;
            float engineAngularVelocity = drivelineAngularVelocity * Ratio;

            float torque = DriveTrainMath.LockedClutchTorque(
                engineAngularVelocity, drivelineAngularVelocity,
                engineNetTorque, drivelineExternalTorque: 0f,
                Je, Jd, Ratio, Eff, Step);

            // The whole point of a rigid coupling: the driveline, geared up, must accelerate at
            // exactly the engine's rate. Anything else means the two are still moving apart.
            Assert.AreEqual(
                EngineAcceleration(torque, engineNetTorque),
                DrivelineAcceleration(torque, 0f) * Ratio,
                0.01f);
        }

        [Test]
        public void LockedClutchTorque_ExistingGap_ClosesItWithinOneStep()
        {
            float drivelineAngularVelocity = 10f;
            float engineAngularVelocity = 200f; // geared driveline sits at 128 rad/s, so a big gap

            float torque = DriveTrainMath.LockedClutchTorque(
                engineAngularVelocity, drivelineAngularVelocity,
                engineNetTorque: 0f, drivelineExternalTorque: 0f,
                Je, Jd, Ratio, Eff, Step);

            float engineAfter = engineAngularVelocity + EngineAcceleration(torque, 0f) * Step;
            float drivelineAfter = drivelineAngularVelocity + DrivelineAcceleration(torque, 0f) * Step;

            Assert.AreEqual(engineAfter, drivelineAfter * Ratio, 0.01f);
        }

        [Test]
        public void LockedClutchTorque_DrivelineUnderExternalLoad_StillHoldsTheCoupling()
        {
            // A braking / rolling-resistance reaction on the wheels. Without accounting for it the
            // coupling drifts apart under load, which is exactly the phantom RPM offset we are
            // trying to avoid.
            const float drivelineExternalTorque = -500f;
            float drivelineAngularVelocity = 10f;
            float engineAngularVelocity = drivelineAngularVelocity * Ratio;

            float torque = DriveTrainMath.LockedClutchTorque(
                engineAngularVelocity, drivelineAngularVelocity,
                engineNetTorque: 0f, drivelineExternalTorque,
                Je, Jd, Ratio, Eff, Step);

            Assert.AreEqual(
                EngineAcceleration(torque, 0f),
                DrivelineAcceleration(torque, drivelineExternalTorque) * Ratio,
                0.01f);
        }

        /// <summary>
        /// Regression guard for the standing start in first gear. This is the case the previous
        /// proportional clutch fell apart on: the driven wheels reversed their direction of
        /// rotation on every single physics step while the clutch slammed between its limits, and
        /// the engine hung just above stall while the car crawled at walking pace.
        ///
        /// Runs the engine, the two driven wheels and the car body against each other for four
        /// seconds the way CarEngine and Suspension do - deliberately as plain math, so this
        /// covers DriveTrainMath rather than the MonoBehaviours around it.
        /// </summary>
        private static void SimulateStandingStart(
            out float minimumWheelAngularVelocity,
            out float minimumEngineRpm,
            out float finalSpeed)
        {
            const float maxClutchTorque = 220f;
            const float wheelRadius = 0.35f;
            const float driveSlipStiffness = 250f;
            const float carMass = 800f;
            const float engineInertia = 0.15f;
            const float deltaTime = 0.02f;
            const float brakeTorqueFromCurve = 80f; // what the real curve gives just off idle

            float engineAngularVelocity = IdleRpm * 2f * Mathf.PI / 60f;
            float drivelineAngularVelocity = 0f;
            float carSpeed = 0f;

            minimumWheelAngularVelocity = float.MaxValue;
            minimumEngineRpm = float.MaxValue;

            for (int i = 0; i < 200; i++)
            {
                float rpm = AngularVelocityToRpm(engineAngularVelocity);
                float friction = EngineFrictionTorque(rpm, MaxRpm, FrictionAtMax);
                float governor = IdleGovernorTorque(rpm, IdleRpm, MaxRpm, FrictionAtMax, IdleGain);
                float engineNetTorque = CombustionTorque(brakeTorqueFromCurve, friction, governor, throttle: 1f) - friction;

                // Both driven tires pushing against the ground, and the ground pushing back on
                // their spin - the same slip-proportional force Suspension builds.
                float slipVelocity = drivelineAngularVelocity * wheelRadius - carSpeed;
                float driveForce = 2f * driveSlipStiffness * slipVelocity;
                float drivelineExternalTorque = -driveForce * wheelRadius;

                float clutchTorque = Mathf.Clamp(
                    LockedClutchTorque(
                        engineAngularVelocity, drivelineAngularVelocity,
                        engineNetTorque, drivelineExternalTorque,
                        engineInertia, Jd, Ratio, Eff, deltaTime),
                    -maxClutchTorque,
                    maxClutchTorque);

                engineAngularVelocity += (engineNetTorque - clutchTorque) / engineInertia * deltaTime;
                drivelineAngularVelocity += (clutchTorque * Ratio * Eff + drivelineExternalTorque) / Jd * deltaTime;
                carSpeed += driveForce / carMass * deltaTime;

                minimumWheelAngularVelocity = Mathf.Min(minimumWheelAngularVelocity, drivelineAngularVelocity);
                minimumEngineRpm = Mathf.Min(minimumEngineRpm, AngularVelocityToRpm(engineAngularVelocity));
            }

            finalSpeed = carSpeed;
        }

        [Test]
        public void StandingStartInFirstGear_NeverSpinsTheDrivenWheelsBackwards()
        {
            SimulateStandingStart(out float minimumWheelAngularVelocity, out _, out _);

            Assert.GreaterOrEqual(minimumWheelAngularVelocity, 0f);
        }

        [Test]
        public void StandingStartInFirstGear_KeepsTheEngineAboveStall()
        {
            SimulateStandingStart(out _, out float minimumEngineRpm, out _);

            Assert.Greater(minimumEngineRpm, 600f);
        }

        [Test]
        public void StandingStartInFirstGear_ActuallyPullsTheCarAway()
        {
            SimulateStandingStart(out _, out _, out float finalSpeed);

            Assert.Greater(finalSpeed, 3f);
        }

        // --- Open differential ----------------------------------------------------------------

        [Test]
        public void OpenDifferentialLoad_AveragesSpeedsAndSumsInertiaAndTorque()
        {
            DrivelineLoad load = DriveTrainMath.OpenDifferentialLoad(
                leftAngularVelocity: 10f, rightAngularVelocity: 14f,
                leftInertia: 1.2f, rightInertia: 1.2f,
                leftExternalTorque: -100f, rightExternalTorque: -300f);

            Assert.AreEqual(12f, load.AngularVelocity, 1e-5f);
            Assert.AreEqual(2.4f, load.Inertia, 1e-5f);
            Assert.AreEqual(-400f, load.ExternalTorque, 1e-4f);
        }

        [Test]
        public void OpenDifferentialWheelTorque_OneWheelWithoutGrip_StillGetsHalf()
        {
            // The defining trait of an open differential: torque is split evenly regardless of
            // grip, so a wheel spinning on nothing caps what the other one can put down.
            Assert.AreEqual(50f, DriveTrainMath.OpenDifferentialWheelTorque(100f), 1e-5f);
        }
    }
}
