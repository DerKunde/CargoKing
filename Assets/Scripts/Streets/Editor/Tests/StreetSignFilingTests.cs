using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;
using UnityEngine.TestTools;

namespace CargoKing.Streets.Editor.Tests
{
    public class StreetSignFilingTests
    {
        [TearDown]
        public void TearDown()
        {
            StreetTestFactory.DestroyAll();
        }

        private static void AssertAt(StreetSpeedSign sign, Vector3 expected)
        {
            Assert.That(
                Vector3.Distance(sign.transform.position, expected), Is.LessThan(0.05f),
                $"'{sign.name}' stands at {sign.transform.position}, expected {expected}.");
        }

        [Test]
        public void Reverse_KeepsEverySignWhereItStands()
        {
            StreetSegment segment = StreetTestFactory.Create("Road", Vector3.zero, new Vector3(0f, 0f, 40f));
            StreetSpeedSign sign = StreetTestFactory.Sign(segment, StreetSide.Right, 10f);
            segment.Rebuild();

            StreetSurgery.Reverse(segment);

            // Same place in the world, now counted from the other end and on the other hand.
            Assert.That(sign.side, Is.EqualTo(StreetSide.Left));
            Assert.That(sign.distance, Is.EqualTo(30f).Within(0.05f));
            AssertAt(sign, new Vector3(9f, 0f, 10f));
        }

        [Test]
        public void Split_HandsSignsBeyondTheCutToTheSecondHalf()
        {
            StreetSegment segment = StreetTestFactory.Create(
                "Road", Vector3.zero, new Vector3(0f, 0f, 20f), new Vector3(0f, 0f, 40f));
            StreetSpeedSign near = StreetTestFactory.Sign(segment, StreetSide.Right, 10f);
            StreetSpeedSign far = StreetTestFactory.Sign(segment, StreetSide.Right, 30f);
            segment.Rebuild();

            StreetSegment second = StreetSurgery.Split(segment, 1);

            Assert.That(near.Segment, Is.SameAs(segment));
            Assert.That(near.distance, Is.EqualTo(10f).Within(0.05f));

            Assert.That(far.Segment, Is.SameAs(second));
            Assert.That(far.distance, Is.EqualTo(10f).Within(0.05f));
            AssertAt(far, new Vector3(9f, 0f, 30f));
        }

        [Test]
        public void Merge_CarriesSignsOntoTheJoinedRoad()
        {
            // The case where both sides are turned around: the joined road reads from z = 30 to 0.
            StreetSegment target = StreetTestFactory.Create("T", new Vector3(0f, 0f, 10f), new Vector3(0f, 0f, 30f));
            StreetSegment dragged = StreetTestFactory.Create("D", Vector3.zero, new Vector3(0f, 0f, 10f));
            StreetSpeedSign onTarget = StreetTestFactory.Sign(target, StreetSide.Right, 10f);
            StreetSpeedSign onDragged = StreetTestFactory.Sign(dragged, StreetSide.Right, 5f);
            target.Rebuild();
            dragged.Rebuild();

            StreetSegment survivor = StreetSurgery.Merge(dragged, StreetEnd.End, target, StreetEnd.Start);

            Assert.That(onTarget.Segment, Is.SameAs(survivor));
            Assert.That(onTarget.side, Is.EqualTo(StreetSide.Left));
            Assert.That(onTarget.distance, Is.EqualTo(10f).Within(0.05f));
            AssertAt(onTarget, new Vector3(9f, 0f, 20f));

            Assert.That(onDragged.Segment, Is.SameAs(survivor));
            Assert.That(onDragged.side, Is.EqualTo(StreetSide.Left));
            Assert.That(onDragged.distance, Is.EqualTo(25f).Within(0.05f));
            AssertAt(onDragged, new Vector3(9f, 0f, 5f));
        }

        [Test]
        public void Refile_MovesASignOffTheEndOfAShortenedRoadToTheLastSlotAndSaysSo()
        {
            StreetSegment segment = StreetTestFactory.Create("Road", Vector3.zero, new Vector3(0f, 0f, 40f));
            StreetSpeedSign sign = StreetTestFactory.Sign(segment, StreetSide.Right, 37f);
            segment.Rebuild();

            StreetSignFiling.Snapshot snapshot = StreetSignFiling.Capture(segment);
            StreetSurgery.SplineOf(segment).SetKnot(1, new BezierKnot(new float3(0f, 0f, 31f)));

            LogAssert.Expect(LogType.Log, new Regex("moved to its slot at 30 m"));
            StreetSignFiling.Refile(snapshot, segment);

            Assert.That(sign.Segment, Is.SameAs(segment));
            Assert.That(sign.side, Is.EqualTo(StreetSide.Right));
            Assert.That(sign.distance, Is.EqualTo(30f));
        }

        [Test]
        public void Insert_MovesASignWhereTheJunctionNowStands()
        {
            GameObject junction = BuildCrossing();

            try
            {
                StreetSegment segment = StreetTestFactory.Create(
                    "Road", Vector3.zero, new Vector3(0f, 0f, 50f), new Vector3(0f, 0f, 100f));
                StreetSpeedSign swallowed = StreetTestFactory.Sign(segment, StreetSide.Right, 47f);
                StreetSpeedSign beyond = StreetTestFactory.Sign(segment, StreetSide.Right, 70f);
                segment.Rebuild();

                LogAssert.Expect(LogType.Log, new Regex("moved to its slot at 40 m"));
                Assert.That(JunctionInsertion.Insert(segment, 1, junction), Is.Not.Null);

                // The first half now ends on the entry socket 9.5 m before the knot, at z = 40.5.
                Assert.That(swallowed.Segment, Is.SameAs(segment));
                Assert.That(swallowed.side, Is.EqualTo(StreetSide.Right));
                Assert.That(swallowed.distance, Is.EqualTo(40f));

                // The second half starts on the exit socket at z = 59.5; the sign keeps its place.
                Assert.That(beyond.Segment, Is.Not.Null);
                Assert.That(beyond.Segment, Is.Not.SameAs(segment));
                Assert.That(beyond.distance, Is.EqualTo(10.5f).Within(0.1f));
                AssertAt(beyond, new Vector3(9f, 0f, 70f));
            }
            finally
            {
                Object.DestroyImmediate(junction);
            }
        }

        /// <summary>A crossing with four arms 9.5 m out, like the project's prefabs; see JunctionPlacementTests.</summary>
        private static GameObject BuildCrossing()
        {
            GameObject junction = new GameObject("Crossing Template");
            junction.AddComponent<Intersection>();

            AddSocket(junction, new Vector3(0f, 0f, 9.5f), Vector3.forward);
            AddSocket(junction, new Vector3(9.5f, 0f, 0f), Vector3.right);
            AddSocket(junction, new Vector3(0f, 0f, -9.5f), Vector3.back);
            AddSocket(junction, new Vector3(-9.5f, 0f, 0f), Vector3.left);

            return junction;
        }

        private static void AddSocket(GameObject junction, Vector3 localPosition, Vector3 outward)
        {
            GameObject socket = new GameObject("Socket");
            socket.transform.SetParent(junction.transform, false);
            socket.transform.localPosition = localPosition;
            socket.transform.localRotation = Quaternion.LookRotation(outward, Vector3.up);
            socket.AddComponent<IntersectionSocket>().roadWidth = 16f;
        }
    }
}
