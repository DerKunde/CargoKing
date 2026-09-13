using NUnit.Framework;
using UnityEngine;

namespace CargoKing.Streets.Editor.Tests
{
    public class StreetSignPlacementTests
    {
        [TearDown]
        public void TearDown()
        {
            StreetTestFactory.DestroyAll();
        }

        private static StreetSegment Road()
        {
            StreetSegment segment = StreetTestFactory.Create("Road", Vector3.zero, new Vector3(0f, 0f, 40f));
            segment.Rebuild();
            return segment;
        }

        [Test]
        public void Create_PutsTheSignOnItsSlotUnderTheStreet()
        {
            StreetSegment segment = Road();

            StreetSpeedSign sign = StreetSignPlacement.Create(segment, StreetSide.Left, 10f, 30f);

            Assert.That(sign.Segment, Is.SameAs(segment));
            Assert.That(sign.limitKmh, Is.EqualTo(30f));
            Assert.That(Vector3.Distance(sign.transform.position, new Vector3(-9f, 0f, 10f)), Is.LessThan(0.01f));
        }

        [Test]
        public void Create_SnapsTheDistanceToTheGrid()
        {
            StreetSpeedSign sign = StreetSignPlacement.Create(Road(), StreetSide.Right, 12.4f, 30f);

            Assert.That(sign.distance, Is.EqualTo(10f));
        }

        [Test]
        public void IsFree_ReportsAnOccupiedSlot()
        {
            StreetSegment segment = Road();
            StreetSignPlacement.Create(segment, StreetSide.Right, 10f, 30f);

            Assert.That(StreetSignPlacement.IsFree(segment, StreetSide.Right, 10f), Is.False);
            Assert.That(StreetSignPlacement.IsFree(segment, StreetSide.Left, 10f), Is.True);
            Assert.That(StreetSignPlacement.IsFree(segment, StreetSide.Right, 15f), Is.True);
        }

        [Test]
        public void NearestSlot_SnapsAWorldPointToTheNearestSlot()
        {
            StreetSegment segment = Road();

            StreetSignPlacement.NearestSlot(segment, new Vector3(7f, 0f, 13f), out StreetSide side, out float distance);
            Assert.That(side, Is.EqualTo(StreetSide.Right));
            Assert.That(distance, Is.EqualTo(15f));

            StreetSignPlacement.NearestSlot(segment, new Vector3(-7f, 0f, 11f), out side, out distance);
            Assert.That(side, Is.EqualTo(StreetSide.Left));
            Assert.That(distance, Is.EqualTo(10f));
        }

        [Test]
        public void MoveTo_CrossesToTheOtherSide()
        {
            StreetSegment segment = Road();
            StreetSpeedSign sign = StreetSignPlacement.Create(segment, StreetSide.Right, 10f, 30f);

            Assert.That(StreetSignPlacement.MoveTo(sign, StreetSide.Left, 20f), Is.True);

            Assert.That(sign.side, Is.EqualTo(StreetSide.Left));
            Assert.That(sign.distance, Is.EqualTo(20f));
            Assert.That(Vector3.Distance(sign.transform.position, new Vector3(-9f, 0f, 20f)), Is.LessThan(0.01f));
        }

        [Test]
        public void MoveTo_RefusesAnOccupiedSlot()
        {
            StreetSegment segment = Road();
            StreetSpeedSign first = StreetSignPlacement.Create(segment, StreetSide.Right, 10f, 30f);
            StreetSignPlacement.Create(segment, StreetSide.Right, 20f, 70f);

            Assert.That(StreetSignPlacement.MoveTo(first, StreetSide.Right, 20f), Is.False);
            Assert.That(first.distance, Is.EqualTo(10f));
        }
    }
}
