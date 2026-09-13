using NUnit.Framework;
using UnityEngine;

namespace CargoKing.Streets.Tests
{
    public class StreetNetworkRuntimeProjectTests
    {
        private StreetNetworkAsset asset;

        /// <summary>
        /// Two parallel lanes 10 m apart in Z, each running 100 m along +X, sampled every 10 m.
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
                        position = new Vector3(index * 10f, 0f, lane * 10f),
                        direction = Vector3.right,
                        distance = index * 10f,
                        radius = float.PositiveInfinity,
                    };
                }
            }

            StreetNetworkLane[] lanes =
            {
                new StreetNetworkLane { firstSample = 0, sampleCount = 11, length = 100f, speedLimit = 14f, intersection = -1, pathIndex = -1 },
                new StreetNetworkLane { firstSample = 11, sampleCount = 11, length = 100f, speedLimit = 14f, intersection = -1, pathIndex = -1 },
            };

            StreetNetworkGridData grid = StreetNetworkGrid.Build(samples, lanes, 25f);

            asset = ScriptableObject.CreateInstance<StreetNetworkAsset>();
            asset.Write(samples, lanes, new int[0], grid, "test");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(asset);
        }

        [Test]
        public void TryProject_PicksTheNearerOfTwoLanes()
        {
            StreetNetworkRuntime runtime = new StreetNetworkRuntime(asset);

            Assert.That(runtime.TryProject(new Vector3(50f, 0f, 8f), out StreetRoutePosition position), Is.True);
            Assert.That(position.lane, Is.EqualTo(1));
            Assert.That(position.distance, Is.EqualTo(50f).Within(0.1f));
        }

        [Test]
        public void TryProject_FindsALaneEvenWhenThePointIsFarOff()
        {
            // A car pushed off the road by the police is still meant to be locatable, which is why the
            // search widens instead of giving up at the first empty ring of cells.
            StreetNetworkRuntime runtime = new StreetNetworkRuntime(asset);

            Assert.That(runtime.TryProject(new Vector3(50f, 0f, 120f), out StreetRoutePosition position), Is.True);
            Assert.That(position.lane, Is.EqualTo(1));
        }

        [Test]
        public void TryProject_OnAnEmptyNetworkFails()
        {
            StreetNetworkAsset empty = ScriptableObject.CreateInstance<StreetNetworkAsset>();
            StreetNetworkRuntime runtime = new StreetNetworkRuntime(empty);

            Assert.That(runtime.TryProject(Vector3.zero, out StreetRoutePosition position), Is.False);
            Assert.That(position.IsValid, Is.False);

            Object.DestroyImmediate(empty);
        }
    }
}
