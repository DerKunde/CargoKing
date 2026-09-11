using UnityEngine;

namespace CargoKing.Car
{
    /// <summary>
    /// A gearbox as a table of ratios, plus final drive and driveline efficiency. Shared by
    /// reference and read by <see cref="CarEngine"/> on every step, like <see cref="EngineProfile"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "CargoKing/Gearbox Profile", fileName = "GearboxProfile")]
    public class GearboxProfile : ScriptableObject
    {
        [Tooltip("Forward gear ratios, first gear first.")]
        public float[] gears = { 3.2f, 1.9f, 1.3f, 1.0f, 0.8f };

        public float reverseRatio = 3.5f;

        [Tooltip("Final drive ratio.")]
        public float axleRatio = 4f;

        [Tooltip("Share of the torque that reaches the wheels.")]
        [Range(0.01f, 1f)]
        public float efficiency = 0.85f;

        [Tooltip("Reverse can only be engaged below this speed, m/s.")]
        public float maxReverseShiftSpeed = 1f;

        public int GearCount => gears != null ? gears.Length : 0;

        /// <summary>Ratio of a gear: 0 is reverse, 1 to <see cref="GearCount"/> the forward gears. A gear that does not exist reads 0.</summary>
        public float Ratio(int gear)
        {
            if (gear == 0)
            {
                return reverseRatio;
            }

            return gear >= 1 && gear <= GearCount ? gears[gear - 1] : 0f;
        }
    }
}
