using CargoKing.Car;
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
            float newRpm = DriveTrainMath.IntegrateEngineRpm(currentRpm: 1000f, combustionTorque: 100f, clutchReactionTorque: 0f, engineInertia: 0.15f, rpmChangeSpeed: 3000f, deltaTime: 0.02f);
            Assert.Greater(newRpm, 1000f);
        }

        [Test]
        public void IntegrateEngineRpm_ReactionTorqueExceedsCombustion_DecreasesRpm()
        {
            float newRpm = DriveTrainMath.IntegrateEngineRpm(currentRpm: 1000f, combustionTorque: 50f, clutchReactionTorque: 220f, engineInertia: 0.15f, rpmChangeSpeed: 3000f, deltaTime: 0.02f);
            Assert.Less(newRpm, 1000f);
        }

        [Test]
        public void IntegrateEngineRpm_RateLimitedByRpmChangeSpeed()
        {
            // Huge net torque, tiny rpmChangeSpeed - the move-towards cap must win.
            float newRpm = DriveTrainMath.IntegrateEngineRpm(currentRpm: 1000f, combustionTorque: 10000f, clutchReactionTorque: 0f, engineInertia: 0.15f, rpmChangeSpeed: 10f, deltaTime: 1f);
            Assert.AreEqual(1010f, newRpm, 0.01f);
        }
    }
}
