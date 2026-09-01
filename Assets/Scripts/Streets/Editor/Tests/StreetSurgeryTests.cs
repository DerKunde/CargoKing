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
    }
}
