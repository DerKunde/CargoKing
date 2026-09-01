using UnityEditor;
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
        /// The tangents live in that frame. Reading the road backwards swaps which of the two leads
        /// in, and each of them has to be re-expressed in the frame the knot now has.
        /// </summary>
        private static BezierKnot Flip(BezierKnot knot)
        {
            quaternion rotation = math.mul(knot.Rotation, quaternion.RotateY(math.PI));

            // Re-expressed in the turned frame, not merely negated. Turning half a circle about the
            // knot's up axis maps a stored tangent to (-x, +y, -z); negating all three would also
            // flip the vertical component and make a merged sloped road dip where it should rise.
            float3 tangentIn = new float3(-knot.TangentOut.x, knot.TangentOut.y, -knot.TangentOut.z);
            float3 tangentOut = new float3(-knot.TangentIn.x, knot.TangentIn.y, -knot.TangentIn.z);

            return new BezierKnot(knot.Position, tangentIn, tangentOut, rotation);
        }

        /// <summary>How close two ends have to be before they count as the same point, in metres.</summary>
        private const float SeamEpsilon = 0.01f;

        private static Vector3 ToVector(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }

        /// <summary>
        /// Joins two streets into one. The target survives, the dragged one disappears into it.
        /// </summary>
        /// <returns>The surviving segment, or null when the merge was refused.</returns>
        public static StreetSegment Merge(
            StreetSegment dragged,
            StreetEnd draggedEnd,
            StreetSegment target,
            StreetEnd targetEnd)
        {
            if (!CanMerge(dragged, target, out string problem))
            {
                Debug.LogWarning(problem, target);
                return null;
            }

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();

            SplineContainer targetContainer = target.GetComponent<SplineContainer>();
            SplineContainer draggedContainer = dragged.GetComponent<SplineContainer>();

            Undo.RecordObject(target, "Merge Streets");
            Undo.RecordObject(targetContainer, "Merge Streets");
            Undo.RecordObject(dragged, "Merge Streets");
            Undo.RecordObject(draggedContainer, "Merge Streets");

            // Reduced to one case instead of four: the target's end meets the dragged one's start.
            if (targetEnd == StreetEnd.Start)
            {
                Reverse(target);
            }

            if (draggedEnd == StreetEnd.End)
            {
                Reverse(dragged);
            }

            Spline targetSpline = SplineOf(target);
            Spline draggedSpline = SplineOf(dragged);

            int seam = targetSpline.Count - 1;

            // Two ends lying on each other are one knot; two that do not are a stretch of road that has
            // to stay. Taking a junction back out merges halves standing where its sockets were, and
            // welding those would swallow exactly the piece the junction had been standing on.
            Vector3 targetSeam = target.transform.TransformPoint(ToVector(targetSpline[seam].Position));
            Vector3 draggedSeam = dragged.transform.TransformPoint(ToVector(draggedSpline[0].Position));
            bool weld = Vector3.Distance(targetSeam, draggedSeam) < SeamEpsilon;

            // BezierKnot.Transform carries position, rotation and tangents across. The tangents sit in
            // the knot's own rotation space, and it rotates them along - writing that conversion here
            // by hand would only be a second, worse version of it.
            float4x4 matrix = math.mul(
                target.transform.worldToLocalMatrix,
                dragged.transform.localToWorldMatrix);

            for (int index = weld ? 1 : 0; index < draggedSpline.Count; index++)
            {
                targetSpline.Add(draggedSpline[index].Transform(matrix), draggedSpline.GetTangentMode(index));
            }

            if (weld)
            {
                // The one knot standing for both ends has to carry its tangents through, or the road
                // would kink where the two used to meet. Across a gap there is nothing to smooth.
                targetSpline.SetTangentMode(seam, TangentMode.Continuous);
            }

            // The outer end of the dragged street becomes the outer end of the joined one.
            target.endConnection = dragged.endConnection;

            // Anything hung on the dragged street - a sign, a lamp - would be destroyed with it.
            Transform draggedTransform = dragged.transform;
            for (int index = draggedTransform.childCount - 1; index >= 0; index--)
            {
                Undo.SetTransformParent(draggedTransform.GetChild(index), target.transform, "Merge Streets");
            }

            Undo.DestroyObjectImmediate(dragged.gameObject);

            EditorUtility.SetDirty(target);
            target.Rebuild();

            Undo.CollapseUndoOperations(group);
            Undo.SetCurrentGroupName("Merge Streets");

            return target;
        }

        /// <summary>
        /// Whether a street can be cut at this knot. Only the inner knots qualify - cutting at an end
        /// would produce a half with no length.
        /// </summary>
        public static bool CanSplit(StreetSegment segment, int knotIndex, out string problem)
        {
            problem = null;

            Spline spline = SplineOf(segment);
            if (spline == null || spline.Count < 3)
            {
                problem = "A street needs at least three knots before it can be cut.";
                return false;
            }

            if (knotIndex < 1 || knotIndex > spline.Count - 2)
            {
                problem = "A street can only be cut at one of its inner knots.";
                return false;
            }

            if (!IsUnscaled(segment.transform))
            {
                problem = "The street needs a scale of 1. The second half is created at scale 1 and "
                    + "the knots are copied into it verbatim, so a scaled street would change size.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Cuts a street in two at one of its knots. The knot itself is duplicated, so each half keeps
        /// an end there. Both new ends are open.
        /// </summary>
        /// <returns>The second half, or null when the cut was refused.</returns>
        public static StreetSegment Split(StreetSegment segment, int knotIndex)
        {
            if (!CanSplit(segment, knotIndex, out string problem))
            {
                Debug.LogWarning(problem, segment);
                return null;
            }

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();

            Spline spline = SplineOf(segment);
            SplineContainer container = segment.GetComponent<SplineContainer>();

            Undo.RecordObject(segment, "Split Street");
            Undo.RecordObject(container, "Split Street");

            GameObject secondObject = new GameObject($"{segment.name} (2)");
            Undo.RegisterCreatedObjectUndo(secondObject, "Split Street");

            secondObject.transform.SetParent(segment.transform.parent, false);

            // Placed exactly on the original, so the knots carry over without any conversion at all.
            secondObject.transform.SetPositionAndRotation(
                segment.transform.position, segment.transform.rotation);

            SplineContainer secondContainer = secondObject.AddComponent<SplineContainer>();
            Spline secondSpline = secondContainer.Spline;
            secondSpline.Clear();

            for (int index = knotIndex; index < spline.Count; index++)
            {
                secondSpline.Add(spline[index], spline.GetTangentMode(index));
            }

            StreetSegment second = secondObject.AddComponent<StreetSegment>();
            second.roadWidth = segment.roadWidth;
            second.sourceMesh = segment.sourceMesh;
            second.forwardAxis = segment.forwardAxis;
            second.tileLength = segment.tileLength;
            second.generateCollider = segment.generateCollider;
            second.curvatureWarningRadius = segment.curvatureWarningRadius;

            MeshRenderer source = segment.GetComponent<MeshRenderer>();
            MeshRenderer destination = second.GetComponent<MeshRenderer>();
            if (source != null && destination != null)
            {
                destination.sharedMaterials = source.sharedMaterials;
            }

            // The far end of the road is now the far end of the second half.
            second.endConnection = segment.endConnection;
            second.startConnection = new StreetEndConnector();
            segment.endConnection = new StreetEndConnector();

            for (int index = spline.Count - 1; index > knotIndex; index--)
            {
                spline.RemoveAt(index);
            }

            EditorUtility.SetDirty(segment);
            segment.Rebuild();
            second.Rebuild();

            Undo.CollapseUndoOperations(group);
            Undo.SetCurrentGroupName("Split Street");

            return second;
        }
    }
}
