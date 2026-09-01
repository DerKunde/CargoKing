using NUnit.Framework;
using UnityEngine;

namespace CargoKing.Streets.Editor.Tests
{
    public class StreetSurgeryTests
    {
        [TearDown]
        public void TearDown()
        {
            StreetTestFactory.DestroyAll();
        }

        [Test]
        public void CanMerge_AcceptsTwoMatchingSegments()
        {
            StreetSegment a = StreetTestFactory.Create("A", Vector3.zero, new Vector3(0f, 0f, 10f));
            StreetSegment b = StreetTestFactory.Create("B", Vector3.zero, new Vector3(0f, 0f, 10f));

            Assert.IsTrue(StreetSurgery.CanMerge(b, a, out string problem), problem);
            Assert.IsNull(problem);
        }

        [Test]
        public void CanMerge_RefusesDifferentRoadWidths()
        {
            StreetSegment a = StreetTestFactory.Create("A", Vector3.zero, new Vector3(0f, 0f, 10f));
            StreetSegment b = StreetTestFactory.Create("B", Vector3.zero, new Vector3(0f, 0f, 10f));
            b.roadWidth = 7f;

            Assert.IsFalse(StreetSurgery.CanMerge(b, a, out string problem));
            StringAssert.Contains("wide", problem);
        }

        [Test]
        public void CanMerge_RefusesTheSameSegmentTwice()
        {
            StreetSegment a = StreetTestFactory.Create("A", Vector3.zero, new Vector3(0f, 0f, 10f));

            Assert.IsFalse(StreetSurgery.CanMerge(a, a, out string problem));
            Assert.IsNotNull(problem);
        }

        [Test]
        public void CanMerge_RefusesScaledSegments()
        {
            StreetSegment a = StreetTestFactory.Create("A", Vector3.zero, new Vector3(0f, 0f, 10f));
            StreetSegment b = StreetTestFactory.Create("B", Vector3.zero, new Vector3(0f, 0f, 10f));
            b.transform.localScale = new Vector3(2f, 1f, 1f);

            Assert.IsFalse(StreetSurgery.CanMerge(b, a, out string problem));
            StringAssert.Contains("scale", problem);
        }

        [Test]
        public void Reverse_TurnsTheKnotOrderAround()
        {
            StreetSegment segment = StreetTestFactory.Create(
                "A",
                Vector3.zero,
                new Vector3(0f, 0f, 10f),
                new Vector3(0f, 0f, 30f));

            StreetSurgery.Reverse(segment);

            UnityEngine.Splines.Spline spline = StreetSurgery.SplineOf(segment);
            Assert.AreEqual(3, spline.Count);
            Assert.AreEqual(30f, spline[0].Position.z, 0.001f);
            Assert.AreEqual(10f, spline[1].Position.z, 0.001f);
            Assert.AreEqual(0f, spline[2].Position.z, 0.001f);
        }

        [Test]
        public void Reverse_TurnsTheDirectionOfTravelAround()
        {
            StreetSegment segment = StreetTestFactory.Create(
                "A",
                Vector3.zero,
                new Vector3(0f, 0f, 10f),
                new Vector3(0f, 0f, 30f));

            StreetSurgery.Reverse(segment);

            // The spline used to run towards +Z, so after turning it around it has to run towards -Z.
            Assert.Less(segment.EndDirection(StreetEnd.Start).z, 0f);
        }

        [Test]
        public void Reverse_SwapsTheTwoConnectors()
        {
            StreetSegment segment = StreetTestFactory.Create(
                "A",
                Vector3.zero,
                new Vector3(0f, 0f, 10f));

            GameObject socketObject = new GameObject("Socket");
            IntersectionSocket socket = socketObject.AddComponent<IntersectionSocket>();
            segment.startConnection.socket = socket;

            StreetSurgery.Reverse(segment);

            Assert.AreSame(socket, segment.endConnection.socket);
            Assert.IsNull(segment.startConnection.socket);

            Object.DestroyImmediate(socketObject);
        }
    }
}
