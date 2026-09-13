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

        [Test]
        public void Collect_WritesTheLimitOntoEverySampleAndATravelTimeOntoTheLane()
        {
            StreetSegment segment = StreetTestFactory.Create("Road", Vector3.zero, new Vector3(50f, 0f, 0f));
            segment.speedLimitOverrideKmh = 72f;
            segment.Rebuild();

            StreetNetworkBakeResult result = StreetNetworkBaker.Collect(
                new[] { segment }, System.Array.Empty<Intersection>());

            StreetNetworkLane lane = result.lanes[0];

            for (int index = 0; index < lane.sampleCount; index++)
            {
                Assert.That(result.samples[lane.firstSample + index].speedLimit, Is.EqualTo(20f).Within(0.1f));
            }

            Assert.That(lane.travelTime, Is.EqualTo(lane.length / 20f).Within(0.01f));
        }

        private const float Thirty = 30f / 3.6f;
        private const float Fifty = 50f / 3.6f;

        private static float LimitAt(StreetNetworkBakeResult result, int lane, float distance)
        {
            return StreetLaneGeometry.SampleAt(result.samples.ToArray(), result.lanes[lane], distance).speedLimit;
        }

        [Test]
        public void Collect_ChangesTheLimitWhereASignStands()
        {
            StreetSegment segment = StreetTestFactory.Create("Road", Vector3.zero, new Vector3(100f, 0f, 0f));
            StreetTestFactory.Sign(segment, StreetSide.Right, 50f, 30f);
            segment.Rebuild();

            StreetNetworkBakeResult result = StreetNetworkBaker.Collect(
                new[] { segment }, System.Array.Empty<Intersection>());

            Assert.That(LimitAt(result, 0, 25f), Is.EqualTo(Fifty).Within(0.01f));
            Assert.That(LimitAt(result, 0, 75f), Is.EqualTo(Thirty).Within(0.01f));
        }

        [Test]
        public void Collect_ASignOnTheLeftGovernsTheBackwardLane()
        {
            StreetSegment segment = StreetTestFactory.Create("Road", Vector3.zero, new Vector3(100f, 0f, 0f));
            StreetTestFactory.Sign(segment, StreetSide.Left, 50f, 30f);
            segment.Rebuild();

            StreetNetworkBakeResult result = StreetNetworkBaker.Collect(
                new[] { segment }, System.Array.Empty<Intersection>());

            Assert.That(LimitAt(result, 0, 75f), Is.EqualTo(Fifty).Within(0.01f));

            // The backward lane runs from x = 100 to 0, so it passes the sign halfway and is 30 after it.
            Assert.That(LimitAt(result, 1, 25f), Is.EqualTo(Fifty).Within(0.01f));
            Assert.That(LimitAt(result, 1, 75f), Is.EqualTo(Thirty).Within(0.01f));
        }

        [Test]
        public void Collect_CarriesAZoneIntoADockedStreet()
        {
            StreetSegment first = StreetTestFactory.Create("First", Vector3.zero, new Vector3(50f, 0f, 0f));
            StreetSegment second = StreetTestFactory.Create("Second", new Vector3(50f, 0f, 0f), new Vector3(100f, 0f, 0f));

            first.endConnection.segment = second;
            first.endConnection.segmentEnd = StreetEnd.Start;
            first.endConnection.driven = false;

            StreetTestFactory.Sign(first, StreetSide.Right, 40f, 30f);
            first.Rebuild();
            second.Rebuild();

            StreetNetworkBakeResult result = StreetNetworkBaker.Collect(
                new[] { first, second }, System.Array.Empty<Intersection>());

            // Lane 2 is the second segment's forward lane.
            Assert.That(LimitAt(result, 2, 25f), Is.EqualTo(Thirty).Within(0.01f));
        }

        private const float Eighty = 80f / 3.6f;

        /// <summary>
        /// A 70 road with a 30 sign on its last stretch runs from the west into a straight two-arm
        /// junction; an 80 road leaves it to the east. Placed away from the origin, so a mix-up of local
        /// and world space cannot hide.
        ///
        /// Lanes 0 and 1 are the west road's forward and backward lane, 2 and 3 the east road's; the
        /// junction's paths come after them.
        /// </summary>
        private static StreetNetworkBakeResult BakeJunctionBetweenTwoRoads()
        {
            Vector3 offset = new Vector3(200f, 0f, 100f);

            Intersection junction = StreetTestFactory.Junction("Junction", offset);
            IntersectionSocket west = StreetTestFactory.Socket(junction, new Vector3(-8f, 0f, 0f), Vector3.left);
            IntersectionSocket east = StreetTestFactory.Socket(junction, new Vector3(8f, 0f, 0f), Vector3.right);
            junction.Rebuild();

            StreetSegment westRoad = StreetTestFactory.Create("West", new Vector3(-58f, 0f, 0f), new Vector3(-8f, 0f, 0f));
            westRoad.transform.position = offset;
            westRoad.speedLimitOverrideKmh = 70f;
            westRoad.endConnection.socket = west;
            westRoad.endConnection.driven = false;
            StreetTestFactory.Sign(westRoad, StreetSide.Right, 30f, 30f);

            StreetSegment eastRoad = StreetTestFactory.Create("East", new Vector3(8f, 0f, 0f), new Vector3(58f, 0f, 0f));
            eastRoad.transform.position = offset;
            eastRoad.speedLimitOverrideKmh = 80f;
            eastRoad.startConnection.socket = east;
            eastRoad.startConnection.driven = false;

            westRoad.Rebuild();
            eastRoad.Rebuild();

            return StreetNetworkBaker.Collect(new[] { westRoad, eastRoad }, new[] { junction });
        }

        /// <summary>The intersection path whose exit is the given lane.</summary>
        private static int PathInto(StreetNetworkBakeResult result, int lane)
        {
            for (int index = 0; index < result.lanes.Count; index++)
            {
                if (result.lanes[index].intersection >= 0 && ExitsOf(result, index).Contains(lane))
                {
                    return index;
                }
            }

            Assert.Fail($"No intersection path leads into lane {lane}.");
            return -1;
        }

        [Test]
        public void Collect_AJunctionPathTakesTheLimitInForceOnTheRoadEnteringIt()
        {
            // Not the 50 km/h a path nobody enters falls back to: crossing out of the 30 zone stays at
            // 30, crossing off the 80 road stays at 80. A driver reads this to decide how fast to take
            // the junction.
            StreetNetworkBakeResult result = BakeJunctionBetweenTwoRoads();

            Assert.That(LimitAt(result, PathInto(result, 2), 1f), Is.EqualTo(Thirty).Within(0.01f));
            Assert.That(LimitAt(result, PathInto(result, 1), 1f), Is.EqualTo(Eighty).Within(0.01f));
        }

        [Test]
        public void Collect_AZoneEndsAtAJunction()
        {
            StreetNetworkBakeResult result = BakeJunctionBetweenTwoRoads();

            // Lane 2 leaves the junction eastwards: back to its own 80, not the 30 it was entered with.
            Assert.That(LimitAt(result, 2, 25f), Is.EqualTo(Eighty).Within(0.01f));
        }
    }
}
