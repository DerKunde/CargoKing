using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

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

        private static float[] MergedPositions(StreetSegment survivor)
        {
            UnityEngine.Splines.Spline spline = StreetSurgery.SplineOf(survivor);
            float[] result = new float[spline.Count];

            for (int index = 0; index < spline.Count; index++)
            {
                result[index] = survivor.transform.TransformPoint(
                    new Vector3(spline[index].Position.x, spline[index].Position.y, spline[index].Position.z)).z;
            }

            return result;
        }

        [Test]
        public void Merge_JoinsEndToStart()
        {
            StreetSegment target = StreetTestFactory.Create("T", Vector3.zero, new Vector3(0f, 0f, 10f));
            StreetSegment dragged = StreetTestFactory.Create(
                "D", new Vector3(0f, 0f, 10f), new Vector3(0f, 0f, 30f));

            StreetSegment survivor = StreetSurgery.Merge(dragged, StreetEnd.Start, target, StreetEnd.End);

            Assert.AreSame(target, survivor);
            Assert.AreEqual(new[] { 0f, 10f, 30f }, MergedPositions(survivor));
            Assert.IsTrue(dragged == null, "The dragged segment has to be gone.");
        }

        [Test]
        public void Merge_JoinsEndToEnd()
        {
            StreetSegment target = StreetTestFactory.Create("T", Vector3.zero, new Vector3(0f, 0f, 10f));
            StreetSegment dragged = StreetTestFactory.Create(
                "D", new Vector3(0f, 0f, 30f), new Vector3(0f, 0f, 10f));

            StreetSegment survivor = StreetSurgery.Merge(dragged, StreetEnd.End, target, StreetEnd.End);

            Assert.AreEqual(new[] { 0f, 10f, 30f }, MergedPositions(survivor));
        }

        [Test]
        public void Merge_JoinsStartToStart()
        {
            StreetSegment target = StreetTestFactory.Create("T", new Vector3(0f, 0f, 10f), Vector3.zero);
            StreetSegment dragged = StreetTestFactory.Create(
                "D", new Vector3(0f, 0f, 10f), new Vector3(0f, 0f, 30f));

            StreetSegment survivor = StreetSurgery.Merge(dragged, StreetEnd.Start, target, StreetEnd.Start);

            Assert.AreEqual(new[] { 0f, 10f, 30f }, MergedPositions(survivor));
        }

        [Test]
        public void Merge_JoinsStartToEnd()
        {
            StreetSegment target = StreetTestFactory.Create("T", new Vector3(0f, 0f, 10f), new Vector3(0f, 0f, 30f));
            StreetSegment dragged = StreetTestFactory.Create("D", Vector3.zero, new Vector3(0f, 0f, 10f));

            StreetSegment survivor = StreetSurgery.Merge(dragged, StreetEnd.End, target, StreetEnd.Start);

            // Both sides had to be turned around for this one, so the joined road is described from
            // the far end backwards. Same road, read the other way - a merge promises no direction.
            Assert.AreEqual(new[] { 30f, 10f, 0f }, MergedPositions(survivor));
        }

        [Test]
        public void Merge_KeepsBothKnotsWhenTheTwoEndsDoNotMeet()
        {
            StreetSegment target = StreetTestFactory.Create("T", Vector3.zero, new Vector3(0f, 0f, 10f));
            StreetSegment dragged = StreetTestFactory.Create(
                "D", new Vector3(0f, 0f, 30f), new Vector3(0f, 0f, 40f));

            StreetSegment survivor = StreetSurgery.Merge(dragged, StreetEnd.Start, target, StreetEnd.End);

            // Nothing is welded here: the gap between 10 and 30 is road that has to stay. This is what
            // taking a junction back out looks like - the two halves stand where its sockets were.
            Assert.AreEqual(new[] { 0f, 10f, 30f, 40f }, MergedPositions(survivor));
        }

        [Test]
        public void Merge_CarriesTheOuterSocketOver()
        {
            StreetSegment target = StreetTestFactory.Create("T", Vector3.zero, new Vector3(0f, 0f, 10f));
            StreetSegment dragged = StreetTestFactory.Create(
                "D", new Vector3(0f, 0f, 10f), new Vector3(0f, 0f, 30f));

            GameObject socketObject = new GameObject("Socket");
            IntersectionSocket socket = socketObject.AddComponent<IntersectionSocket>();
            dragged.endConnection.socket = socket;

            StreetSegment survivor = StreetSurgery.Merge(dragged, StreetEnd.Start, target, StreetEnd.End);

            Assert.AreSame(socket, survivor.endConnection.socket);

            Object.DestroyImmediate(socketObject);
        }

        [Test]
        public void Merge_RefusesAndChangesNothingWhenTheWidthsDiffer()
        {
            LogAssert.ignoreFailingMessages = true;

            StreetSegment target = StreetTestFactory.Create("T", Vector3.zero, new Vector3(0f, 0f, 10f));
            StreetSegment dragged = StreetTestFactory.Create(
                "D", new Vector3(0f, 0f, 10f), new Vector3(0f, 0f, 30f));
            dragged.roadWidth = 7f;

            Assert.IsNull(StreetSurgery.Merge(dragged, StreetEnd.Start, target, StreetEnd.End));
            Assert.IsFalse(dragged == null, "A refused merge must not destroy anything.");
            Assert.AreEqual(2, StreetSurgery.SplineOf(target).Count);
        }

        [Test]
        public void Merge_ConvertsKnotsThroughARotatedTransform()
        {
            // Every other fixture in this file sits at the origin with an identity rotation, so
            // BezierKnot.Transform(matrix) is only ever exercised with an identity matrix there - a
            // swapped multiplication order in Merge would go unnoticed. IntersectionSocketDragging
            // builds new streets at a socket's position AND rotation, so a non-identity merge like
            // this one is not hypothetical.
            StreetSegment target = StreetTestFactory.Create("T", Vector3.zero, new Vector3(0f, 0f, 10f));
            StreetSegment dragged = StreetTestFactory.Create("D", Vector3.zero, new Vector3(0f, 0f, 10f));
            dragged.transform.SetPositionAndRotation(new Vector3(0f, 0f, 10f), Quaternion.Euler(0f, 90f, 0f));

            StreetSegment survivor = StreetSurgery.Merge(dragged, StreetEnd.Start, target, StreetEnd.End);

            UnityEngine.Splines.Spline spline = StreetSurgery.SplineOf(survivor);
            Assert.AreEqual(3, spline.Count);

            Vector3[] expected =
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(0f, 0f, 10f),
                new Vector3(10f, 0f, 10f),
            };

            for (int index = 0; index < expected.Length; index++)
            {
                Vector3 actual = survivor.transform.TransformPoint(new Vector3(
                    spline[index].Position.x, spline[index].Position.y, spline[index].Position.z));
                Assert.AreEqual(0f, Vector3.Distance(actual, expected[index]), 0.001f,
                    $"Knot {index} expected {expected[index]} but was {actual}.");
            }
        }
    }
}
