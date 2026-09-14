using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace CargoKing.Streets.Tests
{
    public class StreetNetworkRuntimeRouteTests
    {
        private StreetNetworkAsset asset;
        private StreetNetworkRuntime runtime;
        private readonly List<int> route = new List<int>();

        /// <summary>
        /// Two lanes end to end: lane 0 runs 0..100 along +X, lane 1 carries on 100..200.
        /// Both are sampled every 10 m.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            StreetNetworkSample[] samples = new StreetNetworkSample[22];

            for (int lane = 0; lane < 2; lane++)
            {
                for (int index = 0; index < 11; index++)
                {
                    samples[lane * 11 + index] = new StreetNetworkSample
                    {
                        position = new Vector3(lane * 100f + index * 10f, 0f, 0f),
                        direction = Vector3.right,
                        distance = index * 10f,
                        radius = float.PositiveInfinity,
                    };
                }
            }

            StreetNetworkLane[] lanes =
            {
                new StreetNetworkLane
                {
                    firstSample = 0, sampleCount = 11, length = 100f, speedLimit = 14f,
                    intersection = -1, pathIndex = -1, firstExit = 0, exitCount = 1, turn = StreetTurn.Straight,
                },
                new StreetNetworkLane
                {
                    firstSample = 11, sampleCount = 11, length = 100f, speedLimit = 14f,
                    intersection = -1, pathIndex = -1, firstExit = 1, exitCount = 0, turn = StreetTurn.Straight,
                },
            };

            StreetNetworkGridData grid = StreetNetworkGrid.Build(samples, lanes, 25f);

            asset = ScriptableObject.CreateInstance<StreetNetworkAsset>();
            asset.Write(samples, lanes, new[] { 1 }, grid, "test");
            runtime = new StreetNetworkRuntime(asset);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(asset);
        }

        [Test]
        public void TryFindRoute_JoinsTwoLanes()
        {
            StreetRoutePosition from = new StreetRoutePosition { lane = 0, distance = 10f };
            StreetRoutePosition to = new StreetRoutePosition { lane = 1, distance = 50f };

            Assert.That(runtime.TryFindRoute(from, to, route), Is.True);
            Assert.That(route, Is.EqualTo(new[] { 0, 1 }));
        }

        [Test]
        public void TryFindRoute_HonoursWhereOnTheGoalLaneTheGoalLies()
        {
            // Lane 1 is a dead end. With the goal behind the start on it, there is no way there - a
            // runtime that dropped the goal's distance would report "already there" instead.
            StreetRoutePosition from = new StreetRoutePosition { lane = 1, distance = 60f };
            StreetRoutePosition to = new StreetRoutePosition { lane = 1, distance = 20f };

            Assert.That(runtime.TryFindRoute(from, to, route), Is.False);
        }

        [Test]
        public void TrySampleAhead_StaysOnTheCurrentLaneWhenItIsLongEnough()
        {
            route.Clear();
            route.AddRange(new[] { 0, 1 });

            StreetRoutePosition position = new StreetRoutePosition { lane = 0, distance = 20f };

            Assert.That(runtime.TrySampleAhead(route, position, 30f, out StreetNetworkSample sample), Is.True);
            Assert.That(sample.position.x, Is.EqualTo(50f).Within(0.1f));
        }

        [Test]
        public void TrySampleAhead_CarriesOnIntoTheNextLane()
        {
            // The whole point of taking the route rather than the lane: a look-ahead that stopped at
            // the end of the current lane would make every car brake at every seam.
            route.Clear();
            route.AddRange(new[] { 0, 1 });

            StreetRoutePosition position = new StreetRoutePosition { lane = 0, distance = 80f };

            Assert.That(runtime.TrySampleAhead(route, position, 50f, out StreetNetworkSample sample), Is.True);
            Assert.That(sample.position.x, Is.EqualTo(130f).Within(0.1f));
        }

        [Test]
        public void TrySampleAhead_BeyondTheLastLaneReturnsItsEnd()
        {
            route.Clear();
            route.AddRange(new[] { 0, 1 });

            StreetRoutePosition position = new StreetRoutePosition { lane = 1, distance = 90f };

            Assert.That(runtime.TrySampleAhead(route, position, 500f, out StreetNetworkSample sample), Is.True);
            Assert.That(sample.position.x, Is.EqualTo(200f).Within(0.1f));
        }

        [Test]
        public void TrySampleAhead_FailsWhenThePositionIsNotOnTheRoute()
        {
            route.Clear();
            route.Add(1);

            StreetRoutePosition position = new StreetRoutePosition { lane = 0, distance = 10f };

            Assert.That(runtime.TrySampleAhead(route, position, 10f, out StreetNetworkSample _), Is.False);
        }

        [Test]
        public void TryProjectNear_KeepsToTheLanesAroundTheHint()
        {
            StreetRoutePosition hint = new StreetRoutePosition { lane = 0, distance = 50f };

            Assert.That(
                runtime.TryProjectNear(hint, new Vector3(55f, 0f, 1f), 20f, out StreetRoutePosition position),
                Is.True);

            Assert.That(position.lane, Is.EqualTo(0));
            Assert.That(position.distance, Is.EqualTo(55f).Within(0.5f));
        }

        [Test]
        public void TryProjectNear_FailsWhenThePointIsOutsideTheWindow()
        {
            // Failing here is the signal a driver uses to fall back to a full projection, so it has to
            // fail rather than return a nonsense nearby answer.
            StreetRoutePosition hint = new StreetRoutePosition { lane = 0, distance = 50f };

            Assert.That(
                runtime.TryProjectNear(hint, new Vector3(50f, 0f, 400f), 20f, out StreetRoutePosition _),
                Is.False);
        }
    }
}
