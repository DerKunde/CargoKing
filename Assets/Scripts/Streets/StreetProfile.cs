using UnityEngine;

namespace CargoKing.Streets
{
    /// <summary>
    /// A class of road: what it is built from, how wide it is, how fast traffic may travel on it and
    /// how densely it is populated.
    ///
    /// Everything that says how a street looks lives here rather than on the segment, so a street never
    /// needs a mesh or a material assigned by hand, and changing the profile changes every street of
    /// its class. Width sits with the tile on purpose: the mesh builder does not scale the tile, so a
    /// width that does not match it produces lanes that miss the carriageway.
    /// </summary>
    [CreateAssetMenu(menuName = "CargoKing/Street Profile", fileName = "StreetProfile")]
    public class StreetProfile : ScriptableObject
    {
        /// <summary>Slowest limit a lane may end up with, in metres per second. Roughly walking pace.</summary>
        public const float MinimumSpeedLimit = 1.5f;

        private const float KilometresPerHourToMetresPerSecond = 1f / 3.6f;

        [Tooltip("Speed limit for roads of this class, in km/h.")]
        [Min(0f)]
        public float speedLimitKmh = 50f;

        [Tooltip("How many vehicles ambient traffic aims for per kilometre of lane.")]
        [Min(0f)]
        public float trafficDensity = 8f;

        [Header("Look")]
        [Tooltip("Tile repeated along the street. Needs Read/Write enabled in its import settings.")]
        public Mesh tileMesh;

        [Tooltip("Local axis of the tile that points along the direction of travel.")]
        public StreetMeshAxis forwardAxis = StreetMeshAxis.X;

        [Tooltip("Nominal length of one tile in metres. 0 measures it from the mesh itself.")]
        [Min(0f)]
        public float tileLength;

        [Tooltip("Material the street is drawn with.")]
        public Material material;

        [Tooltip("Width of the carriageway in metres - the driveable part, without verges. Must match "
            + "the tile and every intersection socket this class of road docks to.")]
        [Min(0f)]
        public float roadWidth = 16f;

        [System.NonSerialized]
        private int version;

        /// <summary>The limit in metres per second, never zero.</summary>
        public float SpeedLimit => Mathf.Max(speedLimitKmh * KilometresPerHourToMetresPerSecond, MinimumSpeedLimit);

        /// <summary>
        /// Advances whenever the profile is edited. A segment remembers the number it was built with
        /// and rebuilds when it moves on, which is how one edit here reaches every street of the class.
        /// </summary>
        public int Version => version;

        /// <summary>Tells every segment using this profile to rebuild.</summary>
        public void MarkChanged()
        {
            version++;
        }

        private void OnValidate()
        {
            MarkChanged();
        }
    }
}
