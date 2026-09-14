using System.Collections.Generic;
using CargoKing.Streets;
using NUnit.Framework;
using UnityEngine;

namespace CargoKing.Traffic.Tests
{
    public class SpeedPlannerTests
    {
        private const float Fifty = 50f / 3.6f;
        private const float Thirty = 30f / 3.6f;
        private const float Deceleration = 3f;

        private readonly List<Object> created = new List<Object>();
        private readonly List<int> route = new List<int>();
        private DrivingProfile profile;

        [SetUp]
        public void SetUp()
        {
            // NUnit keeps one instance of the fixture for all its tests, so a test that changes the
            // route would otherwise hand its route on to the next.
            route.Clear();
            route.Add(0);

            profile = ScriptableObject.CreateInstance<DrivingProfile>();
            created.Add(profile);
        }

        [TearDown]
        public void TearDown()
        {
            for (int index = 0; index < created.Count; index++)
            {
                Object.DestroyImmediate(created[index]);
            }

            created.Clear();
        }

        /// <summary>One straight lane, 200 m along +X, a sample every 10 m, 50 km/h throughout.</summary>
        private StreetNetworkRuntime Straight(System.Action<StreetNetworkSample[]> change = null)
        {
            StreetNetworkSample[] samples = new StreetNetworkSample[21];
            for (int index = 0; index < samples.Length; index++)
            {
                samples[index] = new StreetNetworkSample
                {
                    position = new Vector3(index * 10f, 0f, 0f),
                    direction = Vector3.right,
                    distance = index * 10f,
                    radius = float.PositiveInfinity,
                    speedLimit = Fifty,
                };
            }

            change?.Invoke(samples);

            StreetNetworkLane[] lanes =
            {
                new StreetNetworkLane
                {
                    firstSample = 0, sampleCount = 21, length = 200f, speedLimit = Fifty,
                    intersection = -1, pathIndex = -1, turn = StreetTurn.Straight,
                },
            };

            StreetNetworkAsset asset = ScriptableObject.CreateInstance<StreetNetworkAsset>();
            asset.Write(samples, lanes, System.Array.Empty<int>(), default, "test");
            created.Add(asset);

            return new StreetNetworkRuntime(asset);
        }

        private float Allowed(StreetNetworkRuntime runtime, float at, float speed, float distanceToGoal = float.PositiveInfinity)
        {
            return SpeedPlanner.AllowedSpeed(
                runtime, route, new StreetRoutePosition { lane = 0, distance = at }, speed, distanceToGoal, profile, Deceleration);
        }

        [Test]
        public void AllowedSpeed_OnAStraightIsTheLimitTimesTheFactor()
        {
            Assert.That(Allowed(Straight(), 0f, 10f), Is.EqualTo(13.194f).Within(0.01f));
        }

        [Test]
        public void AllowedSpeed_BrakesForABendAheadWithinTheBrakingDistance()
        {
            // A 20 m radius around 50 m. From 31 m at 13 m/s the probe at 41 m is the first to see it:
            // sqrt(3.4335 * 20) = 8.287 there, carried back over 10 m: sqrt(8.287^2 + 2 * 3 * 10) = 11.343.
            StreetNetworkRuntime runtime = Straight(samples => samples[5].radius = 20f);

            Assert.That(Allowed(runtime, 31f, 13f), Is.EqualTo(11.343f).Within(0.01f));
        }

        [Test]
        public void AllowedSpeed_IgnoresABendBeyondTheBrakingDistance()
        {
            // At 5 m/s the car looks 5^2 / 6 + 10 = 14 m ahead; the bend starts to show at 40 m.
            StreetNetworkRuntime runtime = Straight(samples => samples[5].radius = 20f);

            Assert.That(Allowed(runtime, 0f, 5f), Is.EqualTo(13.194f).Within(0.01f));
        }

        [Test]
        public void AllowedSpeed_BrakesForALowerLimitAhead()
        {
            // 30 km/h from 50 m on. From 41 m the probe at 51 m reads it: 30 km/h * 0.95 = 7.917, carried
            // back over 10 m: sqrt(7.917^2 + 60) = 11.076.
            StreetNetworkRuntime runtime = Straight(samples =>
            {
                for (int index = 5; index < samples.Length; index++)
                {
                    samples[index].speedLimit = Thirty;
                }
            });

            Assert.That(Allowed(runtime, 41f, 13f), Is.EqualTo(11.076f).Within(0.01f));
        }

        [Test]
        public void AllowedSpeed_StopsAtTheGoal()
        {
            // 5 m from the goal: sqrt(2 * 3 * 5) = 5.477.
            Assert.That(Allowed(Straight(), 0f, 10f, distanceToGoal: 5f), Is.EqualTo(5.477f).Within(0.01f));
        }

        [Test]
        public void AllowedSpeed_IsZeroWhenThePositionIsNotOnTheRoute()
        {
            route.Clear();
            route.Add(3);

            Assert.That(Allowed(Straight(), 0f, 10f), Is.EqualTo(0f));
        }

        /// <summary>Two lanes of 100 m end to end. Only their lengths matter here.</summary>
        private StreetNetworkAsset TwoLanes()
        {
            StreetNetworkSample[] samples =
            {
                new StreetNetworkSample { distance = 0f }, new StreetNetworkSample { distance = 100f },
                new StreetNetworkSample { distance = 0f }, new StreetNetworkSample { distance = 100f },
            };

            StreetNetworkLane[] lanes =
            {
                new StreetNetworkLane { firstSample = 0, sampleCount = 2, length = 100f, intersection = -1, pathIndex = -1, firstExit = 0, exitCount = 1 },
                new StreetNetworkLane { firstSample = 2, sampleCount = 2, length = 100f, intersection = -1, pathIndex = -1, firstExit = 1, exitCount = 0 },
            };

            StreetNetworkAsset asset = ScriptableObject.CreateInstance<StreetNetworkAsset>();
            asset.Write(samples, lanes, new[] { 1 }, default, "test");
            created.Add(asset);
            return asset;
        }

        [Test]
        public void DistanceToGoal_AddsTheRestOfThisLaneAndTheWayIntoTheLast()
        {
            float distance = SpeedPlanner.DistanceToGoal(
                TwoLanes(), new List<int> { 0, 1 }, new StreetRoutePosition { lane = 0, distance = 30f }, 50f);

            Assert.That(distance, Is.EqualTo(120f).Within(0.001f));
        }

        [Test]
        public void DistanceToGoal_OnTheGoalLaneIsTheGapToTheGoal()
        {
            float distance = SpeedPlanner.DistanceToGoal(
                TwoLanes(), new List<int> { 0 }, new StreetRoutePosition { lane = 0, distance = 30f }, 80f);

            Assert.That(distance, Is.EqualTo(50f).Within(0.001f));
        }

        [Test]
        public void DistanceToGoal_IsNeverNegative()
        {
            float distance = SpeedPlanner.DistanceToGoal(
                TwoLanes(), new List<int> { 0 }, new StreetRoutePosition { lane = 0, distance = 30f }, 10f);

            Assert.That(distance, Is.EqualTo(0f));
        }
    }
}
