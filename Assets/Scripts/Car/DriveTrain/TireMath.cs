using System;
using UnityEngine;

namespace CargoKing.Car
{
    /// <summary>
    /// One Pacejka Magic Formula curve, y = D * sin(C * atan(B*x - E*(B*x - atan(B*x)))).
    ///
    /// D sets the peak (here: the friction coefficient at nominal load), C the shape - above 1
    /// the curve turns over, and D * sin(C * pi/2) is what is left far past the peak. B scales
    /// the input, so together with C it decides where the peak sits (tan(pi/2C) / B for E = 0).
    /// E bends the curve around the peak without changing its height.
    /// </summary>
    [Serializable]
    public struct PacejkaCurve
    {
        [Tooltip("Stiffness: scales the input. Larger B moves the peak to smaller slip.")]
        public float b;

        [Tooltip("Shape: above 1 the curve turns over after its peak. D * sin(C * 90 deg) is what is left far past it.")]
        public float c;

        [Tooltip("Peak: the friction coefficient at nominal load.")]
        public float d;

        [Tooltip("Curvature around the peak. 0 keeps the peak exactly at tan(90 deg / C) / B; above 1 the curve turns wavy.")]
        public float e;

        public PacejkaCurve(float b, float c, float d, float e)
        {
            this.b = b;
            this.c = c;
            this.d = d;
            this.e = e;
        }
    }

    public readonly struct TireCurves
    {
        public readonly PacejkaCurve Longitudinal;
        public readonly PacejkaCurve Lateral;
        public readonly float PeakSlipRatio;
        public readonly float PeakSlipAngle;
        public readonly float NominalLoad;
        public readonly float LoadSensitivity;

        public TireCurves(PacejkaCurve longitudinal, PacejkaCurve lateral, float peakSlipRatio, float peakSlipAngle, float nominalLoad, float loadSensitivity)
        {
            Longitudinal = longitudinal;
            Lateral = lateral;
            PeakSlipRatio = peakSlipRatio;
            PeakSlipAngle = peakSlipAngle;
            NominalLoad = nominalLoad;
            LoadSensitivity = loadSensitivity;
        }
    }

    public struct TireForces
    {
        public float Longitudinal;
        public float Lateral;
        public float SlipRatio;
        public float SlipAngle;
        public float GripUsage;
        public bool LongitudinalCapped;
    }

    /// <summary>
    /// Pure tire math with no MonoBehaviour or scene dependency, so it can run under a plain
    /// EditMode test - same arrangement as DriveTrainMath.
    /// </summary>
    public static class TireMath
    {
        /// <summary>
        /// Floor for the speed slip ratio and slip angle are measured against, m/s. Only keeps
        /// both finite at standstill; how hard the tire may act there is <see cref="CapForce"/>'s
        /// job.
        /// </summary>
        public const float MinSlipSpeed = 1f;

        /// <summary>
        /// The force one wheel's contact patch puts on the car during one drivetrain sub-step,
        /// from the wheel's own spin and the (frozen) velocity of its contact point.
        ///
        /// Longitudinal comes back along the roll direction, lateral along the lateral axis and
        /// already turned against the slide. Three stability caps apply: the longitudinal force
        /// against the body over the full step (<paramref name="bodyReferenceMass"/>, a quarter
        /// of the car - four wheels share one body) and against the wheel's own spin over the
        /// sub-step (inertia / r^2), the lateral force against the body over the full step
        /// (<paramref name="lateralReferenceMass"/>, EffectiveMassAt - the old deadbeat term).
        /// </summary>
        public static TireForces EvaluateTire(
            in TireCurves tire,
            float wheelAngularVelocity,
            float wheelRadius,
            float wheelInertia,
            float forwardVelocity,
            float lateralVelocity,
            float normalForce,
            float bodyReferenceMass,
            float lateralReferenceMass,
            float stepDeltaTime,
            float subStepDeltaTime)
        {
            if (normalForce <= 0f)
            {
                return default;
            }

            float surfaceSpeed = wheelAngularVelocity * wheelRadius;
            float slipVelocity = surfaceSpeed - forwardVelocity;
            float slipRatio = SlipRatio(surfaceSpeed, forwardVelocity, MinSlipSpeed);
            float slipAngle = SlipAngleDegrees(lateralVelocity, forwardVelocity, MinSlipSpeed);
            float gripLoad = normalForce * LoadFactor(normalForce, tire.NominalLoad, tire.LoadSensitivity);

            Vector2 curveForce = CombinedForce(slipRatio, slipAngle, tire.Longitudinal, tire.Lateral,
                tire.PeakSlipRatio, tire.PeakSlipAngle, gripLoad);

            float longitudinal = CapForce(curveForce.x, slipVelocity, bodyReferenceMass, stepDeltaTime);
            longitudinal = CapForce(longitudinal, slipVelocity, wheelInertia / (wheelRadius * wheelRadius), subStepDeltaTime);
            float lateral = CapForce(-curveForce.y, lateralVelocity, lateralReferenceMass, stepDeltaTime);

            float longitudinalLimit = tire.Longitudinal.d * gripLoad;
            float lateralLimit = tire.Lateral.d * gripLoad;
            float longitudinalShare = longitudinalLimit > 0f ? longitudinal / longitudinalLimit : 0f;
            float lateralShare = lateralLimit > 0f ? lateral / lateralLimit : 0f;

            return new TireForces
            {
                Longitudinal = longitudinal,
                Lateral = lateral,
                SlipRatio = slipRatio,
                SlipAngle = slipAngle,
                GripUsage = Mathf.Sqrt(longitudinalShare * longitudinalShare + lateralShare * lateralShare),
                LongitudinalCapped = Mathf.Abs(longitudinal) < Mathf.Abs(curveForce.x) - 1e-3f,
            };
        }

