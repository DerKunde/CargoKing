using NUnit.Framework;
using UnityEngine;

namespace CargoKing.Streets.Editor.Tests
{
    public class StreetSegmentLayerTests
    {
        [TearDown]
        public void TearDown()
        {
            StreetTestFactory.DestroyAll();
        }

        [Test]
        public void Rebuild_PutsTheSegmentAndEveryChildOnTheFloorLayer()
        {
            // Picking a goal by mouse raycasts against Floor only; a street on any other layer cannot
            // be clicked. Inactive children count too, or a sign switched off and on again would not.
            int floor = LayerMask.NameToLayer("Floor");
            Assume.That(floor, Is.GreaterThanOrEqualTo(0), "The project has no Floor layer.");

            StreetSegment segment = StreetTestFactory.Create("Road", Vector3.zero, new Vector3(50f, 0f, 0f));

            GameObject child = new GameObject("Child");
            child.transform.SetParent(segment.transform, false);

            GameObject grandchild = new GameObject("Grandchild");
            grandchild.transform.SetParent(child.transform, false);
            grandchild.SetActive(false);

            segment.Rebuild();

            Assert.That(segment.gameObject.layer, Is.EqualTo(floor));
            Assert.That(child.layer, Is.EqualTo(floor));
            Assert.That(grandchild.layer, Is.EqualTo(floor));
        }
    }
}
