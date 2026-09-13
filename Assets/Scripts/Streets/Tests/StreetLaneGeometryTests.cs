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
    }
}
