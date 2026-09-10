using CargoKing.Car;
using NUnit.Framework;
using UnityEngine;

namespace CargoKing.Tests
{
    public class TireMathTests
    {
        // Start values from the design spec: peak at 10% slip and at 7 degrees of slip angle.
        private static readonly PacejkaCurve Longitudinal = new PacejkaCurve(14f, 1.65f, 0.9f, 0f);
        private static readonly PacejkaCurve Lateral = new PacejkaCurve(0.376f, 1.3f, 0.9f, 0f);

        // --- Magic Formula ------------------------------------------------------------------

        [Test]
        public void MagicFormula_AtZeroSlip_IsZero()
        {
            Assert.AreEqual(0f, TireMath.MagicFormula(0f, Longitudinal), 1e-6f);
        }

        [Test]
        public void MagicFormula_IsOdd()
        {
            // Braking and driving are the same curve mirrored - a tire does not care which way
            // the slip points.
            foreach (float x in new[] { 0.02f, 0.1f, 0.4f, 1f })
            {
                Assert.AreEqual(
                    -TireMath.MagicFormula(x, Longitudinal),
                    TireMath.MagicFormula(-x, Longitudinal),
                    1e-6f);
            }
        }

        [Test]
        public void MagicFormula_WithoutCurvatureTerm_PeaksAtD()
        {
            float peak = Mathf.Tan(Mathf.PI / (2f * Longitudinal.c)) / Longitudinal.b;
            Assert.AreEqual(Longitudinal.d, TireMath.MagicFormula(peak, Longitudinal), 1e-4f);
        }

        [Test]
        public void MagicFormula_FarPastThePeak_FallsTowardsDTimesSinCHalfPi()
        {
            // What a fully spinning tire keeps: with E = 0 the curve settles at D * sin(C * pi/2),
            // for the start values about 52% of the peak. This is the number the preview shows
            // as "left when spinning".
            float expected = Longitudinal.d * Mathf.Sin(Longitudinal.c * Mathf.PI / 2f);
            Assert.AreEqual(expected, TireMath.MagicFormula(1000f, Longitudinal), 1e-3f);
        }

        // --- Peak position ------------------------------------------------------------------

        [Test]
        public void PeakInput_WithoutCurvatureTerm_MatchesClosedForm()
        {
            float peak = TireMath.PeakInput(Longitudinal, searchLimit: 1f, out bool hasPeak);

            Assert.IsTrue(hasPeak);
            Assert.AreEqual(Mathf.Tan(Mathf.PI / (2f * Longitudinal.c)) / Longitudinal.b, peak, 1e-4f);
        }

        [Test]
        public void PeakInput_LateralStartValues_SitAtSevenDegrees()
        {
            float peak = TireMath.PeakInput(Lateral, searchLimit: 30f, out bool hasPeak);

            Assert.IsTrue(hasPeak);
            Assert.AreEqual(7f, peak, 0.01f);
        }

        [TestCase(0.5f)]
        [TestCase(-1f)]
        [TestCase(0.9f)]
        public void PeakInput_WithCurvatureTerm_MatchesTheSampledMaximum(float e)
        {
            // E moves the peak, and there is no closed form for where to - so the search has to
            // land on the same spot a brute-force sampling does.
            var curve = new PacejkaCurve(10f, 1.9f, 1f, e);
            const float limit = 1f;

            float bestX = 0f;
            float best = float.MinValue;
            for (int i = 1; i <= 100000; i++)
            {
                float x = limit * i / 100000f;
                float value = TireMath.MagicFormula(x, curve);
                if (value > best)
                {
                    best = value;
                    bestX = x;
                }
            }

            float peak = TireMath.PeakInput(curve, limit, out bool hasPeak);

            Assert.IsTrue(hasPeak);
            Assert.AreEqual(bestX, peak, 1e-3f);
        }

        [Test]
        public void PeakInput_CurveWithoutPeak_ReportsNoneAndReturnsTheSearchLimit()
        {
            // C <= 1 never turns over: the force keeps rising with slip. There is no peak to
            // normalise combined slip with, so the range end stands in and the inspector warns.
            var curve = new PacejkaCurve(10f, 0.9f, 1f, 0f);

            float peak = TireMath.PeakInput(curve, searchLimit: 1f, out bool hasPeak);

            Assert.IsFalse(hasPeak);
            Assert.AreEqual(1f, peak, 1e-6f);
        }

