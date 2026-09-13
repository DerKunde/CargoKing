using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace CargoKing.Streets.Editor.Tests
{
    public class StreetSpeedBandTests
    {
        [Test]
        public void ColourFor_TellsTheCommonLimitsApart()
        {
            Assert.That(StreetSpeedBand.ColourFor(30f), Is.Not.EqualTo(StreetSpeedBand.ColourFor(50f)));
            Assert.That(StreetSpeedBand.ColourFor(50f), Is.Not.EqualTo(StreetSpeedBand.ColourFor(70f)));
            Assert.That(StreetSpeedBand.ColourFor(70f), Is.Not.EqualTo(StreetSpeedBand.ColourFor(100f)));
            Assert.That(StreetSpeedBand.ColourFor(49.9f), Is.EqualTo(StreetSpeedBand.ColourFor(50f)));
        }

        [Test]
        public void BuildRuns_CutsALaneWhereTheLimitChanges()
        {
            float[] limits = { 10f, 10f, 20f, 20f };
            StreetNetworkSample[] samples = new StreetNetworkSample[limits.Length];

            for (int index = 0; index < samples.Length; index++)
            {
                samples[index] = new StreetNetworkSample
                {
                    position = new Vector3(index * 10f, 0f, 0f),
                    distance = index * 10f,
                    speedLimit = limits[index],
                };
            }

            StreetNetworkLane lane = new StreetNetworkLane { firstSample = 0, sampleCount = 4, length = 30f };
            List<StreetSpeedBand.Run> runs = new List<StreetSpeedBand.Run>();

            StreetSpeedBand.BuildRuns(samples, lane, runs);

            Assert.That(runs.Count, Is.EqualTo(2));

            // The first run ends on the sample where the next limit starts, so the two touch.
            Assert.That(runs[0].points.Length, Is.EqualTo(3));
            Assert.That(runs[0].points[2].x, Is.EqualTo(20f).Within(0.001f));
            Assert.That(runs[0].colour, Is.EqualTo(StreetSpeedBand.ColourFor(36f)));

            Assert.That(runs[1].points.Length, Is.EqualTo(2));
            Assert.That(runs[1].points[0].x, Is.EqualTo(20f).Within(0.001f));
            Assert.That(runs[1].colour, Is.EqualTo(StreetSpeedBand.ColourFor(72f)));
        }
    }
}
