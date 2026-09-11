using CargoKing.Car;
using NUnit.Framework;
using UnityEngine;

namespace CargoKing.Tests
{
    public class EngineMathTests
    {
        private const float Idle = 1000f;
        private const float Max = 6000f;

        private static readonly TorqueCurve Punto = TorqueCurve.PuntoReference;

        // The curve the formula replaces: Car_v2's AnimationCurve, the Punto 1.2 Pop reference.
        private static readonly float[] ReferenceRpm = { 1000f, 1500f, 2000f, 2500f, 3000f, 3500f, 4000f, 4500f, 5000f, 5500f, 6000f };
        private static readonly float[] ReferenceTorque = { 31f, 61f, 85f, 97f, 102f, 101f, 99.5f, 96.5f, 93f, 88f, 72f };

        private static float Torque(float rpm) => EngineMath.Torque(rpm, Punto, Idle, Max);

        [Test]
        public void Torque_PassesThroughItsThreeAnchors()
        {
            Assert.AreEqual(Punto.torqueAtIdle, Torque(Idle), Punto.torqueAtIdle * 1e-4f);
            Assert.AreEqual(Punto.peakTorque, Torque(Punto.peakRpm), Punto.peakTorque * 1e-4f);
            Assert.AreEqual(Punto.torqueAtMaxRpm, Torque(Max), Punto.torqueAtMaxRpm * 1e-4f);
        }

        [Test]
        public void Torque_PeakIsTheMaximum()
        {
            for (float rpm = 0f; rpm <= Max; rpm += 5f)
            {
                Assert.LessOrEqual(Torque(rpm), Punto.peakTorque * (1f + 1e-5f), $"at {rpm} rpm");
            }
        }

        [Test]
        public void Torque_IsFlatAtThePeak_ForShapesAboveOne()
        {
            // Mean slope of the rise, N*m per rpm - a kink at the peak keeps roughly this slope
            // right up to it, a smooth top flattens out.
            float meanRiseSlope = (Punto.peakTorque - Punto.torqueAtIdle) / (Punto.peakRpm - Idle);

            float leftSlope = Torque(Punto.peakRpm) - Torque(Punto.peakRpm - 1f);
            float rightSlope = Torque(Punto.peakRpm) - Torque(Punto.peakRpm + 1f);

            Assert.Less(leftSlope, 0.05f * meanRiseSlope);
            Assert.Less(rightSlope, 0.05f * meanRiseSlope);
        }

        [Test]
        public void Torque_WithShapeOne_HasAKinkAtThePeak()
        {
            // Proves the flatness test above can fail: a straight rise keeps its slope to the top.
            TorqueCurve straight = Punto;
            straight.riseShape = 1f;
            float meanRiseSlope = (straight.peakTorque - straight.torqueAtIdle) / (straight.peakRpm - Idle);

            float leftSlope = EngineMath.Torque(straight.peakRpm, straight, Idle, Max)
                - EngineMath.Torque(straight.peakRpm - 1f, straight, Idle, Max);

            Assert.Greater(leftSlope, 0.5f * meanRiseSlope);
        }

        [Test]
        public void Torque_BelowIdle_IsNeverNegative()
        {
            for (float rpm = 0f; rpm <= Idle; rpm += 25f)
            {
                Assert.GreaterOrEqual(Torque(rpm), 0f, $"at {rpm} rpm");
            }

            // The rising half would reach about -43 N*m at 0 rpm; the clamp is what holds it at 0.
            Assert.AreEqual(0f, Torque(0f));
        }

        [Test]
        public void Torque_AboveMaxRpm_HoldsTheMaxRpmValue()
        {
            Assert.AreEqual(Punto.torqueAtMaxRpm, Torque(7000f), Punto.torqueAtMaxRpm * 1e-4f);
        }

        [Test]
        public void Torque_WithPeakRpmOutsideTheRange_StaysFinite()
        {
            foreach (float peakRpm in new[] { 0f, 500f, Idle, Max, 9000f })
            {
                TorqueCurve curve = Punto;
                curve.peakRpm = peakRpm;
                for (float rpm = 0f; rpm <= 7000f; rpm += 50f)
                {
                    float torque = EngineMath.Torque(rpm, curve, Idle, Max);
                    Assert.IsFalse(float.IsNaN(torque) || float.IsInfinity(torque), $"peak {peakRpm}, {rpm} rpm");
                    Assert.GreaterOrEqual(torque, 0f);
                }
            }
        }

        [Test]
        public void Torque_WithMaxRpmNotAboveIdle_StaysFinite()
        {
            float torque = EngineMath.Torque(1000f, Punto, 1000f, 1000f);
            Assert.IsFalse(float.IsNaN(torque) || float.IsInfinity(torque));
        }

        [Test]
        public void PuntoReference_MatchesTheReferenceCurveWithin2Nm()
        {
            for (int i = 0; i < ReferenceRpm.Length; i++)
            {
                Assert.AreEqual(ReferenceTorque[i], Torque(ReferenceRpm[i]), 2f, $"at {ReferenceRpm[i]} rpm");
            }
        }

        [Test]
        public void PowerWatts_IsTorqueTimesAngularVelocity()
        {
            // 100 N*m at 3000 rpm = 100 * 3000 * 2 pi / 60 = 31416 W.
            float watts = EngineMath.PowerWatts(100f, 3000f);
            Assert.AreEqual(31415.93f, watts, 31415.93f * 1e-5f);
        }
    }
}
