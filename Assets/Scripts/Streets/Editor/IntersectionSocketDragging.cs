using Unity.Mathematics;
using UnityEditor;
using UnityEditor.Splines;
using UnityEngine;
using UnityEngine.Splines;

namespace CargoKing.Streets.Editor
{
    /// <summary>
    /// Pulls a new street out of a free intersection socket.
    ///
    /// The socket already says where a road leaves and how wide it is, so the gesture only has to
    /// supply the far end. The tile comes from a street that already exists, because a network built
    /// from one kit should not need the same three fields typed in again for every arm.
    ///
    /// The drag only makes the first stretch. Shaping the rest of the road is Unity's job, so the
    /// gesture ends by handing over to its draw tool rather than growing a second one here.
    ///
    /// Occupied sockets get no handle at all. Docking a second street to a socket is an error that
    /// <see cref="StreetSnapping.Validate"/> reports after the fact; not offering the gesture there
    /// keeps it from being made in the first place.
    /// </summary>
    public static class IntersectionSocketDragging
    {
        private static readonly Color HandleColor = new Color(1f, 0.85f, 0.3f);
        private static readonly Color TargetColor = new Color(0.35f, 1f, 0.45f);
        private static readonly Color TooShortColor = new Color(1f, 0.35f, 0.35f);

        /// <summary>Undo label for the whole gesture, so one Ctrl+Z takes the street back.</summary>
        private const string UndoName = "Draw Street";

        /// <summary>How far in front of the socket the grab handle sits, as a share of the road width.</summary>
        private const float HandleDistancePerWidth = 0.35f;

        /// <summary>Shorter than this a drag counts as a slip rather than as a street, in metres.</summary>
        private const float MinimumLength = 2f;

        /// <summary>How far the floor is looked for, in metres.</summary>
        private const float RayLength = 5000f;

        /// <summary>Layer the drivable ground sits on, the same one the AI target is picked from.</summary>
        private const string FloorLayer = "Floor";

        private static IntersectionSocket draggedSocket;
        private static Vector3 dragPosition;

        /// <summary>
        /// Draws a pull handle on every free socket of an intersection and carries out the drag.
        /// Call from <c>OnSceneGUI</c>.
        /// </summary>
        public static void Draw(Intersection intersection)
        {
            if (intersection == null || !StreetDrawing.Enabled)
            {
                draggedSocket = null;
                return;
            }

            // Gathered once for the whole intersection rather than per socket: the inspector repaints
            // constantly, and this is the expensive part of that frame.
            StreetSegment[] segments = Object.FindObjectsByType<StreetSegment>(FindObjectsSortMode.InstanceID);

            for (int index = 0; index < intersection.Sockets.Count; index++)
            {
                IntersectionSocket socket = intersection.Sockets[index];
                if (socket == null || StreetSnapping.FindSegmentAt(socket, null, segments) != null)
                {
                    continue;
                }

                DrawSocket(socket, segments);
            }
        }

        private static void DrawSocket(IntersectionSocket socket, StreetSegment[] segments)
        {
            Transform transform = socket.transform;
            Vector3 origin = transform.position;
            Vector3 handlePosition = origin + socket.Outward * (socket.roadWidth * HandleDistancePerWidth);
            float size = HandleUtility.GetHandleSize(handlePosition) * 0.12f;

            Handles.color = HandleColor;

            EditorGUI.BeginChangeCheck();
            Handles.FreeMoveHandle(handlePosition, size, Vector3.zero, Handles.SphereHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                // The handle's own result moves in the screen plane, which would send the street into
                // the sky. Only the fact that a drag is running is taken from it; where it points comes
                // from the cursor.
                draggedSocket = socket;
                dragPosition = PointUnderCursor(socket);
            }

            if (draggedSocket != socket)
            {
                return;
            }

            Vector3 end = dragPosition;
            float length = Vector3.Distance(origin, end);
            StreetSnapTarget candidate = length >= MinimumLength ? FindFarEnd(socket, end) : default;

            DrawPreview(socket, end, length, candidate);

            // Committed on release rather than while dragging, so passing over a socket on the way
            // somewhere else leaves nothing behind.
            if (Event.current.type == EventType.MouseUp)
            {
                draggedSocket = null;

                if (length >= MinimumLength)
                {
                    CreateStreet(socket, end, candidate, segments);
                }
            }
        }