        // --- Load sensitivity ---------------------------------------------------------------

        private const float NominalLoad = 1962f; // Car_v2: 800 kg * 9.81 / 4
        private const float Sensitivity = 0.1f;

        [Test]
        public void LoadFactor_AtNominalLoad_IsOne()
        {
            Assert.AreEqual(1f, TireMath.LoadFactor(NominalLoad, NominalLoad, Sensitivity), 1e-6f);
        }

        [Test]
        public void LoadFactor_AtDoubleLoad_LosesTheSensitivity()
        {
            // Twice the load brings 1.8 times the grip, not twice - the reason load transfer
            // costs an axle grip overall.
            Assert.AreEqual(1f - Sensitivity, TireMath.LoadFactor(2f * NominalLoad, NominalLoad, Sensitivity), 1e-6f);
        }

        [Test]
        public void LoadFactor_BelowNominal_GivesMoreGripPerNewton()
        {
            Assert.AreEqual(1f + 0.5f * Sensitivity, TireMath.LoadFactor(0.5f * NominalLoad, NominalLoad, Sensitivity), 1e-6f);
        }

        [Test]
        public void LoadFactor_ExtremeLoad_ClampsAtHalf()
        {
            // Linear falloff would go negative on a hard landing and turn grip into anti-grip.
            Assert.AreEqual(0.5f, TireMath.LoadFactor(20f * NominalLoad, NominalLoad, Sensitivity), 1e-6f);
        }

        // --- Slip ratio and slip angle ------------------------------------------------------

        private const float MinSpeed = 1f;

        [Test]
        public void SlipRatio_FreeRolling_IsZero()
        {
            Assert.AreEqual(0f, TireMath.SlipRatio(wheelSurfaceSpeed: 10f, forwardVelocity: 10f, MinSpeed), 1e-6f);
        }

        [Test]
        public void SlipRatio_WheelTenPercentFaster_IsPointOne()
        {
            Assert.AreEqual(0.1f, TireMath.SlipRatio(wheelSurfaceSpeed: 11f, forwardVelocity: 10f, MinSpeed), 1e-6f);
        }

        [Test]
        public void SlipRatio_LockedWheelWhileReversing_PushesForward()
        {
            // The sign has to follow the slip velocity, not the direction of travel: a wheel
            // locked while rolling backwards must produce a force that slows the car, i.e. forward.
            Assert.AreEqual(1f, TireMath.SlipRatio(wheelSurfaceSpeed: 0f, forwardVelocity: -5f, MinSpeed), 1e-6f);
        }

        [Test]
        public void SlipRatio_AtStandstill_StaysFinite()
        {
            Assert.AreEqual(0.5f, TireMath.SlipRatio(wheelSurfaceSpeed: 0.5f, forwardVelocity: 0f, MinSpeed), 1e-6f);
        }

        [Test]
        public void SlipAngle_IsTheAngleOfTheContactVelocityInDegrees()
        {
            float expected = Mathf.Atan(0.1f) * Mathf.Rad2Deg;
            Assert.AreEqual(expected, TireMath.SlipAngleDegrees(lateralVelocity: 1f, forwardVelocity: 10f, MinSpeed), 1e-4f);
        }

        [Test]
        public void SlipAngle_WhileReversing_KeepsItsSign()
        {
            // Measured against the rolling speed, not the signed forward velocity: otherwise the
            // lateral force would flip and push the sideways slide further when reversing.
            Assert.AreEqual(
                TireMath.SlipAngleDegrees(1f, 10f, MinSpeed),
                TireMath.SlipAngleDegrees(1f, -10f, MinSpeed),
                1e-6f);
        }

        [Test]
        public void SlipAngle_AtStandstill_StaysFinite()
        {
            float expected = Mathf.Atan(0.1f) * Mathf.Rad2Deg;
            Assert.AreEqual(expected, TireMath.SlipAngleDegrees(lateralVelocity: 0.1f, forwardVelocity: 0f, MinSpeed), 1e-4f);
        }

        // --- Combined slip ------------------------------------------------------------------

        private const float GripLoad = 1000f;
        private static readonly float PeakSlip = Mathf.Tan(Mathf.PI / (2f * Longitudinal.c)) / Longitudinal.b;
        private static readonly float PeakAngle = Mathf.Tan(Mathf.PI / (2f * Lateral.c)) / Lateral.b;

