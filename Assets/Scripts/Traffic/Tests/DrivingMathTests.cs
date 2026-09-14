using NUnit.Framework;
using UnityEngine;

namespace CargoKing.Traffic.Tests
{
    public class DrivingMathTests
    {
        [Test]
        public void CorneringSpeed_IsTheRootOfLateralAccelerationTimesRadius()
        {
            // 0.35 g on a 20 m radius.
            Assert.That(DrivingMath.CorneringSpeed(3.4335f, 20f), Is.EqualTo(8.2867f).Within(0.001f));
        }

        [Test]
        public void CorneringSpeed_IsUnlimitedOnAStraight()
        {
            Assert.That(float.IsPositiveInfinity(DrivingMath.CorneringSpeed(3.4335f, float.PositiveInfinity)), Is.True);
        }

        [Test]
        public void SpeedToReach_FromAStopIsTheRootOfTwiceDecelerationTimesDistance()
        {
            Assert.That(DrivingMath.SpeedToReach(0f, 3f, 6f), Is.EqualTo(6f).Within(0.001f));
        }

        [Test]
        public void SpeedToReach_RightThereIsTheSpeedThere()
        {
            Assert.That(DrivingMath.SpeedToReach(8f, 3f, 0f), Is.EqualTo(8f).Within(0.001f));
        }

        [Test]
        public void SpeedToReach_TreatsANegativeDistanceAsZero()
        {
            Assert.That(DrivingMath.SpeedToReach(8f, 3f, -5f), Is.EqualTo(8f).Within(0.001f));
        }

        [Test]
        public void LookAheadDistance_IsClampedAtBothEnds()
        {
            Assert.That(DrivingMath.LookAheadDistance(0f, 1.1f, 4f, 30f), Is.EqualTo(4f).Within(0.001f));
            Assert.That(DrivingMath.LookAheadDistance(10f, 1.1f, 4f, 30f), Is.EqualTo(11f).Within(0.001f));
            Assert.That(DrivingMath.LookAheadDistance(100f, 1.1f, 4f, 30f), Is.EqualTo(30f).Within(0.001f));
        }

        [Test]
        public void PurePursuitSteerAngle_IsZeroForATargetStraightAhead()
        {
            Assert.That(DrivingMath.PurePursuitSteerAngle(new Vector3(0f, 0f, 10f), 2.5f), Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void PurePursuitSteerAngle_IsNegativeForATargetOnTheLeft()
        {
            Assert.That(DrivingMath.PurePursuitSteerAngle(new Vector3(-2f, 0f, 10f), 2.5f), Is.LessThan(0f));
        }

        [Test]
        public void PurePursuitSteerAngle_SteersHarderForANearerTargetInTheSameDirection()
        {
            float near = DrivingMath.PurePursuitSteerAngle(new Vector3(1f, 0f, 5f), 2.5f);
            float far = DrivingMath.PurePursuitSteerAngle(new Vector3(2f, 0f, 10f), 2.5f);

            Assert.That(near, Is.GreaterThan(far));
        }

        [Test]
        public void PurePursuitSteerAngle_MatchesTheBicycleGeometry()
        {
            // alpha = atan2(2, 10), L = sqrt(104): atan(2 * 2.5 * sin(alpha) / L) = 5.4924 degrees.
            Assert.That(DrivingMath.PurePursuitSteerAngle(new Vector3(2f, 0f, 10f), 2.5f), Is.EqualTo(5.4924f).Within(0.01f));
        }

        [Test]
        public void PurePursuitSteerAngle_IgnoresHeight()
        {
            Assert.That(
                DrivingMath.PurePursuitSteerAngle(new Vector3(2f, 3f, 10f), 2.5f),
                Is.EqualTo(DrivingMath.PurePursuitSteerAngle(new Vector3(2f, 0f, 10f), 2.5f)).Within(0.0001f));
        }

        [Test]
        public void PedalsFor_GivesThrottleInProportionAboveTheDeadBand()
        {
            Pedals pedals = DrivingMath.PedalsFor(10f, 9f, 0.5f, 0.5f, 0.3f);

            Assert.That(pedals.throttle, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(pedals.brake, Is.EqualTo(0f));
        }

        [Test]
        public void PedalsFor_CoastsInsideTheDeadBand()
        {
            Pedals pedals = DrivingMath.PedalsFor(10f, 9.8f, 0.5f, 0.5f, 0.3f);

            Assert.That(pedals.throttle, Is.EqualTo(0f));
            Assert.That(pedals.brake, Is.EqualTo(0f));
        }

        [Test]
        public void PedalsFor_BrakesInProportionBelowTheDeadBand()
        {
            Pedals pedals = DrivingMath.PedalsFor(5f, 7f, 0.5f, 0.5f, 0.3f);

            Assert.That(pedals.throttle, Is.EqualTo(0f));
            Assert.That(pedals.brake, Is.EqualTo(0.6f).Within(0.001f));
        }

        [Test]
        public void PedalsFor_NeverAsksForMoreThanFullPedal()
        {
            Assert.That(DrivingMath.PedalsFor(30f, 0f, 0.5f, 0.5f, 0.3f).throttle, Is.EqualTo(1f));
            Assert.That(DrivingMath.PedalsFor(0f, 30f, 0.5f, 0.5f, 0.3f).brake, Is.EqualTo(1f));
        }
    }
}
