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

            // The tangent of the curve leaving this knot, read at its own start. Not
            // EvaluateTangent(knotIndex / (Count - 1)): spline space is normalised by ARC LENGTH, so
            // that quotient only lands on the knot when every curve happens to be equally long. On a
            // real street it would read the tangent somewhere else entirely and aim the junction
            // askew. knotIndex is always an inner knot, so curve knotIndex always exists.
            float3 tangent = CurveUtility.EvaluateTangent(spline.GetCurve(knotIndex), 0f);

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

        /// <summary>
        /// The two streets docked to a junction, when there are exactly two.
        /// </summary>
        public static bool TryGetDockedPair(
            Intersection intersection,
            out StreetSegment first,
            out StreetEnd firstEnd,
            out StreetSegment second,
            out StreetEnd secondEnd)
        {
            first = null;
            second = null;
            firstEnd = StreetEnd.Start;
            secondEnd = StreetEnd.Start;

            StreetSegment[] segments = Object.FindObjectsByType<StreetSegment>(FindObjectsSortMode.InstanceID);

            for (int index = 0; index < segments.Length; index++)
            {
                StreetSegment segment = segments[index];

                for (int side = 0; side < 2; side++)
                {
                    StreetEnd end = side == 0 ? StreetEnd.Start : StreetEnd.End;
                    IntersectionSocket socket = segment.ConnectorAt(end).socket;

                    if (socket == null || socket.Owner != intersection)
                    {
                        continue;
                    }

                    if (first == null)
                    {
                        first = segment;
                        firstEnd = end;
                    }
                    else if (second == null)
                    {
                        second = segment;
                        secondEnd = end;
                    }
                    else
                    {
                        // Three or more streets: neither flipping nor removing has a defined meaning.
                        first = null;
                        second = null;
                        return false;
                    }
                }
            }

            return first != null && second != null;
        }

        /// <summary>
        /// Turns a junction half a turn about its up axis, so a T junction's stem changes sides.
        ///
        /// The two docked halves are driven by their sockets, so turning alone would drag each of them
        /// onto the other's place and the two would cross. Their socket references are exchanged along
        /// with the turn, which puts everything back where it was and moves only the stem.
        /// </summary>
        public static void Flip(Intersection intersection)
        {
            if (!TryGetDockedPair(intersection, out StreetSegment first, out StreetEnd firstEnd,
                out StreetSegment second, out StreetEnd secondEnd))
            {
                Debug.LogWarning(
                    "Flipping needs exactly two streets docked to this junction.", intersection);
                return;
            }

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();

            Undo.RecordObject(intersection.transform, "Flip Junction");
            Undo.RecordObject(first, "Flip Junction");
            Undo.RecordObject(second, "Flip Junction");

            intersection.transform.Rotate(intersection.transform.up, 180f, Space.World);

            IntersectionSocket firstSocket = first.ConnectorAt(firstEnd).socket;
            first.ConnectorAt(firstEnd).socket = second.ConnectorAt(secondEnd).socket;
            second.ConnectorAt(secondEnd).socket = firstSocket;

            intersection.Rebuild();
            first.Rebuild();
            second.Rebuild();

            Undo.CollapseUndoOperations(group);
            Undo.SetCurrentGroupName("Flip Junction");
        }

        /// <summary>
        /// Takes a junction out again and closes the road over it.
        /// </summary>
        /// <returns>The joined street, or null when it was refused.</returns>
        public static StreetSegment Remove(Intersection intersection)
        {
            if (!TryGetDockedPair(intersection, out StreetSegment first, out StreetEnd firstEnd,
                out StreetSegment second, out StreetEnd secondEnd))
            {
                Debug.LogWarning(
                    "Removing needs exactly two streets docked to this junction.", intersection);
                return null;
            }

            if (!StreetSurgery.CanMerge(second, first, out string problem))
            {
                Debug.LogWarning(problem, intersection);
                return null;
            }

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();

            StreetSnapping.Disconnect(first, firstEnd);
            StreetSnapping.Disconnect(second, secondEnd);

            Undo.DestroyObjectImmediate(intersection.gameObject);

            StreetSegment survivor = StreetSurgery.Merge(second, secondEnd, first, firstEnd);

            Undo.CollapseUndoOperations(group);
            Undo.SetCurrentGroupName("Remove Junction");

            return survivor;
        }
    }
}
