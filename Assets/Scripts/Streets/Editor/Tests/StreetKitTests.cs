using NUnit.Framework;
using UnityEngine;

namespace CargoKing.Streets.Editor.Tests
{
    public class StreetKitTests
    {
        [Test]
        public void IsValidEntry_AcceptsAnObjectWithAnIntersectionOnItsRoot()
        {
            GameObject candidate = new GameObject("Junction");
            candidate.AddComponent<Intersection>();

            Assert.IsTrue(StreetKit.IsValidEntry(candidate));

            Object.DestroyImmediate(candidate);
        }

        [Test]
        public void IsValidEntry_RefusesAnObjectWithoutOne()
        {
            GameObject candidate = new GameObject("Not a junction");

            Assert.IsFalse(StreetKit.IsValidEntry(candidate));

            Object.DestroyImmediate(candidate);
        }

        [Test]
        public void IsValidEntry_RefusesNull()
        {
            Assert.IsFalse(StreetKit.IsValidEntry(null));
        }
    }
}
