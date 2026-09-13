using NUnit.Framework;
using UnityEngine;

namespace CargoKing.Streets.Tests
{
    public class StreetLaneGeometryTests
    {
        /// <summary>A lane running 100 m along +X, sampled every 10 m, one metre off the origin in Z.</summary>
        private static void BuildStraightLane(out StreetNetworkSample[] samples, out StreetNetworkLane lane)
        {
            samples = new StreetNetworkSample[11];

            for (int index = 0; index < samples.Length; index++)
            {
                samples[index] = new StreetNetworkSample
                {
                    position = new Vector3(index * 10f, 0f, 1f),
                    direction = Vector3.right,
                    distance = index * 10f,
                    radius = float.PositiveInfinity,
                };
            }

            lane = new StreetNetworkLane { firstSample = 0, sampleCount = 11, length = 100f };
        }

        [Test]
        public void ClosestPoint_FindsTheDistanceAlongTheLane()
        {
            BuildStraightLane(out StreetNetworkSample[] samples, out StreetNetworkLane lane);

            float squared = StreetLaneGeometry.ClosestPoint(
                samples, lane, new Vector3(35f, 0f, 4f), out float distanceAlongLane);

            Assert.That(distanceAlongLane, Is.EqualTo(35f).Within(0.01f));
            Assert.That(Mathf.Sqrt(squared), Is.EqualTo(3f).Within(0.01f));
        }

        [Test]
        public void ClosestPoint_ClampsBeforeTheStart()
        {
            BuildStraightLane(out StreetNetworkSample[] samples, out StreetNetworkLane lane);

            StreetLaneGeometry.ClosestPoint(
                samples, lane, new Vector3(-50f, 0f, 1f), out float distanceAlongLane);

            Assert.That(distanceAlongLane, Is.EqualTo(0f).Within(0.01f));
        }

        [Test]
        public void ClosestPoint_ClampsBeyondTheEnd()
        {
            BuildStraightLane(out StreetNetworkSample[] samples, out StreetNetworkLane lane);

            StreetLaneGeometry.ClosestPoint(
                samples, lane, new Vector3(500f, 0f, 1f), out float distanceAlongLane);

            Assert.That(distanceAlongLane, Is.EqualTo(100f).Within(0.01f));
        }

        [Test]
        public void SampleAt_InterpolatesBetweenTwoSamples()
        {
            BuildStraightLane(out StreetNetworkSample[] samples, out StreetNetworkLane lane);

            StreetNetworkSample sample = StreetLaneGeometry.SampleAt(samples, lane, 25f);

            Assert.That(sample.position.x, Is.EqualTo(25f).Within(0.01f));
            Assert.That(sample.distance, Is.EqualTo(25f).Within(0.01f));
        }

        [Test]
        public void SampleAt_BeyondTheEndReturnsTheLastSample()
        {
            BuildStraightLane(out StreetNetworkSample[] samples, out StreetNetworkLane lane);

            StreetNetworkSample sample = StreetLaneGeometry.SampleAt(samples, lane, 250f);

            Assert.That(sample.position.x, Is.EqualTo(100f).Within(0.01f));
        }

        /// <summary>The straight lane, posted at 10 m/s up to 50 m and at 20 m/s from there on.</summary>
        private static void BuildTwoLimitLane(out StreetNetworkSample[] samples, out StreetNetworkLane lane)
        {
            BuildStraightLane(out samples, out lane);

            for (int index = 0; index < samples.Length; index++)
            {
                samples[index].speedLimit = index < 5 ? 10f : 20f;
            }
        }

        [Test]
        public void SampleAt_ReturnsTheLimitInForceRatherThanABlend()
        {
            // A limit changes at a sign. Blending it between two samples would have a car slow down
            // for a 30 zone metres before it reaches the sign.
            BuildTwoLimitLane(out StreetNetworkSample[] samples, out StreetNetworkLane lane);

            Assert.That(StreetLaneGeometry.SampleAt(samples, lane, 45f).speedLimit, Is.EqualTo(10f));
            Assert.That(StreetLaneGeometry.SampleAt(samples, lane, 50f).speedLimit, Is.EqualTo(20f));
            Assert.That(StreetLaneGeometry.SampleAt(samples, lane, 55f).speedLimit, Is.EqualTo(20f));
        }

        [Test]
        public void TravelTime_DrivesEachStretchAtItsOwnLimit()
        {
            BuildTwoLimitLane(out StreetNetworkSample[] samples, out StreetNetworkLane lane);

            // 50 m at 10 m/s plus 50 m at 20 m/s.
            Assert.That(StreetLaneGeometry.TravelTime(samples, lane, 0f), Is.EqualTo(7.5f).Within(0.001f));
        }

        [Test]
        public void TravelTime_FromMidwayChargesOnlyWhatIsLeft()
        {
            BuildTwoLimitLane(out StreetNetworkSample[] samples, out StreetNetworkLane lane);

            Assert.That(StreetLaneGeometry.TravelTime(samples, lane, 25f), Is.EqualTo(5f).Within(0.001f));
            Assert.That(StreetLaneGeometry.TravelTime(samples, lane, 75f), Is.EqualTo(1.25f).Within(0.001f));
        }
    }
}