        /// <summary>
        /// One sub-step of a wheel's spin: drive torque against the ground's reaction to the tire
        /// force, then brake and rolling resistance as friction (see
        /// <see cref="ApplyFrictionTorque"/>) - so enough brake locks the wheel instead of
        /// reversing it.
        /// </summary>
        public static float IntegrateWheel(
            float angularVelocity,
            float driveTorque,
            float longitudinalForce,
            float wheelRadius,
            float wheelInertia,
            float frictionTorque,
            float deltaTime)
        {
            float free = angularVelocity + (driveTorque - longitudinalForce * wheelRadius) / wheelInertia * deltaTime;
            return ApplyFrictionTorque(free, frictionTorque, wheelInertia, deltaTime);
        }

        private const int PeakSearchSamples = 2000;
        private const int PeakRefineIterations = 40;
        private const float InverseGoldenRatio = 0.618034f;

        /// <summary>
        /// The Magic Formula itself, unscaled by load: returns a friction coefficient, so the
        /// caller multiplies by the (load-sensitive) normal force.
        /// </summary>
        public static float MagicFormula(float x, in PacejkaCurve curve)
        {
            float bx = curve.b * x;
            return curve.d * Mathf.Sin(curve.c * Mathf.Atan(bx - curve.e * (bx - Mathf.Atan(bx))));
        }

        /// <summary>
        /// Where the curve peaks within [0, searchLimit]. Combined slip is normalised by this, so
        /// it has to be the real peak of the curve as tuned, whatever E is - and E has no closed
        /// form, hence a coarse sampling followed by a golden-section refinement around the best
        /// sample. One path for every E keeps the E = 0 case from quietly disagreeing with the rest.
        ///
        /// A curve still rising at the range end (C &lt;= 1 never turns over at all) has no peak
        /// to normalise with: <paramref name="hasPeak"/> comes back false and the range end
        /// stands in, so the tire keeps working while the inspector warns.
        /// </summary>
        public static float PeakInput(in PacejkaCurve curve, float searchLimit, out bool hasPeak)
        {
            float step = searchLimit / PeakSearchSamples;
            int bestSample = 0;
            float bestValue = float.MinValue;

            for (int i = 1; i <= PeakSearchSamples; i++)
            {
                float value = MagicFormula(i * step, curve);
                if (value > bestValue)
                {
                    bestValue = value;
                    bestSample = i;
                }
            }

            if (bestSample == PeakSearchSamples)
            {
                hasPeak = false;
                return searchLimit;
            }

            float low = (bestSample - 1) * step;
            float high = (bestSample + 1) * step;
            for (int i = 0; i < PeakRefineIterations; i++)
            {
                float lowerProbe = high - InverseGoldenRatio * (high - low);
                float upperProbe = low + InverseGoldenRatio * (high - low);
                if (MagicFormula(lowerProbe, curve) < MagicFormula(upperProbe, curve))
                {
                    low = lowerProbe;
                }
                else
                {
                    high = upperProbe;
                }
            }

            hasPeak = true;
            return 0.5f * (low + high);
        }

        /// <summary>
        /// Floor for <see cref="LoadFactor"/>. The linear falloff would otherwise go negative on a
        /// hard landing and turn grip into anti-grip.
        /// </summary>
        public const float MinLoadFactor = 0.5f;

        /// <summary>
        /// Load sensitivity: the friction coefficient falls as load rises, so twice the load
        /// brings less than twice the grip. Multiplies D. Exactly 1 at <paramref name="nominalLoad"/>.
        /// </summary>
        public static float LoadFactor(float normalForce, float nominalLoad, float sensitivity)
        {
            if (nominalLoad <= 0f)
            {
                return 1f;
            }

            return Mathf.Max(MinLoadFactor, 1f - sensitivity * (normalForce / nominalLoad - 1f));
        }

