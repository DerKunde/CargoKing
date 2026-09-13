using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CargoKing.Streets.Editor.Tests
{
    public class StreetTestFactoryTests
    {
        [TearDown]
        public void TearDown()
        {
            StreetTestFactory.DestroyAll();
        }

        [Test]
        public void RevertingTheUndoHistory_LeavesNoFactorySegmentInTheScene()
        {
            // The Test Runner reverts every undo step recorded during a run once the run is over.
            // A merge destroys the dragged segment with an undo record; if the segment's creation had
            // no record of its own, reverting brought it back into the open scene and left it there.
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();

            StreetSegment target = StreetTestFactory.Create("Target", Vector3.zero, new Vector3(0f, 0f, 10f));
            StreetSegment dragged = StreetTestFactory.Create(
                "Stray Test Segment", new Vector3(0f, 0f, 10f), new Vector3(0f, 0f, 30f));

            Assert.That(StreetSurgery.Merge(dragged, StreetEnd.Start, target, StreetEnd.End), Is.Not.Null);
            StreetTestFactory.DestroyAll();

            Undo.RevertAllDownToGroup(group);

            Assert.That(GameObject.Find("Stray Test Segment"), Is.Null);
            Assert.That(GameObject.Find("Target"), Is.Null);
        }
    }
}
