using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace CargoKing.Streets.Tests
{
    public class StreetNetworkGridTests
    {
        /// <summary>Two lanes 200 m apart, each a straight run of samples one metre apart.</summary>
        private static void BuildTwoLanes(out StreetNetworkSample[] samples, out StreetNetworkLane[] lanes)
        {
            List<StreetNetworkSample> list = new List<StreetNetworkSample>();

            for (int index = 0; index < 100; index++)
            {
                list.Add(new StreetNetworkSample { position = new Vector3(index, 0f, 0f), distance = index });
            }

            for (int index = 0; index < 100; index++)
            {
                list.Add(new StreetNetworkSample { position = new Vector3(index, 0f, 200f), distance = index });
            }

            samples = list.ToArray();
            lanes = new[]
            {
                new StreetNetworkLane { firstSample = 0, sampleCount = 100, length = 99f },
                new StreetNetworkLane { firstSample = 100, sampleCount = 100, length = 99f },
            };
        }

        [Test]
        public void Overlapping_FindsTheLaneUnderThePoint()
        {
            BuildTwoLanes(out StreetNetworkSample[] samples, out StreetNetworkLane[] lanes);
            StreetNetworkGridData grid = StreetNetworkGrid.Build(samples, lanes, 25f);

            List<int> found = new List<int>();
            StreetNetworkGrid.Overlapping(grid, new Vector3(50f, 0f, 0f), 5f, found);

            Assert.That(found, Contains.Item(0));
        }

        [Test]
        public void Overlapping_LeavesOutALaneFarAway()
        {
            BuildTwoLanes(out StreetNetworkSample[] samples, out StreetNetworkLane[] lanes);
            StreetNetworkGridData grid = StreetNetworkGrid.Build(samples, lanes, 25f);

            List<int> found = new List<int>();
            StreetNetworkGrid.Overlapping(grid, new Vector3(50f, 0f, 0f), 5f, found);

            Assert.That(found, Has.No.Member(1));
        }

        [Test]
        public void Overlapping_ListsALaneOnlyOnceEvenWhenManySamplesShareACell()
        {
            // Samples sit a metre apart and cells are 25 m wide, so one cell holds 25 samples of the
            // same lane. A caller that then tests the lane 25 times is doing 25 times the work.
            BuildTwoLanes(out StreetNetworkSample[] samples, out StreetNetworkLane[] lanes);
            StreetNetworkGridData grid = StreetNetworkGrid.Build(samples, lanes, 25f);

            List<int> found = new List<int>();
            StreetNetworkGrid.Overlapping(grid, new Vector3(50f, 0f, 0f), 1f, found);

            Assert.That(found.FindAll(lane => lane == 0).Count, Is.EqualTo(1));
        }

        [Test]
        public void Build_CoversTheGapBetweenTwoDistantSamples()
        {
            // A straight road is sampled sparsely - up to 20 m between samples - so a lane can cross a
            // whole cell without putting a sample in it. Asking in the middle must still find it.
            StreetNetworkSample[] samples =
            {
                new StreetNetworkSample { position = new Vector3(0f, 0f, 0f), distance = 0f },
                new StreetNetworkSample { position = new Vector3(100f, 0f, 0f), distance = 100f },
            };

            StreetNetworkLane[] lanes = { new StreetNetworkLane { firstSample = 0, sampleCount = 2, length = 100f } };
            StreetNetworkGridData grid = StreetNetworkGrid.Build(samples, lanes, 25f);

            List<int> found = new List<int>();
            StreetNetworkGrid.Overlapping(grid, new Vector3(50f, 0f, 0f), 1f, found);

            Assert.That(found, Contains.Item(0));
        }

        [Test]
        public void Build_OnAnEmptyNetworkProducesAnInvalidGridRatherThanThrowing()
        {
            StreetNetworkGridData grid = StreetNetworkGrid.Build(
                new StreetNetworkSample[0], new StreetNetworkLane[0], 25f);

            Assert.That(grid.IsValid, Is.False);
        }
    }
}
