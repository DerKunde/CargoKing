using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace CargoKing.Streets.Editor.Tests
{
    public class StreetSpeedSignTests
    {
        [TearDown]
        public void TearDown()
        {
            StreetTestFactory.DestroyAll();
        }

        [Test]
        public void Rebuild_PutsEverySignOnItsSlot()
        {
            StreetSegment segment = StreetTestFactory.Create("Road", Vector3.zero, new Vector3(0f, 0f, 40f));
            StreetSpeedSign sign = StreetTestFactory.Sign(segment, StreetSide.Right, 10f);

            segment.Rebuild();

            Assert.That(Vector3.Distance(sign.transform.position, new Vector3(9f, 0f, 10f)), Is.LessThan(0.05f));
        }

        [Test]
        public void Rebuild_TakesTheSignAlongWhenTheRoadMoves()
        {
            StreetSegment segment = StreetTestFactory.Create("Road", Vector3.zero, new Vector3(0f, 0f, 40f));
            StreetSpeedSign sign = StreetTestFactory.Sign(segment, StreetSide.Right, 10f);
            segment.Rebuild();

            StreetSurgery.SplineOf(segment).SetKnot(0, new BezierKnot(new float3(0f, 0f, -20f)));
            segment.Rebuild();

            // Still 10 m from the start, and the start is now at -20.
            Assert.That(sign.transform.position.z, Is.EqualTo(-10f).Within(0.05f));
        }

        [Test]
        public void Rebuild_KeepsTheStoredDistanceWhenTheRoadIsTooShort()
        {
            // Lengthening the road again has to bring the sign back to where the author put it.
            StreetSegment segment = StreetTestFactory.Create("Road", Vector3.zero, new Vector3(0f, 0f, 40f));
            StreetSpeedSign sign = StreetTestFactory.Sign(segment, StreetSide.Right, 60f);

            segment.Rebuild();

            Assert.That(sign.transform.position.z, Is.EqualTo(40f).Within(0.05f));
            Assert.That(sign.distance, Is.EqualTo(60f));
        }

        [Test]
        public void Segment_IsTheStreetTheSignStandsOn()
        {
            StreetSegment segment = StreetTestFactory.Create("Road", Vector3.zero, new Vector3(0f, 0f, 40f));
            StreetSpeedSign sign = StreetTestFactory.Sign(segment, StreetSide.Left, 5f);

            Assert.That(sign.Segment, Is.SameAs(segment));
            Assert.That(sign.SpeedLimit, Is.EqualTo(30f / 3.6f).Within(0.001f));
        }
    }
}