        private static Vector2 Combined(float slipRatio, float slipAngle)
        {
            return TireMath.CombinedForce(slipRatio, slipAngle, Longitudinal, Lateral, PeakSlip, PeakAngle, GripLoad);
        }

        [Test]
        public void CombinedForce_WithoutSlipAngle_IsThePureLongitudinalCurve()
        {
            Vector2 force = Combined(slipRatio: 0.05f, slipAngle: 0f);

            Assert.AreEqual(GripLoad * TireMath.MagicFormula(0.05f, Longitudinal), force.x, 1e-2f);
            Assert.AreEqual(0f, force.y, 1e-4f);
        }

        [Test]
        public void CombinedForce_WithoutSlip_IsThePureLateralCurve()
        {
            Vector2 force = Combined(slipRatio: 0f, slipAngle: 3f);

            Assert.AreEqual(0f, force.x, 1e-4f);
            Assert.AreEqual(GripLoad * TireMath.MagicFormula(3f, Lateral), force.y, 1e-2f);
        }

        [Test]
        public void CombinedForce_AtZeroSlip_IsZero()
        {
            Vector2 force = Combined(0f, 0f);

            Assert.AreEqual(0f, force.x, 1e-6f);
            Assert.AreEqual(0f, force.y, 1e-6f);
        }

        [Test]
        public void CombinedForce_SignsFollowTheSlip()
        {
            Vector2 force = Combined(slipRatio: -0.05f, slipAngle: -3f);

            Assert.Less(force.x, 0f);
            Assert.Less(force.y, 0f);
        }

        [Test]
        public void CombinedForce_CorneringAtThePeak_CutsTheDriveForceWellDown()
        {
            // GitHub #14: at the cornering limit the old friction circle still passed on 96% of
            // the drive force. A tire at its peak slip angle has to give up a large share of it.
            float straight = Combined(PeakSlip, 0f).x;
            float cornering = Combined(PeakSlip, PeakAngle).x;

            Assert.Less(cornering, 0.75f * straight);
        }

        [Test]
        public void CombinedForce_NeverExceedsThePeakGrip()
        {
            // Both curves peak at D = 0.9 here, so no combination of slip and angle may pull more
            // than 0.9 * load out of the contact patch.
            for (float s = -1f; s <= 1f; s += 0.05f)
            {
                for (float a = -30f; a <= 30f; a += 1f)
                {
                    Vector2 force = Combined(s, a);
                    Assert.LessOrEqual(force.magnitude, 0.9f * GripLoad + 1e-2f, $"slip {s}, angle {a}");
                }
            }
        }

        // --- Stability cap ------------------------------------------------------------------

        private const float QuarterBody = 200f;
        private const float Step = 0.02f;

        [Test]
        public void CapForce_SmallForce_PassesThrough()
        {
            Assert.AreEqual(100f, TireMath.CapForce(100f, slipVelocity: 1f, QuarterBody, Step), 1e-4f);
        }

        [Test]
        public void CapForce_LargeForce_IsClampedInBothDirections()
        {
            float cap = TireMath.StabilityFraction * QuarterBody * 1f / Step;

            Assert.AreEqual(cap, TireMath.CapForce(10000f, slipVelocity: 1f, QuarterBody, Step), 1e-2f);
            Assert.AreEqual(-cap, TireMath.CapForce(-10000f, slipVelocity: -1f, QuarterBody, Step), 1e-2f);
        }

        [Test]
        public void CapForce_CappedForce_RemovesAtMostTheStabilityFractionOfTheSlipInOneStep()
        {
            // The property the whole cap exists for: one explicit step can never overshoot the
            // slip it is correcting, so the force cannot flip sign step to step and chatter.
            const float slipVelocity = 0.3f;
            float force = TireMath.CapForce(50000f, slipVelocity, QuarterBody, Step);
            float velocityChange = force / QuarterBody * Step;

            Assert.LessOrEqual(velocityChange, TireMath.StabilityFraction * slipVelocity + 1e-5f);
        }

        [Test]
        public void CapForce_AtZeroSlip_IsZero()
        {
            Assert.AreEqual(0f, TireMath.CapForce(500f, slipVelocity: 0f, QuarterBody, Step), 1e-6f);
        }

