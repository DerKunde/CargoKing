using NUnit.Framework;
using UnityEngine;

namespace CargoKing.Traffic.Tests
{
    public class ShiftPolicyTests
    {
        private DrivingProfile profile;

        [SetUp]
        public void SetUp()
        {
            // The built-in values are the traffic profile: up at 4000, down at 1500, 1 s between
            // upshifts, clutch in below 10 km/h.
            profile = ScriptableObject.CreateInstance<DrivingProfile>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(profile);
        }

        private static ShiftSituation Situation(float rpm, int gear, float speedKmh, bool accelerating = true, float secondsSinceShift = 5f)
        {
            return new ShiftSituation
            {
                rpm = rpm,
                gear = gear,
                gearCount = 5,
                speed = speedKmh / 3.6f,
                accelerating = accelerating,
                secondsSinceShift = secondsSinceShift,
            };
        }

        [Test]
        public void Decide_ShiftsUpAtTheUpshiftRpmUnderThrottle()
        {
            ShiftDecision decision = ShiftPolicy.Decide(Situation(4000f, 2, 55f), profile);

            Assert.That(decision.shift, Is.EqualTo(ShiftCommand.Up));
            Assert.That(decision.holdClutch, Is.False);
        }

        [Test]
        public void Decide_WaitsTheMinimumIntervalBeforeShiftingUpAgain()
        {
            Assert.That(ShiftPolicy.Decide(Situation(4100f, 2, 55f, secondsSinceShift: 0.5f), profile).shift, Is.EqualTo(ShiftCommand.None));
        }

        [Test]
        public void Decide_NeverShiftsAboveTheTopGear()
        {
            Assert.That(ShiftPolicy.Decide(Situation(5000f, 5, 120f), profile).shift, Is.EqualTo(ShiftCommand.None));
        }

        [Test]
        public void Decide_DoesNotShiftUpWhileCoasting()
        {
            Assert.That(ShiftPolicy.Decide(Situation(4200f, 2, 55f, accelerating: false), profile).shift, Is.EqualTo(ShiftCommand.None));
        }

        [Test]
        public void Decide_ShiftsDownBelowTheDownshiftRpm()
        {
            ShiftDecision decision = ShiftPolicy.Decide(Situation(1400f, 3, 30f), profile);

            Assert.That(decision.shift, Is.EqualTo(ShiftCommand.Down));
            Assert.That(decision.holdClutch, Is.False);
        }

        [Test]
        public void Decide_NeverShiftsBelowFirst()
        {
            ShiftDecision decision = ShiftPolicy.Decide(Situation(1000f, 1, 20f), profile);

            Assert.That(decision.shift, Is.EqualTo(ShiftCommand.None));
            Assert.That(decision.holdClutch, Is.False);
        }

        [Test]
        public void Decide_HoldsTheClutchAndGearsDownWhenSlowAndNotAccelerating()
        {
            ShiftDecision decision = ShiftPolicy.Decide(Situation(900f, 3, 8f, accelerating: false), profile);

            Assert.That(decision.shift, Is.EqualTo(ShiftCommand.Down));
            Assert.That(decision.holdClutch, Is.True);
        }

        [Test]
        public void Decide_HoldsTheClutchInFirstWhenSlowAndNotAccelerating()
        {
            ShiftDecision decision = ShiftPolicy.Decide(Situation(700f, 1, 5f, accelerating: false), profile);

            Assert.That(decision.shift, Is.EqualTo(ShiftCommand.None));
            Assert.That(decision.holdClutch, Is.True);
        }

        [Test]
        public void Decide_GearsDownWithTheClutchHeldAtAStandstillInAHighGear()
        {
            ShiftDecision decision = ShiftPolicy.Decide(Situation(0f, 3, 0f), profile);

            Assert.That(decision.shift, Is.EqualTo(ShiftCommand.Down));
            Assert.That(decision.holdClutch, Is.True);
        }

        [Test]
        public void Decide_LeavesReverseToTheManoeuvres()
        {
            ShiftDecision decision = ShiftPolicy.Decide(Situation(2000f, 0, 5f, accelerating: false), profile);

            Assert.That(decision.shift, Is.EqualTo(ShiftCommand.None));
            Assert.That(decision.holdClutch, Is.False);
        }
    }
}
