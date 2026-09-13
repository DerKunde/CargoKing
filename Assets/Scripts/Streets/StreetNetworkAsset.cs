using System.Collections.Generic;
using UnityEngine;

namespace CargoKing.Streets
{
    /// <summary>
    /// The baked street network: every lane in the scene, flattened into arrays that hold no
    /// reference to any object in it.
    ///
    /// Deliberately dumb. It answers nothing by itself - <see cref="StreetNetworkRuntime"/> does the
    /// asking. A timetable, not a scene graph.
    /// </summary>
    [CreateAssetMenu(menuName = "CargoKing/Street Network Asset", fileName = "StreetNetwork")]
    public class StreetNetworkAsset : ScriptableObject
    {
        [SerializeField] private StreetNetworkSample[] samples = System.Array.Empty<StreetNetworkSample>();
        [SerializeField] private StreetNetworkLane[] lanes = System.Array.Empty<StreetNetworkLane>();
        [SerializeField] private int[] exits = System.Array.Empty<int>();
        [SerializeField] private StreetNetworkGridData grid;

        [SerializeField]
        [Tooltip("Hash of the scene objects this was baked from. Mismatch means the bake is stale.")]
        private string contentHash = string.Empty;

        public StreetNetworkSample[] Samples => samples;

        public StreetNetworkLane[] Lanes => lanes;

        /// <summary>Lane indices, grouped per lane. Address them through <see cref="ExitsOf"/>.</summary>
        public int[] Exits => exits;

        public StreetNetworkGridData Grid => grid;

        public string ContentHash => contentHash;

        /// <summary>True when there is anything to drive on.</summary>
        public bool IsEmpty => lanes.Length == 0;

        /// <summary>Replaces the whole content. There is no partial bake; a network is baked whole.</summary>
        public void Write(
            StreetNetworkSample[] bakedSamples,
            StreetNetworkLane[] bakedLanes,
            int[] bakedExits,
            StreetNetworkGridData bakedGrid,
            string bakedHash)
        {
            samples = bakedSamples ?? System.Array.Empty<StreetNetworkSample>();
            lanes = bakedLanes ?? System.Array.Empty<StreetNetworkLane>();
            exits = bakedExits ?? System.Array.Empty<int>();
            grid = bakedGrid;
            contentHash = bakedHash ?? string.Empty;
        }

        /// <summary>Fills the list with the lanes that carry on from this one.</summary>
        public void ExitsOf(int lane, List<int> results)
        {
            results.Clear();

            if (lane < 0 || lane >= lanes.Length)
            {
                return;
            }

            StreetNetworkLane entry = lanes[lane];
            for (int index = 0; index < entry.exitCount; index++)
            {
                results.Add(exits[entry.firstExit + index]);
            }
        }
    }
}