        [Test]
        public void CapForce_LateralAtSpeed_LeavesThePacejkaForceAlone()
        {
            // 3 degrees of slip angle at 30 m/s on a loaded wheel: the curve is well inside what
            // the cap allows, so the tuned curve reaches the car unchanged.
            const float forward = 30f;
            float lateralVelocity = forward * Mathf.Tan(3f * Mathf.Deg2Rad);
            float pacejka = NominalLoad * TireMath.MagicFormula(3f, Lateral);

            Assert.AreEqual(pacejka, TireMath.CapForce(pacejka, lateralVelocity, referenceMass: 248f, Step), 1e-2f);
        }

        [Test]
        public void CapForce_LateralAtWalkingPace_TakesOver()
        {
            // The same 3 degrees at 2 m/s: the curve would ask for more than one step can safely
            // take out of so little sideways velocity, so the cap - today's deadbeat term - binds.
            const float forward = 2f;
            float lateralVelocity = forward * Mathf.Tan(3f * Mathf.Deg2Rad);
            float pacejka = NominalLoad * TireMath.MagicFormula(3f, Lateral);

            Assert.Less(TireMath.CapForce(pacejka, lateralVelocity, referenceMass: 248f, Step), pacejka);
        }

        // --- Friction torque (brake, rolling resistance) ------------------------------------

        [Test]
        public void ApplyFrictionTorque_SlowsASpinningWheel()
        {
            Assert.AreEqual(9f, TireMath.ApplyFrictionTorque(angularVelocity: 10f, frictionTorque: 100f, inertia: 1f, deltaTime: 0.01f), 1e-5f);
        }

        [Test]
        public void ApplyFrictionTorque_ActsAgainstEitherDirection()
        {
            Assert.AreEqual(-9f, TireMath.ApplyFrictionTorque(angularVelocity: -10f, frictionTorque: 100f, inertia: 1f, deltaTime: 0.01f), 1e-5f);
        }

        [Test]
        public void ApplyFrictionTorque_StopsTheWheelButNeverReversesIt()
        {
            // Friction can only take motion away. Enough brake torque locks the wheel; it must not
            // throw it backwards, which is what a plain signed torque does across zero.
            Assert.AreEqual(0f, TireMath.ApplyFrictionTorque(angularVelocity: 0.5f, frictionTorque: 100f, inertia: 1f, deltaTime: 0.01f), 1e-6f);
            Assert.AreEqual(0f, TireMath.ApplyFrictionTorque(angularVelocity: -0.5f, frictionTorque: 100f, inertia: 1f, deltaTime: 0.01f), 1e-6f);
        }

        [Test]
        public void ApplyFrictionTorque_WithoutTorque_LeavesTheWheelAlone()
        {
            Assert.AreEqual(10f, TireMath.ApplyFrictionTorque(angularVelocity: 10f, frictionTorque: 0f, inertia: 1f, deltaTime: 0.01f), 1e-6f);
        }

        // --- One wheel, one sub-step --------------------------------------------------------

        private const float WheelRadius = 0.35f;   // Car_v2 wheel mesh, 0.7 m across
        private const float WheelInertia = 1.225f; // 0.5 * 20 kg * 0.35^2
        private const float RollingResistance = 0.015f;
        private static readonly TireCurves Tire = new TireCurves(Longitudinal, Lateral, PeakSlip, PeakAngle, NominalLoad, Sensitivity);

        // Reference masses large enough that the stability cap stays out of the way, for the
        // cases that are about the curve rather than the cap.
        private const float Heavy = 1e6f;

        private static TireForces Evaluate(float angularVelocity, float forward, float lateral, float normalForce)
        {
            return TireMath.EvaluateTire(Tire, angularVelocity, WheelRadius, WheelInertia,
                forward, lateral, normalForce, Heavy, Heavy, Step, Step / 20f);
        }

        [Test]
        public void EvaluateTire_WithoutLoad_GivesNoForce()
        {
            TireForces forces = Evaluate(angularVelocity: 0f, forward: 20f, lateral: 2f, normalForce: 0f);

            Assert.AreEqual(0f, forces.Longitudinal, 1e-6f);
            Assert.AreEqual(0f, forces.Lateral, 1e-6f);
        }

