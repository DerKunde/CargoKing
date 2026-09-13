using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace CargoKing.Streets.Editor.Tests
{
    public class StreetProfileChoiceTests
    {
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

        /// <summary>An intersection with two sockets, the way the prefabs are built.</summary>
        private IntersectionSocket[] CreateIntersection(string name)
        {
            GameObject root = new GameObject(name);
            extra.Add(root);
            root.AddComponent<Intersection>();

            IntersectionSocket[] sockets = new IntersectionSocket[2];
            for (int index = 0; index < sockets.Length; index++)
            {
                GameObject socket = new GameObject($"Socket {index}");
                socket.transform.SetParent(root.transform, false);
                sockets[index] = socket.AddComponent<IntersectionSocket>();
            }

            return sockets;
        }

        private StreetProfile CreateProfile(string name)
        {
            StreetProfile profile = ScriptableObject.CreateInstance<StreetProfile>();
            profile.name = name;
            extra.Add(profile);
            return profile;
        }

        [Test]
        public void ForSocket_TakesTheProfileOfAStreetAtTheSameIntersection()
        {
            // The arms of one junction come from the same kit, so the neighbour is a better guess than
            // the project-wide default.
            IntersectionSocket[] sockets = CreateIntersection("Junction");
            StreetProfile neighbours = CreateProfile("Avenue");

            StreetSegment docked = StreetTestFactory.Create("Docked", Vector3.zero, new Vector3(0f, 0f, 20f));
            docked.profile = neighbours;
            docked.startConnection.socket = sockets[1];

            StreetProfile chosen = StreetProfileChoice.ForSocket(
                sockets[0], new[] { docked }, CreateProfile("Default"));

            Assert.That(chosen, Is.SameAs(neighbours));
        }

        [Test]
        public void ForSocket_IgnoresStreetsAtOtherIntersections()
        {
            IntersectionSocket[] here = CreateIntersection("Here");
            IntersectionSocket[] elsewhere = CreateIntersection("Elsewhere");
            StreetProfile fallback = CreateProfile("Default");

            StreetSegment docked = StreetTestFactory.Create("Docked", Vector3.zero, new Vector3(0f, 0f, 20f));
            docked.profile = CreateProfile("Lane");
            docked.startConnection.socket = elsewhere[0];

            Assert.That(StreetProfileChoice.ForSocket(here[0], new[] { docked }, fallback), Is.SameAs(fallback));
        }

        [Test]
        public void ForSocket_FallsBackWhenNoNeighbourHasAProfile()
        {
            IntersectionSocket[] sockets = CreateIntersection("Junction");
            StreetProfile fallback = CreateProfile("Default");

            StreetSegment docked = StreetTestFactory.Create("Docked", Vector3.zero, new Vector3(0f, 0f, 20f));
            docked.profile = null;
            docked.startConnection.socket = sockets[1];

            Assert.That(StreetProfileChoice.ForSocket(sockets[0], new[] { docked }, fallback), Is.SameAs(fallback));
        }

        [Test]
        public void ForSocket_ReturnsNullWhenThereIsNothingToChooseFrom()
        {
            // Creation is never blocked: the street is made without a profile and says so.
            IntersectionSocket[] sockets = CreateIntersection("Junction");

            Assert.That(StreetProfileChoice.ForSocket(sockets[0], new StreetSegment[0], null), Is.Null);
        }
    }
}
