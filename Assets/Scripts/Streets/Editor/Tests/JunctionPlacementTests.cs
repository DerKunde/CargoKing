using NUnit.Framework;
using UnityEngine;

namespace CargoKing.Streets.Editor.Tests
{
    public class JunctionPlacementTests
    {
        private GameObject junction;

        /// <summary>
        /// A crossing with four arms 9.5 m out, the way the project's prefabs are built: each socket
        /// looks away from the middle.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            junction = new GameObject("Junction");
            junction.AddComponent<Intersection>();

            AddSocket(new Vector3(0f, 0f, 9.5f), Vector3.forward);
            AddSocket(new Vector3(9.5f, 0f, 0f), Vector3.right);
            AddSocket(new Vector3(0f, 0f, -9.5f), Vector3.back);
            AddSocket(new Vector3(-9.5f, 0f, 0f), Vector3.left);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(junction);
        }

        private void AddSocket(Vector3 localPosition, Vector3 outward)
        {
            GameObject socket = new GameObject("Socket");
            socket.transform.SetParent(junction.transform, false);
            socket.transform.localPosition = localPosition;
            socket.transform.localRotation = Quaternion.LookRotation(outward, Vector3.up);
            socket.AddComponent<IntersectionSocket>().roadWidth = 16f;
        }

        [Test]
        public void TryAlign_PutsTheEntrySocketAgainstTheDirectionOfTravel()
        {
            Assert.IsTrue(JunctionPlacement.TryAlign(
                junction,
                new Vector3(100f, 0f, 50f),
                Vector3.forward,
                Vector3.up,
                false,
                out JunctionAlignment alignment,
                out string problem), problem);

            // The road arrives travelling +Z, so the socket it docks to has to look back down it.
            Vector3 entryOutward = alignment.rotation * alignment.entry.transform.localRotation * Vector3.forward;
            Assert.AreEqual(-1f, Vector3.Dot(entryOutward.normalized, Vector3.forward), 0.001f);
        }

        [Test]
        public void TryAlign_PutsTheMidpointOfThePairOnTheKnot()
        {
            Vector3 knot = new Vector3(100f, 0f, 50f);

            Assert.IsTrue(JunctionPlacement.TryAlign(
                junction, knot, Vector3.forward, Vector3.up, false, out JunctionAlignment alignment, out _));

            Vector3 entry = alignment.position + alignment.rotation * alignment.entry.transform.localPosition;
            Vector3 exit = alignment.position + alignment.rotation * alignment.exit.transform.localPosition;

            Assert.AreEqual(0f, Vector3.Distance((entry + exit) * 0.5f, knot), 0.001f);
        }

        [Test]
        public void TryAlign_ReportsTheDistanceFromTheMiddleToASocket()
        {
            Assert.IsTrue(JunctionPlacement.TryAlign(
                junction, Vector3.zero, Vector3.forward, Vector3.up, false, out JunctionAlignment alignment, out _));

            Assert.AreEqual(9.5f, alignment.socketOffset, 0.001f);
        }

        [Test]
        public void TryAlign_SwapsEntryAndExitWhenFlipped()
        {
            JunctionPlacement.TryAlign(
                junction, Vector3.zero, Vector3.forward, Vector3.up, false, out JunctionAlignment straight, out _);
            JunctionPlacement.TryAlign(
                junction, Vector3.zero, Vector3.forward, Vector3.up, true, out JunctionAlignment flipped, out _);

            Assert.AreSame(straight.entry, flipped.exit);
            Assert.AreSame(straight.exit, flipped.entry);
        }

        [Test]
        public void TryAlign_RefusesAJunctionWithoutAnOpposingPair()
        {
            GameObject bare = new GameObject("Bare");
            bare.AddComponent<Intersection>();

            // Add two sockets that are not opposing: one forward, one right. Their dot product is 0,
            // which fails the opposing-pair threshold, so TryAlign must reject through the first == null branch.
            GameObject socket1 = new GameObject("Socket");
            socket1.transform.SetParent(bare.transform, false);
            socket1.transform.localPosition = Vector3.zero;
            socket1.transform.localRotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            socket1.AddComponent<IntersectionSocket>().roadWidth = 16f;

            GameObject socket2 = new GameObject("Socket");
            socket2.transform.SetParent(bare.transform, false);
            socket2.transform.localPosition = Vector3.zero;
            socket2.transform.localRotation = Quaternion.LookRotation(Vector3.right, Vector3.up);
            socket2.AddComponent<IntersectionSocket>().roadWidth = 16f;

            Assert.IsFalse(JunctionPlacement.TryAlign(
                bare, Vector3.zero, Vector3.forward, Vector3.up, false, out _, out string problem));
            Assert.IsNotNull(problem);

            Object.DestroyImmediate(bare);
        }

        [Test]
        public void TryAlign_WorksWithJunctionNotAtOrigin()
        {
            // The fixture in SetUp is at the origin, which masks any local/world confusion because
            // InverseTransformPoint and InverseTransformDirection become no-ops. The real prefab
            // (Intersection_full.prefab) has its root at (106.41697, -0.051073894, -239.35114),
            // so test that configuration here. Assertions use world space to make the offset matter.
            junction.transform.SetPositionAndRotation(
                new Vector3(106.41697f, -0.051073894f, -239.35114f),
                Quaternion.Euler(0f, 37f, 0f));

            Vector3 knotPosition = new Vector3(100f, 0f, 50f);
            Assert.IsTrue(JunctionPlacement.TryAlign(
                junction,
                knotPosition,
                Vector3.forward,
                Vector3.up,
                false,
                out JunctionAlignment alignment,
                out string problem), problem);

            // Entry socket's resulting world outward must equal -roadDirection.
            Vector3 entryWorldOutward = alignment.rotation * junction.transform.InverseTransformDirection(alignment.entry.Outward);
            Assert.AreEqual(-1f, Vector3.Dot(entryWorldOutward.normalized, Vector3.forward), 0.001f);

            // Midpoint of the two sockets must land on the knot.
            Vector3 entryWorldPosition = alignment.position + alignment.rotation * junction.transform.InverseTransformPoint(alignment.entry.transform.position);
            Vector3 exitWorldPosition = alignment.position + alignment.rotation * junction.transform.InverseTransformPoint(alignment.exit.transform.position);
            Vector3 midpoint = (entryWorldPosition + exitWorldPosition) * 0.5f;
            Assert.AreEqual(0f, Vector3.Distance(midpoint, knotPosition), 0.001f);
        }
    }
}