        [Test]
        public void EvaluateTire_FreeRollingStraight_GivesNoForce()
        {
            TireForces forces = Evaluate(angularVelocity: 20f / WheelRadius, forward: 20f, lateral: 0f, NominalLoad);

            Assert.AreEqual(0f, forces.Longitudinal, 1e-3f);
            Assert.AreEqual(0f, forces.Lateral, 1e-3f);
        }

        [Test]
        public void EvaluateTire_SlidingSideways_PushesBackAgainstTheSlide()
        {
            TireForces forces = Evaluate(angularVelocity: 20f / WheelRadius, forward: 20f, lateral: 1f, NominalLoad);

            Assert.Less(forces.Lateral, 0f);
            Assert.Greater(forces.SlipAngle, 0f);
        }

        [Test]
        public void EvaluateTire_AtThePeakSlip_UsesAllTheGrip()
        {
            float angularVelocity = 20f * (1f + PeakSlip) / WheelRadius;
            TireForces forces = Evaluate(angularVelocity, forward: 20f, lateral: 0f, NominalLoad);

            Assert.AreEqual(1f, forces.GripUsage, 1e-3f);
            Assert.AreEqual(0.9f * NominalLoad, forces.Longitudinal, 1f);
        }

        [Test]
        public void IntegrateWheel_DriveTorqueAgainstTheTireReaction()
        {
            float next = TireMath.IntegrateWheel(angularVelocity: 10f, driveTorque: 300f, longitudinalForce: 500f,
                WheelRadius, WheelInertia, frictionTorque: 0f, deltaTime: 0.001f);

            Assert.AreEqual(10f + 0.001f / WheelInertia * (300f - 500f * WheelRadius), next, 1e-5f);
        }

        // --- Loop tests: one wheel and a quarter of the body, straight line -----------------
        //
        // Runs the sub-step loop the way CarController and Suspension do - body velocity frozen
        // across the sub-steps, averaged tire force applied once per 50 Hz step - as plain math,
        // so these cover TireMath rather than the MonoBehaviours around it.

        private static void SimulateQuarterCar(
            float startSpeed, float driveTorque, float brakeForce, int subSteps, float seconds,
            out float[] speed, out float[] force, out float[] angularVelocity, out float[] slip)
        {
            float h = Step / subSteps;
            int steps = Mathf.RoundToInt(seconds / Step);
            float frictionTorque = (brakeForce + RollingResistance * NominalLoad) * WheelRadius;

            float v = startSpeed;
            float w = startSpeed / WheelRadius;

            speed = new float[steps];
            force = new float[steps];
            angularVelocity = new float[steps];
            slip = new float[steps];

            for (int k = 0; k < steps; k++)
            {
                float impulse = 0f;
                float lastSlip = 0f;
                for (int i = 0; i < subSteps; i++)
                {
                    TireForces tire = TireMath.EvaluateTire(Tire, w, WheelRadius, WheelInertia,
                        v, 0f, NominalLoad, QuarterBody, QuarterBody, Step, h);
                    w = TireMath.IntegrateWheel(w, driveTorque, tire.Longitudinal, WheelRadius, WheelInertia, frictionTorque, h);
                    impulse += tire.Longitudinal * h;
                    lastSlip = tire.SlipRatio;
                }

                float averageForce = impulse / Step;
                v += averageForce / QuarterBody * Step;

                speed[k] = v;
                force[k] = averageForce;
                angularVelocity[k] = w;
                slip[k] = lastSlip;
            }
        }

        private const int DefaultSubSteps = 20;
        private const float MotorwaySpeed = 36f; // 130 km/h

        [Test]
        public void FullBrake_FromMotorwaySpeed_NeverSpinsTheWheelBackwards()
        {
            SimulateQuarterCar(MotorwaySpeed, 0f, brakeForce: 2200f, DefaultSubSteps, 10f,
                out _, out _, out float[] angularVelocity, out _);

            foreach (float w in angularVelocity)
            {
                Assert.GreaterOrEqual(w, 0f);
            }
        }

        [Test]
        public void FullBrake_FromMotorwaySpeed_NeverPushesTheCarForward()
        {
            // A force flipping sign from step to step is the chatter the cap exists to prevent.
            SimulateQuarterCar(MotorwaySpeed, 0f, brakeForce: 2200f, DefaultSubSteps, 10f,
                out float[] speed, out float[] force, out _, out _);

            for (int k = 0; k < speed.Length; k++)
            {
                Assert.LessOrEqual(force[k], 1e-3f, $"step {k} at {speed[k]:F2} m/s");
            }
        }

