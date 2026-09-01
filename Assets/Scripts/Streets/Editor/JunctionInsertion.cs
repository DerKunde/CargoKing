using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace CargoKing.Streets.Editor
{
    /// <summary>
    /// Replaces a knot on a street with a junction: cut the spline there, put the prefab in the gap,
    /// dock both halves to it.
    ///
    /// The halves then retreat onto their sockets by themselves, because a docked end is driven by
    /// what it docks to. That is what makes the junction eat exactly the stretch of road it stands on
    /// without a line of code to move anything.
    /// </summary>
    public static class JunctionInsertion
    {
        /// <summary>Where a knot sits and which way the road runs there, in world space.</summary>
        private struct KnotPose
        {
            public Vector3 position;
            public Vector3 direction;
            public Vector3 up;
        }

        /// <summary>
        /// The pose at one knot. Both the check and the insertion need it, and they have to agree -
        /// a check that measures a different place than the one that gets built is worse than none.
        /// </summary>
        private static KnotPose PoseAt(StreetSegment segment, int knotIndex)
        {
            Spline spline = StreetSurgery.SplineOf(segment);
            Transform transform = segment.transform;

            float3 position = spline[knotIndex].Position;

            // Spline space runs one unit per curve, so a knot index divided by the number of curves
            // lands exactly on that knot.
            float3 tangent = spline.EvaluateTangent((float)knotIndex / (spline.Count - 1));

            Vector3 direction = transform.TransformDirection(
                new Vector3(tangent.x, tangent.y, tangent.z));

            return new KnotPose
            {
                position = transform.TransformPoint(new Vector3(position.x, position.y, position.z)),
                direction = direction.sqrMagnitude > 0.000001f ? direction.normalized : transform.forward,
                up = transform.up,
            };
        }

        /// <summary>
        /// Whether a junction fits at this knot, and what is in the way when it does not.
        /// </summary>
        public static bool CanInsert(
            StreetSegment segment,
            int knotIndex,
            GameObject junction,
            out string problem)
        {
            if (!StreetSurgery.CanSplit(segment, knotIndex, out problem))
            {
                return false;
            }

            Spline spline = StreetSurgery.SplineOf(segment);
            KnotPose pose = PoseAt(segment, knotIndex);

            if (!JunctionPlacement.TryAlign(
                junction,
                pose.position,
                pose.direction,
                pose.up,
                false,
                out JunctionAlignment alignment,
                out problem))
            {
                return false;
            }

            if (alignment.entry != null
                && !Mathf.Approximately(alignment.entry.roadWidth, segment.roadWidth))
            {
                problem = $"The junction's sockets are {alignment.entry.roadWidth:0.0} m wide and this "
                    + $"street is {segment.roadWidth:0.0} m. The lanes would not line up.";
                return false;
            }

            // Both halves have to be longer than the stretch the junction takes for itself, or the
            // half would be pulled past its own far end and turn inside out.
            float before = LengthBetween(spline, 0, knotIndex);
            float after = LengthBetween(spline, knotIndex, spline.Count - 1);

            if (before <= alignment.socketOffset || after <= alignment.socketOffset)
            {
                problem = $"The junction needs more than {alignment.socketOffset:0.0} m of street on "
                    + $"each side of the knot; there are {before:0.0} m and {after:0.0} m.";
                return false;
            }

            return true;
        }

        /// <summary>Arc length of the spline between two knots, in metres.</summary>
        private static float LengthBetween(Spline spline, int fromKnot, int toKnot)
        {
            float length = 0f;

            for (int index = fromKnot; index < toKnot; index++)
            {
                length += spline.GetCurveLength(index);
            }

            return length;
        }

        /// <summary>
        /// Puts a junction where a knot was.
        /// </summary>
        /// <returns>The junction that was placed, or null when it was refused.</returns>
        public static Intersection Insert(StreetSegment segment, int knotIndex, GameObject junction)
        {
            if (!CanInsert(segment, knotIndex, junction, out string problem))
            {
                Debug.LogWarning(problem, segment);
                return null;
            }

            // Read before the split, because the split is what takes the knot apart.
            KnotPose pose = PoseAt(segment, knotIndex);

            if (!JunctionPlacement.TryAlign(
                junction, pose.position, pose.direction, pose.up, false,
                out JunctionAlignment alignment, out problem))
            {
                Debug.LogWarning(problem, segment);
                return null;
            }

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();

            StreetSegment second = StreetSurgery.Split(segment, knotIndex);
            if (second == null)
            {
                return null;
            }

            GameObject instance = PrefabUtility.IsPartOfPrefabAsset(junction)
                ? (GameObject)PrefabUtility.InstantiatePrefab(junction)
                : Object.Instantiate(junction);

            Undo.RegisterCreatedObjectUndo(instance, "Insert Junction");

            // A sibling of the road, never a child: from the third arm on it belongs to a third street
            // as well.
            instance.transform.SetParent(segment.transform.parent, false);
            instance.transform.SetPositionAndRotation(alignment.position, alignment.rotation);

            Intersection intersection = instance.GetComponent<Intersection>();
            intersection.Rebuild();

            // The alignment named sockets on the prefab; the docking has to use the ones on the copy.
            IntersectionSocket entry = FindSame(junction, instance, alignment.entry);
            IntersectionSocket exit = FindSame(junction, instance, alignment.exit);

            if (entry == null || exit == null)
            {
                // Undoing the whole group is the only honest way out: the road is already cut and the
                // junction already placed, and half a junction is worse than none.
                Debug.LogWarning(
                    $"The sockets of '{junction.name}' could not be found on the copy in the scene.",
                    segment);

                Undo.RevertAllDownToGroup(group);
                return null;
            }

            StreetSnapping.Connect(
                segment, StreetEnd.End, new StreetSnapTarget { socket = entry, position = entry.transform.position });
            StreetSnapping.Connect(
                second, StreetEnd.Start, new StreetSnapTarget { socket = exit, position = exit.transform.position });

            Selection.activeGameObject = instance;

            Undo.CollapseUndoOperations(group);
            Undo.SetCurrentGroupName("Insert Junction");

            return intersection;
        }

        /// <summary>
        /// The socket on the copy that matches one on the prefab, found by the path down the hierarchy
        /// rather than by name - two arms of a junction are often called the same thing.
        /// </summary>
        private static IntersectionSocket FindSame(GameObject prefab, GameObject instance, IntersectionSocket socket)
        {
            string path = AnimationUtility.CalculateTransformPath(socket.transform, prefab.transform);
            Transform found = instance.transform.Find(path);

            return found != null ? found.GetComponent<IntersectionSocket>() : null;
        }
    }
}
