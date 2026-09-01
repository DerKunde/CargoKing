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

            Assert.IsFalse(JunctionPlacement.TryAlign(
                bare, Vector3.zero, Vector3.forward, Vector3.up, false, out _, out string problem));
            Assert.IsNotNull(problem);

            Object.DestroyImmediate(bare);
        }
    }
}