        [Test]
        public void FullBrake_FromMotorwaySpeed_BringsTheCarToAStop()
        {
            SimulateQuarterCar(MotorwaySpeed, 0f, brakeForce: 2200f, DefaultSubSteps, 10f,
                out float[] speed, out _, out _, out _);

            Assert.Less(speed[speed.Length - 1], 0.05f);
            Assert.GreaterOrEqual(speed[speed.Length - 1], 0f);
        }

        [Test]
        public void FullBrake_StrongerThanTheGrip_LocksTheWheelAndBrakesWorseThanThePeak()
        {
            // 2200 N of brake against 1766 N of grip: the wheel has to lock, and a locked tire
            // slides on the tail of the curve - the reason ABS exists.
            SimulateQuarterCar(MotorwaySpeed, 0f, brakeForce: 2200f, DefaultSubSteps, 1f,
                out float[] speed, out float[] force, out float[] angularVelocity, out _);

            int last = speed.Length - 1;
            Assert.Greater(speed[last], 5f);
            Assert.AreEqual(0f, angularVelocity[last], 1e-6f);
            Assert.Less(-force[last], 0.9f * 0.95f * NominalLoad);
        }

        [Test]
        public void ModerateBrake_BelowTheGrip_KeepsTheWheelRolling()
        {
            SimulateQuarterCar(MotorwaySpeed, 0f, brakeForce: 1000f, DefaultSubSteps, 1.5f,
                out float[] speed, out _, out float[] angularVelocity, out float[] slip);

            int last = speed.Length - 1;
            Assert.Greater(angularVelocity[last], 0f);
            Assert.Less(Mathf.Abs(slip[last]), PeakSlip);
        }

        [Test]
        public void ModerateBrake_BelowTheGrip_DeceleratesWithTheBrakeForce()
        {
            // Once the wheel has settled the tire passes on what the brake asks for - less the
            // torque it takes to slow the wheel's own inertia along with the car, which the brake
            // pays for directly: I * dw/dt = -F * r - T_brake, with dw/dt = a / r.
            SimulateQuarterCar(MotorwaySpeed, 0f, brakeForce: 1000f, DefaultSubSteps, 1.5f,
                out float[] speed, out float[] force, out _, out _);

            int last = force.Length - 1;
            float deceleration = (speed[last] - speed[last - 1]) / Step;
            float brakeTorque = (1000f + RollingResistance * NominalLoad) * WheelRadius;
            float expected = -(brakeTorque + WheelInertia * deceleration / WheelRadius) / WheelRadius;

            Assert.AreEqual(expected, force[last], 0.02f * -expected);
        }

        [Test]
        public void Drive_FromStandstill_PullsAwayWithoutChatter()
        {
            // 350 N*m at the wheel is 1000 N at the road, well inside the grip.
            SimulateQuarterCar(0f, driveTorque: 350f, brakeForce: 0f, DefaultSubSteps, 5f,
                out float[] speed, out float[] force, out _, out float[] slip);

            for (int k = 1; k < force.Length; k++)
            {
                Assert.GreaterOrEqual(force[k], 0f, $"step {k}");
            }

            Assert.Greater(speed[speed.Length - 1], 20f);
            Assert.Less(slip[slip.Length - 1], PeakSlip);
        }

        [TestCase(10)]
        [TestCase(20)]
        [TestCase(40)]
        public void ModerateBrake_WithSubSteps_StopsLikeAFineReference(int subSteps)
        {
            // Sub-stepping is there for accuracy as well as stability: the stopping distance must
            // not depend noticeably on how many sub-steps are used.
            float Distance(int n)
            {
                SimulateQuarterCar(MotorwaySpeed, 0f, brakeForce: 1000f, n, 8f, out float[] speed, out _, out _, out _);
                float distance = 0f;
                foreach (float v in speed)
                {
                    distance += v * Step;
                }
                return distance;
            }

            float reference = Distance(400);
            Assert.AreEqual(reference, Distance(subSteps), 0.02f * reference);
        }

        // --- Loop test: engine, clutch, open differential and two driven wheels --------------

