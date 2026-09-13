using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace CargoKing.Streets.Editor.Tests
{
    public class StreetSnappingTargetsTests
    {
        private readonly List<StreetSnapTarget> targets = new List<StreetSnapTarget>();
        private readonly List<Object> extra = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            StreetTestFactory.DestroyAll();

            for (int index = 0; index < extra.Count; index++)
            {
                if (extra[index] != null)
                {
                    Object.DestroyImmediate(extra[index]);
                }
            }

            extra.Clear();
        }

        private IntersectionSocket CreateSocket(Vector3 position)
        {
            GameObject root = new GameObject("Junction");
            extra.Add(root);
            root.AddComponent<Intersection>();

            GameObject socket = new GameObject("Socket");
            socket.transform.SetParent(root.transform, false);
            socket.transform.position = position;
            return socket.AddComponent<IntersectionSocket>();
        }

        private static StreetSnapTarget EndOf(List<StreetSnapTarget> list, StreetSegment segment, StreetEnd end)
        {
            return list.Find(target => target.segment == segment && target.segmentEnd == end);
        }

        [Test]
        public void CollectTargets_ListsBothEndsOfAnotherSegmentButNoneOfItsOwn()
        {
            StreetSegment dragged = StreetTestFactory.Create("Dragged", Vector3.zero, new Vector3(0f, 0f, 20f));
            StreetSegment other = StreetTestFactory.Create("Other", new Vector3(50f, 0f, 0f), new Vector3(50f, 0f, 20f));

            StreetSnapping.CollectTargets(dragged, targets);

            Assert.That(EndOf(targets, other, StreetEnd.Start).IsValid, Is.True);
            Assert.That(EndOf(targets, other, StreetEnd.End).IsValid, Is.True);
            Assert.That(targets.Exists(target => target.segment == dragged), Is.False);
        }

        [Test]
        public void CollectTargets_MarksAnOpenEndFreeAndAConnectedEndTaken()
        {
            StreetSegment dragged = StreetTestFactory.Create("Dragged", Vector3.zero, new Vector3(0f, 0f, 20f));
            StreetSegment other = StreetTestFactory.Create("Other", new Vector3(50f, 0f, 0f), new Vector3(50f, 0f, 20f));
            other.endConnection.socket = CreateSocket(new Vector3(50f, 0f, 20f));

            StreetSnapping.CollectTargets(dragged, targets);

            Assert.That(EndOf(targets, other, StreetEnd.Start).isFree, Is.True);
            Assert.That(EndOf(targets, other, StreetEnd.End).isFree, Is.False);
        }

        [Test]
        public void CollectTargets_MarksASocketTakenOnlyWhenAnotherSegmentDocksToIt()
        {
            // The dragged segment's own socket stays free: pulling an end off and dropping it back
            // onto the same socket has to remain possible.
            StreetSegment dragged = StreetTestFactory.Create("Dragged", Vector3.zero, new Vector3(0f, 0f, 20f));
            StreetSegment other = StreetTestFactory.Create("Other", new Vector3(50f, 0f, 0f), new Vector3(50f, 0f, 20f));

            IntersectionSocket own = CreateSocket(Vector3.zero);
            IntersectionSocket taken = CreateSocket(new Vector3(50f, 0f, 0f));
            IntersectionSocket free = CreateSocket(new Vector3(100f, 0f, 0f));

            dragged.startConnection.socket = own;
            other.startConnection.socket = taken;

            StreetSnapping.CollectTargets(dragged, targets);

            Assert.That(targets.Find(target => target.socket == own).isFree, Is.True);
            Assert.That(targets.Find(target => target.socket == taken).isFree, Is.False);
            Assert.That(targets.Find(target => target.socket == free).isFree, Is.True);
        }

        [Test]
        public void FindNearest_StillPicksTheClosestTargetWithinReach()
        {
            // The same search the drawing now shares. Its answer must not change with it.
            StreetSegment dragged = StreetTestFactory.Create("Dragged", Vector3.zero, new Vector3(0f, 0f, 20f));
            StreetSegment near = StreetTestFactory.Create("Near", new Vector3(0f, 0f, 22f), new Vector3(0f, 0f, 40f));
            StreetTestFactory.Create("Far", new Vector3(0f, 0f, 25f), new Vector3(0f, 0f, 45f));

            StreetSnapTarget found = StreetSnapping.FindNearest(
                new Vector3(0f, 0f, 20f), dragged, StreetSnapping.SnapRadius);

            Assert.That(found.segment, Is.SameAs(near));
            Assert.That(found.segmentEnd, Is.EqualTo(StreetEnd.Start));
        }

        [Test]
        public void FindNearest_FindsNothingBeyondTheRadius()
        {
            StreetSegment dragged = StreetTestFactory.Create("Dragged", Vector3.zero, new Vector3(0f, 0f, 20f));
            StreetTestFactory.Create("Far", new Vector3(0f, 0f, 60f), new Vector3(0f, 0f, 80f));

            StreetSnapTarget found = StreetSnapping.FindNearest(
                new Vector3(0f, 0f, 20f), dragged, StreetSnapping.SnapRadius);

            Assert.That(found.IsValid, Is.False);
        }
    }
}
