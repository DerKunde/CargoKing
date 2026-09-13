using NUnit.Framework;
using UnityEngine;

namespace CargoKing.Streets.Tests
{
    public class StreetProfileTests
    {
        [Test]
        public void SpeedLimit_ConvertsKilometresPerHourToMetresPerSecond()
        {
            StreetProfile profile = ScriptableObject.CreateInstance<StreetProfile>();
            profile.speedLimitKmh = 50f;

            Assert.That(profile.SpeedLimit, Is.EqualTo(13.889f).Within(0.01f));

            Object.DestroyImmediate(profile);
        }

        [Test]
        public void SpeedLimit_NeverReturnsZero()
        {
            // A lane with a zero limit would divide by zero in the route cost and stall every
            // vehicle on it, so an unset profile has to fall back to something driveable.
            StreetProfile profile = ScriptableObject.CreateInstance<StreetProfile>();
            profile.speedLimitKmh = 0f;

            Assert.That(profile.SpeedLimit, Is.GreaterThan(0f));

            Object.DestroyImmediate(profile);
        }
    }
}