        /// <summary>
        /// Longitudinal slip ratio: 0 free rolling, positive when the tire surface runs ahead of
        /// the ground (driving), negative behind it (braking). Its sign follows the slip velocity
        /// rather than the direction of travel, so the resulting force opposes the slip in
        /// reverse as well. <paramref name="minSpeed"/> only keeps the division finite at
        /// standstill - stability is the job of <see cref="CapForce"/>.
        /// </summary>
        public static float SlipRatio(float wheelSurfaceSpeed, float forwardVelocity, float minSpeed)
        {
            return (wheelSurfaceSpeed - forwardVelocity) / Mathf.Max(Mathf.Abs(forwardVelocity), minSpeed);
        }

        /// <summary>
        /// Slip angle in degrees, signed like <paramref name="lateralVelocity"/>; the tire force
        /// opposes it. Measured against the rolling speed, not the signed forward velocity, so the
        /// force does not flip when reversing.
        /// </summary>
        public static float SlipAngleDegrees(float lateralVelocity, float forwardVelocity, float minSpeed)
        {
            return Mathf.Atan2(lateralVelocity, Mathf.Max(Mathf.Abs(forwardVelocity), minSpeed)) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// Longitudinal (x) and lateral (y) tire force for a slip ratio and slip angle acting at
        /// the same time, N. Both come back signed like their slip; the caller turns the lateral
        /// one against the slip.
        ///
        /// Normalised slip vector: each slip is expressed as a fraction of its own curve's peak,
        /// the two fractions combine into one length rho, and each curve is evaluated at that
        /// common rho and projected back onto its own axis. So cornering and driving draw on one
        /// budget - a tire at its peak slip angle has little left for traction - while each
        /// direction keeps its own curve shape and peak. Where the two D differ the envelope is an
        /// ellipse rather than a circle.
        ///
        /// <paramref name="gripLoad"/> is the normal force already scaled by
        /// <see cref="LoadFactor"/>.
        /// </summary>
        public static Vector2 CombinedForce(
            float slipRatio,
            float slipAngleDegrees,
            in PacejkaCurve longitudinal,
            in PacejkaCurve lateral,
            float peakSlipRatio,
            float peakSlipAngleDegrees,
            float gripLoad)
        {
            float s = slipRatio / peakSlipRatio;
            float a = slipAngleDegrees / peakSlipAngleDegrees;
            float rho = Mathf.Sqrt(s * s + a * a);

            if (rho < 1e-6f)
            {
                return Vector2.zero;
            }

            float longitudinalForce = gripLoad * MagicFormula(rho * peakSlipRatio, longitudinal) * s / rho;
            float lateralForce = gripLoad * MagicFormula(rho * peakSlipAngleDegrees, lateral) * a / rho;
            return new Vector2(longitudinalForce, lateralForce);
        }

        /// <summary>
        /// Largest share of its slip velocity a tire force may remove in one integration step.
        /// Below 1 an explicit step cannot overshoot, so the force cannot flip sign every step;
        /// 0.4 rather than anything near 1 because the lateral force couples into yaw through
        /// EffectiveMassAt and chatters from roughly 0.5 upwards (this used to be tireGripFactor,
        /// tuned there for that reason).
        /// </summary>
        public const float StabilityFraction = 0.4f;

        /// <summary>
        /// Clamps a tire force so that one step of length <paramref name="deltaTime"/> removes at
        /// most <see cref="StabilityFraction"/> of <paramref name="slipVelocity"/> from a body of
        /// <paramref name="referenceMass"/>.
        ///
        /// This replaces a low-speed blend band. At speed a Pacejka force is far softer than this
        /// cap and passes through untouched; near standstill, where the same slip angle or slip
        /// ratio stands for a tiny slip velocity and the curve would ask for an enormous
        /// correction, the cap takes over - and the capped force is exactly the old "cancel a
        /// fraction of the slip per step" term. The hand-over is continuous, with nothing to tune.
        /// </summary>
        public static float CapForce(float force, float slipVelocity, float referenceMass, float deltaTime)
        {
            float cap = StabilityFraction * referenceMass * Mathf.Abs(slipVelocity) / deltaTime;
            return Mathf.Clamp(force, -cap, cap);
        }

        /// <summary>
        /// Brake and rolling resistance acting on the wheel's spin: friction pulls the angular
        /// velocity towards zero and can stop the wheel within the step, but never throws it
        /// backwards. A plain signed torque does exactly that across zero, and a locked wheel
        /// would then chatter between forwards and backwards every step.
        /// </summary>
        public static float ApplyFrictionTorque(float angularVelocity, float frictionTorque, float inertia, float deltaTime)
        {
            float maxChange = frictionTorque / inertia * deltaTime;
            if (Mathf.Abs(angularVelocity) <= maxChange)
            {
                return 0f;
            }

            return angularVelocity - Mathf.Sign(angularVelocity) * maxChange;
        }
    }
}
