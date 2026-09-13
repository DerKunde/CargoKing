using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace CargoKing.Streets.Tests
{
    public class StreetNetworkAssetTests
    {
        [Test]
        public void ExitsOf_ReturnsOnlyTheLanesOfThatLane()
        {
            StreetNetworkAsset asset = ScriptableObject.CreateInstance<StreetNetworkAsset>();

            StreetNetworkLane[] lanes =
            {
                new StreetNetworkLane { firstExit = 0, exitCount = 2 },
                new StreetNetworkLane { firstExit = 2, exitCount = 1 },
            };

            asset.Write(new StreetNetworkSample[0], lanes, new[] { 1, 1, 0 }, default, "hash");

            List<int> exits = new List<int>();
            asset.ExitsOf(1, exits);

            Assert.That(exits, Is.EquivalentTo(new[] { 0 }));

            Object.DestroyImmediate(asset);
        }

        [Test]
        public void Write_ReplacesEverythingIncludingTheHash()
        {
            StreetNetworkAsset asset = ScriptableObject.CreateInstance<StreetNetworkAsset>();

            asset.Write(new StreetNetworkSample[1], new StreetNetworkLane[1], new int[0], default, "first");
            asset.Write(new StreetNetworkSample[3], new StreetNetworkLane[2], new int[0], default, "second");

            Assert.That(asset.Samples.Length, Is.EqualTo(3));
            Assert.That(asset.Lanes.Length, Is.EqualTo(2));
            Assert.That(asset.ContentHash, Is.EqualTo("second"));

            Object.DestroyImmediate(asset);
        }
    }
}
