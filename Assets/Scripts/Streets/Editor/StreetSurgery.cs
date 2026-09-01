using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

namespace CargoKing.Streets.Editor
{
    /// <summary>
    /// Spline and connector surgery on street segments: joining two into one, turning one around,
    /// cutting one in half.
    ///
    /// Knows nothing about intersections. Inserting a junction is this surgery with a prefab put in
    /// the gap, and keeping the two apart is what makes the surgery testable on its own.
    /// </summary>
    public static class StreetSurgery
    {
        /// <summary>How far a transform's scale may stray from 1 before it is refused.</summary>
        private const float ScaleEpsilon = 0.0001f;

        /// <summary>The spline of a segment. StreetSegment keeps its own reference private.</summary>
        public static Spline SplineOf(StreetSegment segment)
        {
            SplineContainer container = segment != null ? segment.GetComponent<SplineContainer>() : null;
            return container != null ? container.Spline : null;
        }

        /// <summary>
        /// Whether two segments may become one, and why not when they may not.
        ///
        /// Everything that says how the road is built has to agree. Quietly taking one of two road
        /// widths would change geometry nobody asked to have changed.
        /// </summary>
        public static bool CanMerge(StreetSegment dragged, StreetSegment target, out string problem)
        {
            problem = null;

            if (dragged == null || target == null)
            {
                problem = "One of the two streets is gone.";
                return false;
            }

            if (dragged == target)
            {
                problem = "A street cannot be merged with itself.";
                return false;
            }

            Spline draggedSpline = SplineOf(dragged);
            Spline targetSpline = SplineOf(target);

            if (draggedSpline == null || targetSpline == null
                || draggedSpline.Count < 2 || targetSpline.Count < 2)
            {
                problem = "Both streets need a spline with at least two knots.";
                return false;
            }

            if (!Mathf.Approximately(dragged.roadWidth, target.roadWidth))
            {
                problem = $"'{dragged.name}' is {dragged.roadWidth:0.0} m wide and '{target.name}' is "
                    + $"{target.roadWidth:0.0} m. Streets of different width cannot become one.";
                return false;
            }

            if (dragged.sourceMesh != target.sourceMesh
                || dragged.forwardAxis != target.forwardAxis
                || !Mathf.Approximately(dragged.tileLength, target.tileLength))
            {
                problem = $"'{dragged.name}' and '{target.name}' are built from different tiles.";
                return false;
            }

            if (!IsUnscaled(dragged.transform) || !IsUnscaled(target.transform))
            {
                problem = "Both streets need a scale of 1. A scaled transform would distort the "
                    + "tangent lengths when the knots are converted.";
                return false;
            }

            return true;
        }

        private static bool IsUnscaled(Transform transform)
        {
            Vector3 scale = transform.lossyScale;

            return Mathf.Abs(scale.x - 1f) < ScaleEpsilon
                && Mathf.Abs(scale.y - 1f) < ScaleEpsilon
                && Mathf.Abs(scale.z - 1f) < ScaleEpsilon;
        }

        /// <summary>
        /// Turns a street around: the last knot becomes the first, and the road runs the other way.
        ///
        /// Needed because two of the four ways two streets can meet require one side to be read
        /// backwards, and in one of them that side is the survivor itself.
        /// </summary>
        public static void Reverse(StreetSegment segment)
        {
            Spline spline = SplineOf(segment);
            if (spline == null || spline.Count < 2)
            {
                return;
            }

            int count = spline.Count;
            BezierKnot[] knots = new BezierKnot[count];
            TangentMode[] modes = new TangentMode[count];

            for (int index = 0; index < count; index++)
            {
                knots[index] = spline[index];
                modes[index] = spline.GetTangentMode(index);
            }

            spline.Clear();
            for (int index = count - 1; index >= 0; index--)
            {
                spline.Add(Flip(knots[index]), modes[index]);
            }

            // The connectors describe ends, and the ends have just changed places.
            StreetEndConnector start = segment.startConnection;
            segment.startConnection = segment.endConnection;
            segment.endConnection = start;
        }

        /// <summary>
        /// One knot, turned around. Its frame is spun half a turn about its own up axis so that its
        /// forward points the new way while up stays where it was - a road that is read backwards must
        /// not end up upside down.
        ///
        /// The tangents live in that frame. Spinning the frame flips their sign, and reading the road
        /// backwards swaps which of the two leads in, so they are exchanged and negated together.
        /// </summary>
        private static BezierKnot Flip(BezierKnot knot)
        {
            quaternion rotation = math.mul(knot.Rotation, quaternion.RotateY(math.PI));
            return new BezierKnot(knot.Position, -knot.TangentOut, -knot.TangentIn, rotation);
        }
    }
}
