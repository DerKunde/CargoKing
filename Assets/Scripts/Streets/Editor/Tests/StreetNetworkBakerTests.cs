using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace CargoKing.Streets.Editor.Tests
{
    public class StreetNetworkBakerTests
    {
        [TearDown]
        public void TearDown()
        {
            StreetTestFactory.DestroyAll();
        }

        [Test]
        public void Collect_ProducesTwoLanesPerSegment()
        {
            StreetSegment segment = StreetTestFactory.Create("Road", Vector3.zero, new Vector3(50f, 0f, 0f));
            segment.Rebuild();

            StreetNetworkBakeResult result = StreetNetworkBaker.Collect(
                new[] { segment }, System.Array.Empty<Intersection>());

            Assert.That(result.lanes.Count, Is.EqualTo(2));
        }

        [Test]
        public void Collect_PutsSamplesIntoWorldSpace()
        {
            StreetSegment segment = StreetTestFactory.Create("Road", Vector3.zero, new Vector3(50f, 0f, 0f));
            segment.transform.position = new Vector3(1000f, 0f, 0f);
            segment.Rebuild();

            StreetNetworkBakeResult result = StreetNetworkBaker.Collect(
                new[] { segment }, System.Array.Empty<Intersection>());

            StreetNetworkLane lane = result.lanes[0];
            Vector3 first = result.samples[lane.firstSample].position;

            Assert.That(first.x, Is.GreaterThan(900f));
        }

        [Test]
        public void Collect_CarriesTheSpeedLimitOntoEveryLaneOfTheSegment()
        {
            StreetSegment segment = StreetTestFactory.Create("Road", Vector3.zero, new Vector3(50f, 0f, 0f));
            segment.speedLimitOverrideKmh = 72f;
            segment.Rebuild();

            StreetNetworkBakeResult result = StreetNetworkBaker.Collect(
                new[] { segment }, System.Array.Empty<Intersection>());

            Assert.That(result.lanes[0].speedLimit, Is.EqualTo(20f).Within(0.1f));
            Assert.That(result.lanes[1].speedLimit, Is.EqualTo(20f).Within(0.1f));
        }

        [Test]
        public void Collect_LinksTheForwardLaneOfOneSegmentToTheNext()
        {
            StreetSegment first = StreetTestFactory.Create("First", Vector3.zero, new Vector3(50f, 0f, 0f));
            StreetSegment second = StreetTestFactory.Create("Second", new Vector3(50f, 0f, 0f), new Vector3(100f, 0f, 0f));

            // The end of the first docks to the start of the second, the way StreetSnapping writes it.
            first.endConnection.segment = second;
            first.endConnection.segmentEnd = StreetEnd.Start;
            first.endConnection.driven = false;

            first.Rebuild();
            second.Rebuild();

            StreetNetworkBakeResult result = StreetNetworkBaker.Collect(
                new[] { first, second }, System.Array.Empty<Intersection>());

            List<int> exits = ExitsOf(result, 0);

            // Lane 0 is the first segment's forward lane, lane 2 the second segment's forward lane.
            Assert.That(exits, Is.EqualTo(new[] { 2 }));
        }

        [Test]
        public void Collect_LeavesAnOpenEndWithNoExits()
        {
            StreetSegment segment = StreetTestFactory.Create("Road", Vector3.zero, new Vector3(50f, 0f, 0f));
            segment.Rebuild();

            StreetNetworkBakeResult result = StreetNetworkBaker.Collect(
                new[] { segment }, System.Array.Empty<Intersection>());

            Assert.That(ExitsOf(result, 0), Is.Empty);
        }

        [Test]
        public void Collect_NumbersDistancesFromTheStartOfEachLane()
        {
            StreetSegment segment = StreetTestFactory.Create("Road", Vector3.zero, new Vector3(50f, 0f, 0f));
            segment.Rebuild();

            StreetNetworkBakeResult result = StreetNetworkBaker.Collect(
                new[] { segment }, System.Array.Empty<Intersection>());

            StreetNetworkLane lane = result.lanes[0];

            Assert.That(result.samples[lane.firstSample].distance, Is.EqualTo(0f).Within(0.001f));
            Assert.That(
                result.samples[lane.firstSample + lane.sampleCount - 1].distance,
                Is.EqualTo(lane.length).Within(0.01f));
        }

        [Test]
        public void Collect_MarksALongStraightRoadLaneAsSpawnable()
        {
            StreetSegment segment = StreetTestFactory.Create("Road", Vector3.zero, new Vector3(200f, 0f, 0f));
            segment.Rebuild();

            StreetNetworkBakeResult result = StreetNetworkBaker.Collect(
                new[] { segment }, System.Array.Empty<Intersection>());

            Assert.That(result.lanes[0].spawnable, Is.True);
        }

        [Test]
        public void Collect_LeavesAShortLaneUnspawnable()
        {
            StreetSegment segment = StreetTestFactory.Create("Road", Vector3.zero, new Vector3(10f, 0f, 0f));
            segment.Rebuild();

            StreetNetworkBakeResult result = StreetNetworkBaker.Collect(
                new[] { segment }, System.Array.Empty<Intersection>());

            Assert.That(result.lanes[0].spawnable, Is.False);
        }

        private static List<int> ExitsOf(StreetNetworkBakeResult result, int lane)
        {
            List<int> found = new List<int>();
            StreetNetworkLane entry = result.lanes[lane];

            for (int index = 0; index < entry.exitCount; index++)
            {
                found.Add(result.exits[entry.firstExit + index]);
            }

            return found;
        }
    }
}
