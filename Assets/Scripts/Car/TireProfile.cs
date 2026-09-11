using UnityEngine;

namespace CargoKing.Car
{
    /// <summary>
    /// How a tire turns slip into force: one Pacejka curve per direction plus load sensitivity.
    /// Shared by reference, so all four wheels of a car (or one axle each) read the same asset and
    /// the values live in exactly one place - no per-wheel copies, no scene overrides.
    ///
    /// Edits made in Play Mode persist after leaving it, as with any asset. That is intended: tune
    /// while driving, keep the result.
    /// </summary>
    [CreateAssetMenu(menuName = "CargoKing/Tire Profile", fileName = "TireProfile")]
    public class TireProfile : ScriptableObject
    {
        /// <summary>Range the longitudinal peak is searched in and the preview spans: 0 to 100% slip.</summary>
        public const float SlipRatioRange = 1f;

        /// <summary>Range the lateral peak is searched in and the preview spans, degrees.</summary>
        public const float SlipAngleRange = 30f;

        // Keeps combined slip from dividing by a zero peak while a coefficient is being typed in.
        private const float MinPeakSlipRatio = 1e-4f;
        private const float MinPeakSlipAngle = 0.01f;

        [Header("Longitudinal (x = slip ratio, 0.1 = 10%)")]
        // Peak at 10% slip with a broad top: 95% of it left at 30% slip, 80% with the wheel
        // spinning or locked - close to real dry-asphalt tires. An earlier E = 0 start value kept
        // only 62% there, so a spinning wheel barely held the engine back and launches flared.
        public PacejkaCurve longitudinal = new PacejkaCurve(28.95f, 1.65f, 0.9f, 0.9f);

        [Header("Lateral (x = slip angle in degrees)")]
        public PacejkaCurve lateral = new PacejkaCurve(0.376f, 1.3f, 0.9f, 0f);

        [Header("Load")]
        [Tooltip("Load at which D applies unchanged, N. Car_v2: 800 kg * 9.81 / 4.")]
        public float nominalLoad = 1962f;

        [Tooltip("How much the friction coefficient drops per unit of load above nominal. 0.1: twice the load brings 1.8 times the grip.")]
        [Range(0f, 0.5f)]
        public float loadSensitivity = 0.1f;

        /// <summary>Where the longitudinal curve peaks, as a slip ratio. Combined slip is normalised by it.</summary>
        public float PeakSlipRatio { get; private set; }

        /// <summary>Where the lateral curve peaks, degrees. Combined slip is normalised by it.</summary>
        public float PeakSlipAngle { get; private set; }

        /// <summary>False when the longitudinal curve still rises at the range end (C &lt;= 1, or B too small).</summary>
        public bool HasLongitudinalPeak { get; private set; }

        /// <summary>False when the lateral curve still rises at the range end (C &lt;= 1, or B too small).</summary>
        public bool HasLateralPeak { get; private set; }

        public TireCurves Curves => new TireCurves(longitudinal, lateral, PeakSlipRatio, PeakSlipAngle, nominalLoad, loadSensitivity);

        private void OnEnable()
        {
            RecalculatePeaks();
        }

        private void OnValidate()
        {
            RecalculatePeaks();
        }

        /// <summary>
        /// Finds both peaks again. Called on every change, so the physics always normalises with
        /// the curve as it is currently tuned; public for the inspector, which redraws before
        /// OnValidate is guaranteed to have run.
        /// </summary>
        public void RecalculatePeaks()
        {
            PeakSlipRatio = Mathf.Max(MinPeakSlipRatio, TireMath.PeakInput(longitudinal, SlipRatioRange, out bool hasLongitudinalPeak));
            PeakSlipAngle = Mathf.Max(MinPeakSlipAngle, TireMath.PeakInput(lateral, SlipAngleRange, out bool hasLateralPeak));
            HasLongitudinalPeak = hasLongitudinalPeak;
            HasLateralPeak = hasLateralPeak;
        }
    }
}
