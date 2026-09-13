using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace CargoKing.Streets.Tests
{
    public class StreetRouteSearchTests
    {
        private StreetNetworkAsset asset;
        private readonly List<int> route = new List<int>();

        /// <summary>
        /// A diamond. Lane 0 splits into a short slow way (1) and a long fast way (2); both rejoin at
        /// lane 3. Lane 4 is a detached island, unreachable from anywhere.
        ///
        ///        1 (100 m at 5 m/s = 20 s)
        ///      /                          \
        ///   0                               3
        ///      \                          /
        ///        2 (300 m at 30 m/s = 10 s)
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            StreetNetworkSample[] samples = new StreetNetworkSample[10];
            for (int index = 0; index < samples.Length; index++)
            {
                samples[index] = new StreetNetworkSample
                {
                    position = new Vector3(index * 10f, 0f, 0f),
                    direction = Vector3.right,
                    distance = (index % 2) * 100f,
                    radius = float.PositiveInfinity,
                };
            }

            StreetNetworkLane[] lanes =
            {
                Lane(0, 100f, 20f, firstExit: 0, exitCount: 2),
                Lane(2, 100f, 5f, firstExit: 2, exitCount: 1),
                Lane(4, 300f, 30f, firstExit: 3, exitCount: 1),
                Lane(6, 100f, 20f, firstExit: 4, exitCount: 0),
                Lane(8, 100f, 20f, firstExit: 4, exitCount: 0),
            };

            int[] exits = { 1, 2, 3, 3 };

            asset = ScriptableObject.CreateInstance<StreetNetworkAsset>();
            asset.Write(samples, lanes, exits, default, "test");
        }

        private static StreetNetworkLane Lane(int firstSample, float length, float speedLimit, int firstExit, int exitCount)
        {
            return new StreetNetworkLane
            {
                firstSample = firstSample,
                sampleCount = 2,
                length = length,
                speedLimit = speedLimit,
                intersection = -1,
                pathIndex = -1,
                firstExit = firstExit,
                exitCount = exitCount,
                turn = StreetTurn.Straight,
            };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(asset);
        }

        [Test]
        public void TryFind_PrefersTheFasterWayOverTheShorterOne()
        {
            Assert.That(StreetRouteSearch.TryFind(asset, 0, 0f, 3, route), Is.True);
            Assert.That(route, Is.EqualTo(new[] { 0, 2, 3 }));
        }

        [Test]
        public void TryFind_StartingOnTheGoalLaneReturnsJustThatLane()
        {
            Assert.That(StreetRouteSearch.TryFind(asset, 3, 10f, 3, route), Is.True);
            Assert.That(route, Is.EqualTo(new[] { 3 }));
        }

        [Test]
        public void TryFind_ReportsAnUnreachableGoal()
        {
            Assert.That(StreetRouteSearch.TryFind(asset, 0, 0f, 4, route), Is.False);
            Assert.That(route, Is.Empty);
        }

        [Test]
        public void TryFind_RejectsALaneIndexOutOfRange()
        {
            Assert.That(StreetRouteSearch.TryFind(asset, 0, 0f, 99, route), Is.False);
        }

        [Test]
        public void TurnPenalty_CostsMoreForALeftTurnThanForGoingStraight()
        {
            // Without this a route search treats a left turn across oncoming traffic as free and picks
            // zigzag routes through side streets that no driver would choose.
            Assert.That(
                StreetRouteSearch.TurnPenalty(StreetTurn.Left),
                Is.GreaterThan(StreetRouteSearch.TurnPenalty(StreetTurn.Straight)));

            Assert.That(
                StreetRouteSearch.TurnPenalty(StreetTurn.Left),
                Is.GreaterThan(StreetRouteSearch.TurnPenalty(StreetTurn.Right)));
        }
    }
}
