using NUnit.Framework;
using UnityEngine;

namespace CargoKing.Streets.Editor.Tests
{
    public class StreetNetworkBakeJobTests
    {
        [TearDown]
        public void TearDown()
        {
            StreetTestFactory.DestroyAll();
        }

        [Test]
        public void ComputeHash_ChangesWhenASegmentMoves()
        {
            StreetSegment segment = StreetTestFactory.Create("Road", Vector3.zero, new Vector3(50f, 0f, 0f));
            segment.Rebuild();

            string before = StreetNetworkBakeJob.ComputeHash(new[] { segment }, System.Array.Empty<Intersection>());

            segment.transform.position = new Vector3(0f, 0f, 25f);

            string after = StreetNetworkBakeJob.ComputeHash(new[] { segment }, System.Array.Empty<Intersection>());

            Assert.That(after, Is.Not.EqualTo(before));
        }

        [Test]
        public void ComputeHash_StaysTheSameWhenNothingChanges()
        {
            // Every save and every play recomputes this. If it were not stable the editor would report
            // a stale bake constantly and the auto-bake would rebake the world on every save.
            StreetSegment segment = StreetTestFactory.Create("Road", Vector3.zero, new Vector3(50f, 0f, 0f));
            segment.Rebuild();

            string first = StreetNetworkBakeJob.ComputeHash(new[] { segment }, System.Array.Empty<Intersection>());
            string second = StreetNetworkBakeJob.ComputeHash(new[] { segment }, System.Array.Empty<Intersection>());

            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void ComputeHash_ChangesWithTheSpeedLimit()
        {
            StreetSegment segment = StreetTestFactory.Create("Road", Vector3.zero, new Vector3(50f, 0f, 0f));
            segment.Rebuild();

            string before = StreetNetworkBakeJob.ComputeHash(new[] { segment }, System.Array.Empty<Intersection>());

            segment.speedLimitOverrideKmh = 30f;

            string after = StreetNetworkBakeJob.ComputeHash(new[] { segment }, System.Array.Empty<Intersection>());

            Assert.That(after, Is.Not.EqualTo(before));
        }

        [Test]
        public void ComputeHash_ChangesWithASignsLimit()
        {
            StreetSegment segment = StreetTestFactory.Create("Road", Vector3.zero, new Vector3(50f, 0f, 0f));
            StreetSpeedSign sign = StreetTestFactory.Sign(segment, StreetSide.Right, 20f, 30f);
            segment.Rebuild();

            string before = StreetNetworkBakeJob.ComputeHash(new[] { segment }, System.Array.Empty<Intersection>());

            sign.limitKmh = 70f;

            string after = StreetNetworkBakeJob.ComputeHash(new[] { segment }, System.Array.Empty<Intersection>());

            Assert.That(after, Is.Not.EqualTo(before));
        }

        [Test]
        public void ComputeHash_ChangesWhenASignMoves()
        {
            StreetSegment segment = StreetTestFactory.Create("Road", Vector3.zero, new Vector3(50f, 0f, 0f));
            StreetSpeedSign sign = StreetTestFactory.Sign(segment, StreetSide.Right, 20f, 30f);
            segment.Rebuild();

            string before = StreetNetworkBakeJob.ComputeHash(new[] { segment }, System.Array.Empty<Intersection>());

            sign.distance = 25f;

            string after = StreetNetworkBakeJob.ComputeHash(new[] { segment }, System.Array.Empty<Intersection>());

            Assert.That(after, Is.Not.EqualTo(before));
        }
    }
}
