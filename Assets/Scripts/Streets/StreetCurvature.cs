using UnityEngine;
using UnityEngine.Splines;

namespace CargoKing.Streets
{
    /// <summary>
    /// Measures how sharply a spline bends.
    ///
    /// The same number serves twice over the life of a street: while authoring it warns that a curve
    /// has become too tight for a swept road mesh, and later it gives the AI driver its cornering
    /// speed. It lives apart from the mesh builder so the second use does not drag geometry along.
    /// </summary>
    public static class StreetCurvature
    {
        /// <summary>Shortest spline worth scanning.</summary>
        private const float MinimumLength = 0.001f;

        private const int MinimumSampleCount = 8;
        private const int MaximumSampleCount = 4096;

        /// <summary>
        /// How densely a spline is scanned. Two per metre finds a curve long before it gets tight
        /// enough to matter, without walking thousands of points along a kilometre of road.
        /// </summary>
        private const float SamplesPerMeter = 2f;

        /// <summary>
        /// Finds the smallest curve radius along a spline.
        /// </summary>
        /// <param name="spline">The spline to scan.</param>
        /// <param name="tightestT">Normalized position of the sharpest point that was found.</param>
        /// <returns>
        /// The radius in metres, or <see cref="float.PositiveInfinity"/> when the spline is straight.
        /// </returns>
        public static float MinimumRadius(ISpline spline, out float tightestT)
        {
            tightestT = 0f;

            if (spline == null || spline.Count < 2)
            {
                return float.PositiveInfinity;
            }

            float length = spline.GetLength();
            if (length < MinimumLength)
            {
                return float.PositiveInfinity;
            }

            // Unity's normalized t is arc length parameterised, so evenly spaced t values are also
            // evenly spaced along the road and no stretch of it gets scanned more thinly than another.
            int sampleCount = Mathf.Clamp(
                Mathf.CeilToInt(length * SamplesPerMeter),
                MinimumSampleCount,
                MaximumSampleCount);

            float sharpest = 0f;

            for (int sample = 0; sample < sampleCount; sample++)
            {
                float t = sample / (float)(sampleCount - 1);
                float curvature = spline.EvaluateCurvature(t);

                // A degenerate curve - two knots on top of each other - divides by a zero tangent.
                if (float.IsNaN(curvature) || float.IsInfinity(curvature))
                {
                    continue;
                }

                if (curvature > sharpest)
                {
                    sharpest = curvature;
                    tightestT = t;
                }
            }

            // Curvature is 1/radius, so a straight spline reports zero and simply has no finite radius.
            return sharpest > 0f ? 1f / sharpest : float.PositiveInfinity;
        }
    }
}