        /// <summary>
        /// Where the cursor points, at the height a road leaving this socket runs at.
        /// </summary>
        private static Vector3 PointUnderCursor(IntersectionSocket socket)
        {
            Transform transform = socket.transform;
            Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);

            int floor = LayerMask.GetMask(FloorLayer);
            if (floor != 0 && Physics.Raycast(ray, out RaycastHit hit, RayLength, floor))
            {
                // Lifted back to the socket's own height above the floor. The socket is a spline
                // anchor, not a mark on the tarmac, so dropping the far end onto the floor itself
                // would tilt every new road by that offset and put a step in the seam.
                return hit.point + transform.up * HeightAboveFloor(socket, floor);
            }

            // No floor under the cursor: stay in the socket's own plane.
            Plane plane = new Plane(transform.up, transform.position);
            return plane.Raycast(ray, out float distance) ? ray.GetPoint(distance) : transform.position;
        }

        private static float HeightAboveFloor(IntersectionSocket socket, int floor)
        {
            Transform transform = socket.transform;
            Ray down = new Ray(transform.position + transform.up * RayLength, -transform.up);

            return Physics.Raycast(down, out RaycastHit hit, RayLength * 2f, floor)
                ? Vector3.Distance(transform.position, hit.point)
                : 0f;
        }

        /// <summary>
        /// What the far end of the new street would dock to, ignoring the socket it starts from.
        /// </summary>
        private static StreetSnapTarget FindFarEnd(IntersectionSocket socket, Vector3 end)
        {
            StreetSnapTarget target = StreetSnapping.FindNearest(end, null, StreetSnapping.SnapRadius);
            return target.socket == socket ? default : target;
        }

        private static void DrawPreview(
            IntersectionSocket socket,
            Vector3 end,
            float length,
            StreetSnapTarget candidate)
        {
            Vector3 origin = socket.transform.position;

            if (length < MinimumLength)
            {
                Handles.color = TooShortColor;
                Handles.DrawDottedLine(origin, end, 3f);
                Handles.Label(end, "too short");
                return;
            }

            Vector3 along = (end - origin) / length;
            Vector3 side = Vector3.Cross(socket.transform.up, along).normalized * (socket.roadWidth * 0.5f);

            // A straight band, not the curve the spline will actually take. The knot at the socket
            // leaves along its outward direction, so a road dragged sideways bends - drawing that here
            // would mean evaluating a spline that does not exist yet, for a preview the real mesh
            // replaces a moment later.
            Handles.color = candidate.IsValid ? TargetColor : HandleColor;
            Handles.DrawAAPolyLine(3f, origin - side, end - side);
            Handles.DrawAAPolyLine(3f, origin + side, end + side);
            Handles.DrawDottedLine(origin, end, 3f);
            Handles.Label(end, candidate.IsValid ? $"{length:0.0} m to {candidate.Label}" : $"{length:0.0} m");

            if (candidate.IsValid)
            {
                Handles.DrawWireDisc(
                    candidate.position,
                    socket.transform.up,
                    HandleUtility.GetHandleSize(candidate.position) * 0.4f);
            }
        }

        private static void CreateStreet(
            IntersectionSocket socket,
            Vector3 end,
            StreetSnapTarget farEnd,
            StreetSegment[] segments)
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();

            Intersection intersection = socket.Owner;
            GameObject street = new GameObject("Street");
            Undo.RegisterCreatedObjectUndo(street, UndoName);

            // Beside the intersection, never inside it: an intersection is a prefab instance, and a
            // street parented into one would be recorded as an override of that prefab.
            if (intersection != null && intersection.transform.parent != null)
            {
                street.transform.SetParent(intersection.transform.parent, false);
            }

            street.transform.SetPositionAndRotation(socket.transform.position, socket.transform.rotation);

            BuildSpline(Undo.AddComponent<SplineContainer>(street), street.transform, end);

