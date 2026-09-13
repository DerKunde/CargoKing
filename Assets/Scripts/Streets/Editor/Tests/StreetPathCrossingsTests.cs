using NUnit.Framework;
using UnityEngine;

namespace CargoKing.Streets.Editor.Tests
{
    public class StreetPathCrossingsTests
    {
        /// <summary>Adds a straight two-sample path to the result and returns its lane index.</summary>
        private static int AddPath(
            StreetNetworkBakeResult result,
            Vector3 from,
            Vector3 to,
            int intersection,
            int pathIndex)
        {
            int first = result.samples.Count;

            result.samples.Add(new StreetNetworkSample { position = from, distance = 0f, radius = float.PositiveInfinity });
            result.samples.Add(new StreetNetworkSample
            {
                position = to,
                distance = Vector3.Distance(from, to),
                radius = float.PositiveInfinity,
            });

            result.lanes.Add(new StreetNetworkLane
            {
                firstSample = first,
                sampleCount = 2,
                length = Vector3.Distance(from, to),
                speedLimit = 25f,
                intersection = intersection,
                pathIndex = pathIndex,
            });

            result.laneSources.Add(null);
            return result.lanes.Count - 1;
        }

        [Test]
        public void Apply_MarksTwoPathsThatCrossInTheMiddle()
        {
            StreetNetworkBakeResult result = new StreetNetworkBakeResult();

            int west = AddPath(result, new Vector3(-10f, 0f, 0f), new Vector3(10f, 0f, 0f), 0, 0);
            int south = AddPath(result, new Vector3(0f, 0f, -10f), new Vector3(0f, 0f, 10f), 0, 1);

            StreetPathCrossings.Apply(result);

            Assert.That(result.lanes[west].crossingMask & (1 << 1), Is.Not.Zero);
            Assert.That(result.lanes[south].crossingMask & (1 << 0), Is.Not.Zero);
        }

        [Test]
        public void Apply_LeavesTwoPathsThatDoNotMeetAlone()
        {
            // Two right turns from opposite arms. Letting these run at once is the whole reason a path
            // is reserved rather than the intersection.
            StreetNetworkBakeResult result = new StreetNetworkBakeResult();

            int first = AddPath(result, new Vector3(-10f, 0f, -3f), new Vector3(-3f, 0f, -10f), 0, 0);
            int second = AddPath(result, new Vector3(10f, 0f, 3f), new Vector3(3f, 0f, 10f), 0, 1);

            StreetPathCrossings.Apply(result);

            Assert.That(result.lanes[first].crossingMask, Is.Zero);
            Assert.That(result.lanes[second].crossingMask, Is.Zero);
        }

        [Test]
        public void Apply_MarksTwoPathsThatEndAtTheSameExit()
        {
            // They never cross but they merge, and two cars arriving at the same exit at once collide
            // just as surely as two that cross.
            StreetNetworkBakeResult result = new StreetNetworkBakeResult();

            int first = AddPath(result, new Vector3(-10f, 0f, 0f), new Vector3(0f, 0f, 10f), 0, 0);
            int second = AddPath(result, new Vector3(10f, 0f, 0f), new Vector3(0f, 0f, 10f), 0, 1);

            StreetPathCrossings.Apply(result);

            Assert.That(result.lanes[first].crossingMask & (1 << 1), Is.Not.Zero);
        }

        [Test]
        public void Apply_NeverMarksPathsOfDifferentIntersections()
        {
            // Two intersections stacked at the same coordinates would otherwise block each other. Only
            // paths of one intersection are ever compared, which is also why five bits are enough.
            StreetNetworkBakeResult result = new StreetNetworkBakeResult();

            int first = AddPath(result, new Vector3(-10f, 0f, 0f), new Vector3(10f, 0f, 0f), 0, 0);
            int second = AddPath(result, new Vector3(0f, 0f, -10f), new Vector3(0f, 0f, 10f), 1, 0);

            StreetPathCrossings.Apply(result);

            Assert.That(result.lanes[first].crossingMask, Is.Zero);
            Assert.That(result.lanes[second].crossingMask, Is.Zero);
        }

        [Test]
        public void Apply_IgnoresRoadLanes()
        {
            StreetNetworkBakeResult result = new StreetNetworkBakeResult();

            int road = AddPath(result, new Vector3(-10f, 0f, 0f), new Vector3(10f, 0f, 0f), -1, -1);
            AddPath(result, new Vector3(0f, 0f, -10f), new Vector3(0f, 0f, 10f), -1, -1);

            StreetPathCrossings.Apply(result);

            Assert.That(result.lanes[road].crossingMask, Is.Zero);
        }
    }
}
