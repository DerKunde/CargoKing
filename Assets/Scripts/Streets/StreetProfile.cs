using UnityEngine;

namespace CargoKing.Streets
{
    /// <summary>
    /// A class of road: how fast traffic may travel on it and how densely it is populated.
    ///
    /// Width is deliberately not here. It sits on <see cref="StreetSegment"/> and
    /// <see cref="IntersectionSocket"/>, where it is already authored and where the mesh and the
    /// sockets both read it; moving it would reshape every existing road the moment a profile were
    /// assigned.
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

        /// <summary>The limit in metres per second, never zero.</summary>
        public float SpeedLimit => Mathf.Max(speedLimitKmh * KilometresPerHourToMetresPerSecond, MinimumSpeedLimit);
    }
}
