using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace CargoKing.Streets
{
    /// <summary>
    /// Derives the driveable lanes of a street segment from its spline.
    ///
    /// Static and free of any Unity lifecycle, like the mesh builder, and for the same reason: the
    /// AI drives on what comes out of here, so it has to be checkable without a scene.
    /// </summary>
    public static class StreetLaneBuilder
    {
        /// <summary>How far a straight chord may stray from the real curve, in metres.</summary>
        public const float DefaultTolerance = 0.05f;

        private const float MinimumLength = 0.001f;

        /// <summary>Closest two samples ever get, so a hairpin cannot produce thousands of points.</summary>
        private const float MinimumStep = 0.25f;

        /// <summary>Furthest two samples ever get, so even a dead straight road keeps some resolution.</summary>
        private const float MaximumStep = 20f;

        private const int MaximumSampleCount = 8192;

        private static readonly List<float> distances = new List<float>();

        /// <summary>
        /// Builds one lane in each direction. Right hand traffic: driving along the spline you keep
        /// to its right, so the forward lane is the one offset to the right.
        /// </summary>
        /// <param name="spline">Centre line of the segment.</param>
        /// <param name="roadWidth">Full width of the carriageway in metres.</param>
        /// <param name="tolerance">How far a chord may stray from the real curve, in metres.</param>
        /// <returns>Two lanes, or an empty array when the spline cannot carry any.</returns>
        public static StreetLane[] Build(ISpline spline, float roadWidth, float tolerance = DefaultTolerance)
        {
            if (spline == null || spline.Count < 2 || roadWidth < MinimumLength)
            {
                return System.Array.Empty<StreetLane>();
            }

            float length = spline.GetLength();
            if (length < MinimumLength)
            {
                return System.Array.Empty<StreetLane>();
            }

            SampleCentreLine(spline, length, Mathf.Max(tolerance, MinimumLength));

            // Two lanes, so each centre sits a quarter of the road's width off the middle.
            float offset = roadWidth * 0.25f;

            StreetLaneSample[] right = new StreetLaneSample[distances.Count];
            StreetLaneSample[] left = new StreetLaneSample[distances.Count];

            for (int index = 0; index < distances.Count; index++)
            {
                float t = Mathf.Clamp01(distances[index] / length);

                spline.Evaluate(t, out float3 position, out float3 tangent, out float3 up);
                Quaternion frame = StreetFrame.At(tangent, up);

                Vector3 centre = new Vector3(position.x, position.y, position.z);
                Vector3 forward = frame * Vector3.forward;
                Vector3 sideways = frame * Vector3.right;

                float centreRadius = RadiusAt(spline, t, out float turnSign);

                right[index] = new StreetLaneSample
                {
                    position = centre + sideways * offset,
                    direction = forward,

                    // Turning towards a side puts the centre of the bend there, which makes the lane
                    // on that side the inner and therefore the tighter one.
                    radius = ShiftRadius(centreRadius, -turnSign * offset),
                };

                left[index] = new StreetLaneSample
                {
                    position = centre - sideways * offset,
                    direction = -forward,
                    radius = ShiftRadius(centreRadius, turnSign * offset),
                };
            }

            System.Array.Reverse(left);

            // Measured after the offset, never carried over from the centre line: the inside of a bend
            // is shorter than the middle, and a route follower reading the wrong remaining distance
            // would brake in the wrong place.
            AccumulateDistances(right);
            AccumulateDistances(left);

            return new[]
            {
                new StreetLane(StreetLaneDirection.Forward, right),
                new StreetLane(StreetLaneDirection.Backward, left),
            };
        }

        /// <summary>
        /// Fills the distance buffer with sample positions along the centre line, spaced by curvature
        /// rather than evenly: a straight gets a handful of points, a bend as many as it needs.
        ///
        /// The step comes from the sagitta of a chord, s^2 * k / 8, solved for the step that keeps it
        /// within the tolerance. Curvature is read at the start of each step, so a bend that tightens
        /// very abruptly is sampled slightly too thinly - the minimum step bounds how far that goes.
        /// </summary>
        private static void SampleCentreLine(ISpline spline, float length, float tolerance)
        {
            distances.Clear();
            distances.Add(0f);

            float distance = 0f;
            while (distance < length && distances.Count < MaximumSampleCount)
            {
                float curvature = spline.EvaluateCurvature(Mathf.Clamp01(distance / length));

                float step = MaximumStep;
                if (!float.IsNaN(curvature) && !float.IsInfinity(curvature) && curvature > 0f)
                {
                    step = Mathf.Sqrt(8f * tolerance / curvature);
                }

                distance += Mathf.Clamp(step, MinimumStep, MaximumStep);
                if (distance >= length)
                {
                    break;
                }

                distances.Add(distance);
            }

            distances.Add(length);
        }

        /// <summary>
        /// Curve radius of the centre line at t, and which way it bends: +1 when the centre of the
        /// bend lies to the right of the direction of travel, -1 when it lies to the left.
        /// </summary>
        private static float RadiusAt(ISpline spline, float t, out float turnSign)
        {
            turnSign = 1f;

            float curvature = spline.EvaluateCurvature(t);
            if (float.IsNaN(curvature) || float.IsInfinity(curvature) || curvature <= 0f)
            {
                return float.PositiveInfinity;
            }

            float3 tangent = spline.EvaluateTangent(t);
            float3 acceleration = spline.EvaluateAcceleration(t);
            float3 up = spline.EvaluateUpVector(t);

            // Acceleration points towards the centre of the bend, so this asks which side it is on.
            turnSign = math.dot(math.cross(tangent, acceleration), up) >= 0f ? 1f : -1f;

            return 1f / curvature;
        }

        /// <summary>
        /// Moves a radius sideways by an offset. A positive offset moves away from the centre of the
        /// bend and widens the curve, a negative one tightens it.
        /// </summary>
        private static float ShiftRadius(float radius, float offset)
        {
            if (float.IsInfinity(radius))
            {
                return radius;
            }

            // A lane inside a bend tighter than its own offset has no meaningful radius left. The
            // curvature warning on the segment catches that case where the author can still act on it.
            return Mathf.Max(radius + offset, MinimumLength);
        }

        private static void AccumulateDistances(StreetLaneSample[] samples)
        {
            float distance = 0f;

            for (int index = 1; index < samples.Length; index++)
            {
                distance += Vector3.Distance(samples[index - 1].position, samples[index].position);
                samples[index].distance = distance;
            }
        }
    }
}
