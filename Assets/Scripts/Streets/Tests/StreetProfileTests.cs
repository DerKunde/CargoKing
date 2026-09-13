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

        [Test]
        public void MarkChanged_AdvancesTheVersion()
        {
            // Segments compare this number to notice that their profile was edited. Without it a
            // changed tile or material would only show after the next unrelated rebuild.
            StreetProfile profile = ScriptableObject.CreateInstance<StreetProfile>();
            int before = profile.Version;

            profile.MarkChanged();

            Assert.That(profile.Version, Is.Not.EqualTo(before));

            Object.DestroyImmediate(profile);
        }

        [Test]
        public void Defaults_DescribeTheProjectsStandardStreet()
        {
            StreetProfile profile = ScriptableObject.CreateInstance<StreetProfile>();

            Assert.That(profile.roadWidth, Is.EqualTo(16f));
            Assert.That(profile.forwardAxis, Is.EqualTo(StreetMeshAxis.X));
            Assert.That(profile.tileLength, Is.EqualTo(0f));

            Object.DestroyImmediate(profile);
        }
    }
}
