using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace CargoKing.Streets.Editor.Tests
{
    public class StreetNetworkValidationTests
    {
        private readonly List<StreetNetworkIssue> issues = new List<StreetNetworkIssue>();

        [TearDown]
        public void TearDown()
        {
            StreetTestFactory.DestroyAll();
        }

        [Test]
        public void Run_ReportsAnOpenEndAsAWarningAndStillPasses()
        {
            // A dead end is legitimate in an open world - a cul-de-sac, an unfinished corner of the
            // map. Worth pointing out, not worth refusing to bake over.
            StreetSegment segment = StreetTestFactory.Create("Road", Vector3.zero, new Vector3(50f, 0f, 0f));

            bool passed = StreetNetworkValidation.Run(
                new[] { segment }, System.Array.Empty<Intersection>(), issues);

            Assert.That(passed, Is.True);
            Assert.That(issues.Count, Is.EqualTo(2));
            Assert.That(issues[0].severity, Is.EqualTo(StreetNetworkIssueSeverity.Warning));
        }

        [Test]
        public void Run_RejectsTwoSegmentsOnTheSameSocket()
        {
            GameObject intersectionObject = new GameObject("Intersection");
            Intersection intersection = intersectionObject.AddComponent<Intersection>();

            GameObject socketObject = new GameObject("Socket");
            socketObject.transform.SetParent(intersectionObject.transform);
            IntersectionSocket socket = socketObject.AddComponent<IntersectionSocket>();
            socket.roadWidth = 16f;

            StreetSegment first = StreetTestFactory.Create("First", Vector3.zero, new Vector3(50f, 0f, 0f));
            StreetSegment second = StreetTestFactory.Create("Second", Vector3.zero, new Vector3(50f, 0f, 0f));

            first.startConnection.socket = socket;
            second.startConnection.socket = socket;

            bool passed = StreetNetworkValidation.Run(new[] { first, second }, new[] { intersection }, issues);

            Assert.That(passed, Is.False);
            Assert.That(issues.Exists(issue => issue.severity == StreetNetworkIssueSeverity.Error), Is.True);

            Object.DestroyImmediate(intersectionObject);
        }

        [Test]
        public void Run_RejectsAWidthMismatchAtASocket()
        {
            // The lanes are placed from the width. A mismatch means the lanes miss each other at the
            // seam, which no amount of driving skill recovers from.
            GameObject intersectionObject = new GameObject("Intersection");
            Intersection intersection = intersectionObject.AddComponent<Intersection>();

            GameObject socketObject = new GameObject("Socket");
            socketObject.transform.SetParent(intersectionObject.transform);
            IntersectionSocket socket = socketObject.AddComponent<IntersectionSocket>();
            socket.roadWidth = 7f;

            StreetSegment segment = StreetTestFactory.Create("Road", Vector3.zero, new Vector3(50f, 0f, 0f));
            segment.roadWidth = 16f;
            segment.startConnection.socket = socket;

            bool passed = StreetNetworkValidation.Run(new[] { segment }, new[] { intersection }, issues);

            Assert.That(passed, Is.False);

            Object.DestroyImmediate(intersectionObject);
        }

        [Test]
        public void Run_PassesACleanNetwork()
        {
            StreetSegment first = StreetTestFactory.Create("First", Vector3.zero, new Vector3(50f, 0f, 0f));
            StreetSegment second = StreetTestFactory.Create("Second", new Vector3(50f, 0f, 0f), new Vector3(100f, 0f, 0f));

            first.startConnection.segment = second;
            first.startConnection.segmentEnd = StreetEnd.End;
            first.endConnection.segment = second;
            first.endConnection.segmentEnd = StreetEnd.Start;
            second.startConnection.segment = first;
            second.startConnection.segmentEnd = StreetEnd.End;
            second.endConnection.segment = first;
            second.endConnection.segmentEnd = StreetEnd.Start;

            bool passed = StreetNetworkValidation.Run(
                new[] { first, second }, System.Array.Empty<Intersection>(), issues);

            Assert.That(passed, Is.True);
            Assert.That(issues, Is.Empty);
        }
    }
}
