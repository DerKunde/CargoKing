using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace CargoKing.Streets.Tests
{
    public class StreetSignSlotsTests
    {
        /// <summary>A straight road along +Z from the origin. Seen along it, right is +X.</summary>
        private static Spline Straight(float length)
        {
            Spline spline = new Spline();
            spline.Add(new BezierKnot(float3.zero), TangentMode.AutoSmooth);
            spline.Add(new BezierKnot(new float3(0f, 0f, length)), TangentMode.AutoSmooth);
            return spline;
        }

        [Test]
        public void LastSlot_IsTheLastMultipleOfTheSpacingOnTheRoad()
        {
            Assert.That(StreetSignSlots.LastSlot(23f), Is.EqualTo(20f));
            Assert.That(StreetSignSlots.LastSlot(20f), Is.EqualTo(20f));
            Assert.That(StreetSignSlots.SlotCount(20f), Is.EqualTo(5));
            Assert.That(StreetSignSlots.SlotCount(23f), Is.EqualTo(5));
            Assert.That(StreetSignSlots.SlotCount(4f), Is.EqualTo(1));
        }

        [Test]
        public void Snap_RoundsToTheNearestSlotStillOnTheRoad()
        {
            Assert.That(StreetSignSlots.Snap(7.4f, 23f), Is.EqualTo(5f));
            Assert.That(StreetSignSlots.Snap(7.6f, 23f), Is.EqualTo(10f));
            Assert.That(StreetSignSlots.Snap(22.9f, 23f), Is.EqualTo(20f));
            Assert.That(StreetSignSlots.Snap(-3f, 23f), Is.EqualTo(0f));
        }

        [Test]
        public void At_PutsARightSignBesideTheRoadFacingOncomingTraffic()
        {
            StreetSignSlot slot = StreetSignSlots.At(Straight(40f), 16f, StreetSide.Right, 10f);

            // Half of 16 m plus the 1 m clearance.
            Assert.That(Vector3.Distance(slot.position, new Vector3(9f, 0f, 10f)), Is.LessThan(0.05f));
            Assert.That(Vector3.Distance(slot.centre, new Vector3(0f, 0f, 10f)), Is.LessThan(0.05f));

            // Traffic on the right drives +Z, so the face looks back down the road.
            Assert.That(Vector3.Dot(slot.rotation * Vector3.forward, Vector3.back), Is.GreaterThan(0.999f));
        }

        [Test]
        public void At_PutsALeftSignOnTheOtherSideFacingTheOtherWay()
        {
            StreetSignSlot slot = StreetSignSlots.At(Straight(40f), 16f, StreetSide.Left, 10f);

            Assert.That(Vector3.Distance(slot.position, new Vector3(-9f, 0f, 10f)), Is.LessThan(0.05f));
            Assert.That(Vector3.Dot(slot.rotation * Vector3.forward, Vector3.forward), Is.GreaterThan(0.999f));
        }

        [Test]
        public void At_PlacesADistancePastTheEndAtTheEnd()
        {
            StreetSignSlot slot = StreetSignSlots.At(Straight(40f), 16f, StreetSide.Right, 60f);

            Assert.That(slot.centre.z, Is.EqualTo(40f).Within(0.01f));
        }

        [Test]
        public void Locate_FindsTheSideAndTheDistance()
        {
            float away = StreetSignSlots.Locate(
                Straight(40f), new Vector3(5f, 0f, 12.3f), out StreetSide side, out float distance, out bool beyond);

            Assert.That(side, Is.EqualTo(StreetSide.Right));
            Assert.That(distance, Is.EqualTo(12.3f).Within(0.05f));
            Assert.That(beyond, Is.False);
            Assert.That(away, Is.EqualTo(5f).Within(0.05f));

            StreetSignSlots.Locate(Straight(40f), new Vector3(-5f, 0f, 4f), out side, out distance, out beyond);

            Assert.That(side, Is.EqualTo(StreetSide.Left));
            Assert.That(distance, Is.EqualTo(4f).Within(0.05f));
        }

        [Test]
        public void Locate_ReportsAPointPastEitherEnd()
        {
            StreetSignSlots.Locate(Straight(40f), new Vector3(3f, 0f, 47f), out _, out float distance, out bool beyond);

            Assert.That(beyond, Is.True);
            Assert.That(distance, Is.EqualTo(40f).Within(0.05f));

            StreetSignSlots.Locate(Straight(40f), new Vector3(3f, 0f, -3f), out _, out distance, out beyond);

            Assert.That(beyond, Is.True);
            Assert.That(distance, Is.EqualTo(0f).Within(0.05f));
        }

        [Test]
        public void Locate_ASignBesideTheEndIsNotPastIt()
        {
            // Where the segment itself parks a sign whose distance runs past a shortened road.
            StreetSignSlots.Locate(Straight(40f), new Vector3(9f, 0f, 40f), out _, out _, out bool beyond);

            Assert.That(beyond, Is.False);
        }
    }
}