            StreetSegment segment = Undo.AddComponent<StreetSegment>(street);
            segment.roadWidth = socket.roadWidth;
            CopyTile(FindTemplate(socket, segments), segment);

            StreetSnapping.Connect(
                segment,
                StreetEnd.Start,
                new StreetSnapTarget { socket = socket, position = socket.transform.position });

            // Docking the far end to another street merges the two, and the object that survives is
            // the other one. Everything after this point has to talk about the survivor.
            StreetSegment survivor = segment;
            if (farEnd.IsValid)
            {
                survivor = StreetSnapping.Connect(segment, StreetEnd.End, farEnd) ?? segment;
            }

            survivor.Rebuild();
            Selection.activeGameObject = survivor.gameObject;

            // Hand straight over to Unity's own draw tool so the road can be carried on without
            // switching tools by hand. Only while the far end is still open: past a docked end the new
            // last knot would be the one the connector drives, and the seam would come apart.
            if (!farEnd.IsValid)
            {
                EditorSplineUtility.SetKnotPlacementTool();
            }

            Undo.CollapseUndoOperations(group);
            Undo.SetCurrentGroupName(UndoName);
        }

        /// <summary>
        /// Gives the new segment a two knot spline: one at the socket, one where the drag ended.
        /// </summary>
        private static void BuildSpline(SplineContainer container, Transform space, Vector3 end)
        {
            Spline spline = container.Spline;
            spline.Clear();

            // The object was placed on the socket and turned the way it faces, so the first knot sits
            // at its origin and the road leaves along local +Z.
            Vector3 localEnd = space.InverseTransformPoint(end);
            float3 direction = new float3(localEnd.x, localEnd.y, localEnd.z);
            Quaternion rotation = StreetFrame.At(direction, new float3(0f, 1f, 0f));

            spline.Add(new BezierKnot(float3.zero), TangentMode.AutoSmooth);
            spline.Add(
                new BezierKnot(
                    direction,
                    float3.zero,
                    float3.zero,
                    new quaternion(rotation.x, rotation.y, rotation.z, rotation.w)),
                TangentMode.AutoSmooth);
        }

        /// <summary>
        /// The street whose tile the new one is built from: one already docked to this intersection
        /// first, because the arms of one junction come from the same kit, then any street in the
        /// scene. Null when there is no street to copy from yet - the segment is then left without a
        /// tile and shows nothing until one is assigned.
        /// </summary>
        private static StreetSegment FindTemplate(IntersectionSocket socket, StreetSegment[] segments)
        {
            Intersection intersection = socket.Owner;

            if (intersection != null)
            {
                for (int index = 0; index < segments.Length; index++)
                {
                    StreetSegment segment = segments[index];
                    if (segment.sourceMesh != null && DocksTo(segment, intersection))
                    {
                        return segment;
                    }
                }
            }

            for (int index = 0; index < segments.Length; index++)
            {
                if (segments[index].sourceMesh != null)
                {
                    return segments[index];
                }
            }

            return null;
        }

        private static bool DocksTo(StreetSegment segment, Intersection intersection)
        {
            return Owns(intersection, segment.startConnection.socket)
                || Owns(intersection, segment.endConnection.socket);
        }

        private static bool Owns(Intersection intersection, IntersectionSocket socket)
        {
            return socket != null && socket.Owner == intersection;
        }

        /// <summary>
        /// Copies everything that says how a street is built from its tile. The width is not among
        /// them - it comes from the socket, which is the side the seam has to line up with.
        /// </summary>
        private static void CopyTile(StreetSegment template, StreetSegment segment)
        {
            if (template == null)
            {
                return;
            }

            segment.sourceMesh = template.sourceMesh;
            segment.forwardAxis = template.forwardAxis;
            segment.tileLength = template.tileLength;
            segment.generateCollider = template.generateCollider;
            segment.curvatureWarningRadius = template.curvatureWarningRadius;

            MeshRenderer source = template.GetComponent<MeshRenderer>();
            MeshRenderer destination = segment.GetComponent<MeshRenderer>();
            if (source != null && destination != null)
            {
                destination.sharedMaterials = source.sharedMaterials;
            }
        }
    }
}
