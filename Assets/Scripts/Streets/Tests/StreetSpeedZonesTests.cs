using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace CargoKing.Streets.Tests
{
    public class StreetSpeedZonesTests
    {
        private const float Thirty = 30f / 3.6f;
        private const float Fifty = 50f / 3.6f;
        private const float Seventy = 70f / 3.6f;

        /// <summary>Lanes of 100 m sampled every 10 m, laid end to end along +X. No exits yet.</summary>
        private static void Build(int laneCount, out StreetNetworkSample[] samples, out StreetNetworkLane[] lanes)
        {
            samples = new StreetNetworkSample[laneCount * 11];
            lanes = new StreetNetworkLane[laneCount];

            for (int lane = 0; lane < laneCount; lane++)
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

                lanes[lane] = new StreetNetworkLane
                {
                    firstSample = lane * 11,
                    sampleCount = 11,
                    length = 100f,
                    intersection = -1,
                    pathIndex = -1,
                };
            }
        }

        /// <summary>Writes the exits: pairs of (from, to) lane indices.</summary>
        private static int[] Link(StreetNetworkLane[] lanes, params int[] pairs)
        {
            List<int> exits = new List<int>();

            for (int lane = 0; lane < lanes.Length; lane++)
            {
                lanes[lane].firstExit = exits.Count;
                lanes[lane].exitCount = 0;

                for (int pair = 0; pair < pairs.Length; pair += 2)
                {
                    if (pairs[pair] == lane)
                    {
                        exits.Add(pairs[pair + 1]);
                        lanes[lane].exitCount++;
                    }
                }
            }

            return exits.ToArray();
        }

        private static float[] Defaults(int count, float limit)
        {
            float[] limits = new float[count];
            for (int index = 0; index < count; index++)
            {
                limits[index] = limit;
            }

            return limits;
        }

        private static StreetSpeedSignPlacement Sign(int lane, float distance, float limit)
        {
            return new StreetSpeedSignPlacement { lane = lane, distance = distance, speedLimit = limit };
        }

        private static float LimitAt(StreetNetworkSample[] samples, StreetNetworkLane lane, float distance)
        {
            return StreetLaneGeometry.SampleAt(samples, lane, distance).speedLimit;
        }

        [Test]
        public void Apply_ChangesTheLimitExactlyAtTheSign()
        {
            Build(1, out StreetNetworkSample[] samples, out StreetNetworkLane[] lanes);

            samples = StreetSpeedZones.Apply(samples, lanes, Link(lanes), Defaults(1, Fifty),
                new[] { Sign(0, 45f, Thirty) });

            Assert.That(LimitAt(samples, lanes[0], 44.9f), Is.EqualTo(Fifty).Within(0.001f));
            Assert.That(LimitAt(samples, lanes[0], 45f), Is.EqualTo(Thirty).Within(0.001f));
            Assert.That(LimitAt(samples, lanes[0], 90f), Is.EqualTo(Thirty).Within(0.001f));
        }

        [Test]
        public void Apply_InsertsExactlyOneSampleAtTheSign()
        {
            Build(1, out StreetNetworkSample[] samples, out StreetNetworkLane[] lanes);

            samples = StreetSpeedZones.Apply(samples, lanes, Link(lanes), Defaults(1, Fifty),
                new[] { Sign(0, 45f, Thirty) });

            Assert.That(lanes[0].sampleCount, Is.EqualTo(12));

            int atSign = 0;
            for (int index = 0; index < lanes[0].sampleCount; index++)
            {
                StreetNetworkSample sample = samples[lanes[0].firstSample + index];

                if (Mathf.Abs(sample.distance - 45f) < 0.001f)
                {
                    atSign++;
                    Assert.That(sample.position.x, Is.EqualTo(45f).Within(0.001f));
                }

                if (index > 0)
                {
                    Assert.That(sample.distance, Is.GreaterThan(samples[lanes[0].firstSample + index - 1].distance));
                }
            }

            Assert.That(atSign, Is.EqualTo(1));
        }

        [Test]
        public void Apply_InsertsNothingWhereASampleAlreadyStands()
        {
            Build(1, out StreetNetworkSample[] samples, out StreetNetworkLane[] lanes);

            StreetSpeedZones.Apply(samples, lanes, Link(lanes), Defaults(1, Fifty), new[] { Sign(0, 50f, Thirty) });

            Assert.That(lanes[0].sampleCount, Is.EqualTo(11));
        }

        [Test]
        public void Apply_TheNextSignSupersedesTheLast()
        {
            Build(1, out StreetNetworkSample[] samples, out StreetNetworkLane[] lanes);

            samples = StreetSpeedZones.Apply(samples, lanes, Link(lanes), Defaults(1, Fifty),
                new[] { Sign(0, 20f, Thirty), Sign(0, 60f, Seventy) });

            Assert.That(LimitAt(samples, lanes[0], 10f), Is.EqualTo(Fifty).Within(0.001f));
            Assert.That(LimitAt(samples, lanes[0], 40f), Is.EqualTo(Thirty).Within(0.001f));
            Assert.That(LimitAt(samples, lanes[0], 80f), Is.EqualTo(Seventy).Within(0.001f));
        }

        [Test]
        public void Apply_CarriesAZoneAcrossADockedStreet()
        {
            Build(2, out StreetNetworkSample[] samples, out StreetNetworkLane[] lanes);
            int[] exits = Link(lanes, 0, 1);

            samples = StreetSpeedZones.Apply(samples, lanes, exits, Defaults(2, Fifty), new[] { Sign(0, 50f, Thirty) });

            Assert.That(LimitAt(samples, lanes[1], 0f), Is.EqualTo(Thirty).Within(0.001f));
            Assert.That(LimitAt(samples, lanes[1], 99f), Is.EqualTo(Thirty).Within(0.001f));
        }

        [Test]
        public void Apply_AnIntersectionPathTakesItsEntryLimitAndTheStreetAfterItResets()
        {
            Build(3, out StreetNetworkSample[] samples, out StreetNetworkLane[] lanes);
            lanes[1].intersection = 0;
            lanes[1].pathIndex = 0;
            int[] exits = Link(lanes, 0, 1, 1, 2);

            float[] defaults = { Fifty, 25f, Fifty };

            samples = StreetSpeedZones.Apply(samples, lanes, exits, defaults, new[] { Sign(0, 50f, Thirty) });

            // Driving in at 30 means 30 through the junction...
            Assert.That(LimitAt(samples, lanes[1], 50f), Is.EqualTo(Thirty).Within(0.001f));

            // ...and the street beyond starts again from its own limit.
            Assert.That(LimitAt(samples, lanes[2], 50f), Is.EqualTo(Fifty).Within(0.001f));
        }

        [Test]
        public void Apply_TerminatesOnARingOfStreets()
        {
            Build(2, out StreetNetworkSample[] samples, out StreetNetworkLane[] lanes);
            int[] exits = Link(lanes, 0, 1, 1, 0);

            samples = StreetSpeedZones.Apply(samples, lanes, exits, Defaults(2, Fifty), new[] { Sign(1, 50f, Thirty) });

            // The walk begins at lane 0, which therefore starts from its own default.
            Assert.That(LimitAt(samples, lanes[0], 10f), Is.EqualTo(Fifty).Within(0.001f));
            Assert.That(LimitAt(samples, lanes[1], 10f), Is.EqualTo(Fifty).Within(0.001f));
            Assert.That(LimitAt(samples, lanes[1], 60f), Is.EqualTo(Thirty).Within(0.001f));
        }

        [Test]
        public void Apply_TwoSignsAtOnePlace_TheLaterOneWins()
        {
            Build(1, out StreetNetworkSample[] samples, out StreetNetworkLane[] lanes);

            samples = StreetSpeedZones.Apply(samples, lanes, Link(lanes), Defaults(1, Fifty),
                new[] { Sign(0, 45f, Thirty), Sign(0, 45f, Seventy) });

            Assert.That(lanes[0].sampleCount, Is.EqualTo(12));
            Assert.That(LimitAt(samples, lanes[0], 60f), Is.EqualTo(Seventy).Within(0.001f));
        }

        [Test]
        public void Apply_IgnoresASignOnAnIntersectionPath()
        {
            Build(1, out StreetNetworkSample[] samples, out StreetNetworkLane[] lanes);
            lanes[0].intersection = 0;

            samples = StreetSpeedZones.Apply(samples, lanes, Link(lanes), Defaults(1, Fifty),
                new[] { Sign(0, 50f, Thirty) });

            Assert.That(LimitAt(samples, lanes[0], 80f), Is.EqualTo(Fifty).Within(0.001f));
        }

        [Test]
        public void Apply_WritesTheHighestLimitAndTheTravelTime()
        {
            Build(1, out StreetNetworkSample[] samples, out StreetNetworkLane[] lanes);

            StreetSpeedZones.Apply(samples, lanes, Link(lanes), Defaults(1, 20f), new[] { Sign(0, 50f, 10f) });

            Assert.That(lanes[0].speedLimit, Is.EqualTo(20f).Within(0.001f));
            Assert.That(lanes[0].travelTime, Is.EqualTo(7.5f).Within(0.001f));
        }

        [Test]
        public void Apply_KeepsEveryLaneOnItsOwnSamples()
        {
            Build(2, out StreetNetworkSample[] samples, out StreetNetworkLane[] lanes);

            samples = StreetSpeedZones.Apply(samples, lanes, Link(lanes), Defaults(2, Fifty),
                new[] { Sign(0, 45f, Thirty) });

            Assert.That(lanes[1].firstSample, Is.EqualTo(12));
            Assert.That(samples[lanes[1].firstSample].position.x, Is.EqualTo(100f).Within(0.001f));
            Assert.That(samples.Length, Is.EqualTo(23));
        }
    }
}
