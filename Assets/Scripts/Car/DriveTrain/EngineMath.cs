using System;
using UnityEngine;

namespace CargoKing.Car
{
    /// <summary>
    /// Full-load engine torque over rpm, as two power-law halves that meet at the peak:
    ///
    ///   rpm &lt;= peakRpm:  T = peakTorque - (peakTorque - torqueAtIdle) * ((peakRpm - rpm) / (peakRpm - idleRpm)) ^ riseShape
    ///   rpm &gt;  peakRpm:  T = peakTorque - (peakTorque - torqueAtMaxRpm) * ((rpm - peakRpm) / (maxRpm - peakRpm)) ^ fallShape
    ///
    /// It passes exactly through (idle, torqueAtIdle), (peakRpm, peakTorque) and (max, torqueAtMaxRpm),
    /// and for shapes above 1 both halves leave the peak flat. Unlike a Gaussian it can rise steeply from
    /// idle and fall slowly after the peak, as real engines do. Brake torque, already net of friction -
    /// see <see cref="DriveTrainMath.CombustionTorque"/>.
    /// </summary>
    [Serializable]
    public struct TorqueCurve
    {
        [Tooltip("Torque at idle rpm, N*m.")]
        public float torqueAtIdle;

        [Tooltip("Highest torque, N*m.")]
        public float peakTorque;

        [Tooltip("Rpm of the highest torque. Held between idle and max rpm.")]
        public float peakRpm;

        [Tooltip("Torque at max rpm, N*m.")]
        public float torqueAtMaxRpm;

        [Tooltip("Shape of the rise from idle to the peak. 1 is a straight line, 2 a parabola; above 1 the top is smooth.")]
        public float riseShape;

        [Tooltip("Shape of the fall from the peak to max rpm. Larger values hold the torque up longer and drop it late.")]
        public float fallShape;

        public TorqueCurve(float torqueAtIdle, float peakTorque, float peakRpm, float torqueAtMaxRpm, float riseShape, float fallShape)
        {
            this.torqueAtIdle = torqueAtIdle;
            this.peakTorque = peakTorque;
            this.peakRpm = peakRpm;
            this.torqueAtMaxRpm = torqueAtMaxRpm;
            this.riseShape = riseShape;
            this.fallShape = fallShape;
        }

        /// <summary>
        /// Fitted to the reference curve (Punto 1.2 Pop, 11 points from 1000 to 6000 rpm) with both end
        /// points held: no point is more than 1.7 N*m off. Assumes 1000 idle and 6000 max rpm.
        /// </summary>
        public static TorqueCurve PuntoReference => new TorqueCurve(31f, 100.3f, 2675f, 72f, 1.56f, 4.32f);
    }

    /// <summary>
    /// Pure engine math with no MonoBehaviour or scene dependency, so it can run under a plain
    /// EditMode test - same arrangement as DriveTrainMath and TireMath.
    /// </summary>
    public static class EngineMath
    {
        /// <summary>Metric horsepower per kilowatt.</summary>
        public const float KilowattsToPs = 1.35962f;

        // Keeps a shape that is being typed in from turning the curve into a step (x^0 = 1).
        private const float MinShape = 0.01f;

        // Below this much room between idle and max rpm there is no space for two halves.
        private const float MinRpmRange = 2f;

        /// <summary>
        /// Full-load torque at <paramref name="rpm"/>, N*m. Below idle the rising half carries on,
        /// clamped at 0; above max rpm the torque holds at <see cref="TorqueCurve.torqueAtMaxRpm"/> and
        /// the rev limiter does the rest. The peak rpm is held just inside (idle, max) so the formula
        /// never divides by zero.
        /// </summary>
        public static float Torque(float rpm, in TorqueCurve curve, float idleRpm, float maxRpm)
        {
            if (maxRpm - idleRpm < MinRpmRange)
            {
                return Mathf.Max(0f, curve.peakTorque);
            }

            float peakRpm = Mathf.Clamp(curve.peakRpm, idleRpm + 1f, maxRpm - 1f);

            if (rpm <= peakRpm)
            {
                // Above 1 below idle: the rise continues downwards until the clamp stops it.
                float towardsIdle = (peakRpm - rpm) / (peakRpm - idleRpm);
                float rising = curve.peakTorque
                    - (curve.peakTorque - curve.torqueAtIdle) * Mathf.Pow(towardsIdle, Mathf.Max(MinShape, curve.riseShape));
                return Mathf.Max(0f, rising);
            }

            float towardsMax = Mathf.Min(1f, (rpm - peakRpm) / (maxRpm - peakRpm));
            float falling = curve.peakTorque
                - (curve.peakTorque - curve.torqueAtMaxRpm) * Mathf.Pow(towardsMax, Mathf.Max(MinShape, curve.fallShape));
            return Mathf.Max(0f, falling);
        }

        /// <summary>Mechanical power, W: torque times angular velocity.</summary>
        public static float PowerWatts(float torque, float rpm)
        {
            return torque * rpm * 2f * Mathf.PI / 60f;
        }
    }
}