        private const float EngineIdleRpm = 1000f;
        private const float EngineMaxRpm = 6000f;
        private const float EngineFrictionAtMax = 30f;
        private const float EngineIdleGain = 0.05f;
        private const float EngineInertia = 0.15f;
        private const float FirstGearRatio = 12.8f;
        private const float DrivelineEfficiency = 0.85f;
        private const float MaxClutchTorque = 220f;

        /// <summary>
        /// The sub-stepped counterpart of DriveTrainMathTests.SimulateStandingStart: full
        /// throttle in first gear with the Pacejka tire, the rigid clutch and the open
        /// differential, both wheels identical.
        /// </summary>
        private static void SimulateStandingStart(
            int subSteps, out float minimumWheelAngularVelocity, out float minimumEngineRpm, out float finalSpeed)
        {
            const float brakeTorqueFromCurve = 80f;
            const float axleBody = 2f * QuarterBody;
            float h = Step / subSteps;
            float rollingTorque = RollingResistance * NominalLoad * WheelRadius;

            float engine = EngineIdleRpm * 2f * Mathf.PI / 60f;
            float wheel = 0f;
            float v = 0f;

            minimumWheelAngularVelocity = float.MaxValue;
            minimumEngineRpm = float.MaxValue;

            for (int k = 0; k < 200; k++)
            {
                float impulse = 0f;
                for (int i = 0; i < subSteps; i++)
                {
                    TireForces tire = TireMath.EvaluateTire(Tire, wheel, WheelRadius, WheelInertia,
                        v, 0f, NominalLoad, QuarterBody, QuarterBody, Step, h);

                    float wheelExternal = -tire.Longitudinal * WheelRadius - Mathf.Sign(wheel) * rollingTorque;
                    DrivelineLoad axle = DriveTrainMath.OpenDifferentialLoad(
                        wheel, wheel, WheelInertia, WheelInertia, wheelExternal, wheelExternal);

                    float rpm = DriveTrainMath.AngularVelocityToRpm(engine);
                    float friction = DriveTrainMath.EngineFrictionTorque(rpm, EngineMaxRpm, EngineFrictionAtMax);
                    float governor = DriveTrainMath.IdleGovernorTorque(rpm, EngineIdleRpm, EngineMaxRpm, EngineFrictionAtMax, EngineIdleGain);
                    float engineNet = DriveTrainMath.CombustionTorque(brakeTorqueFromCurve, friction, governor, throttle: 1f) - friction;

                    float clutch = Mathf.Clamp(
                        DriveTrainMath.LockedClutchTorque(engine, axle.AngularVelocity, engineNet, axle.ExternalTorque,
                            EngineInertia, axle.Inertia, FirstGearRatio, DrivelineEfficiency, h),
                        -MaxClutchTorque, MaxClutchTorque);

                    engine += (engineNet - clutch) / EngineInertia * h;
                    float wheelTorque = DriveTrainMath.OpenDifferentialWheelTorque(clutch * FirstGearRatio * DrivelineEfficiency);
                    wheel = TireMath.IntegrateWheel(wheel, wheelTorque, tire.Longitudinal, WheelRadius, WheelInertia, rollingTorque, h);

                    impulse += 2f * tire.Longitudinal * h;
                    minimumWheelAngularVelocity = Mathf.Min(minimumWheelAngularVelocity, wheel);
                    minimumEngineRpm = Mathf.Min(minimumEngineRpm, DriveTrainMath.AngularVelocityToRpm(engine));
                }

                v += impulse / axleBody;
            }

            finalSpeed = v;
        }

        [Test]
        public void StandingStartInFirstGear_NeverSpinsTheDrivenWheelsBackwards()
        {
            SimulateStandingStart(DefaultSubSteps, out float minimumWheel, out _, out _);
            Assert.GreaterOrEqual(minimumWheel, 0f);
        }

        [Test]
        public void StandingStartInFirstGear_KeepsTheEngineAboveStall()
        {
            SimulateStandingStart(DefaultSubSteps, out _, out float minimumRpm, out _);
            Assert.Greater(minimumRpm, 600f);
        }

        [Test]
        public void StandingStartInFirstGear_ActuallyPullsTheCarAway()
        {
            SimulateStandingStart(DefaultSubSteps, out _, out _, out float finalSpeed);
            Assert.Greater(finalSpeed, 3f);
        }
    }
}
