using UnityEditor;
using UnityEngine;

namespace CargoKing.Streets.Editor
{
    /// <summary>
    /// Draws every speed sign in the scene view, selected or not: a round sign with its number, and an
    /// arrow on the lane beside it pointing the way the governed traffic moves.
    /// </summary>
    public static class StreetSpeedSignGizmo
    {
        private const float DiscRadius = 0.9f;
        private const float PostHeight = 2.2f;

        private static readonly Color RimColour = new Color(0.85f, 0.1f, 0.1f);
        private static readonly Color FaceColour = new Color(1f, 1f, 1f, 0.9f);
        private static readonly Color PostColour = new Color(0.6f, 0.6f, 0.6f);
        private static readonly Color ArrowColour = new Color(1f, 1f, 1f, 0.8f);
        private static readonly Color StrayColour = new Color(1f, 0.35f, 0.35f);

        private static GUIStyle numberStyle;

        [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected | GizmoType.Pickable)]
        private static void Draw(StreetSpeedSign sign, GizmoType gizmoType)
        {
            if (!StreetDrawing.Enabled)
            {
                return;
            }

            Transform transform = sign.transform;
            Vector3 foot = transform.position;
            Vector3 centre = foot + transform.up * PostHeight;
            Vector3 face = transform.forward;

            // Nearly invisible, but it is what makes a sign clickable in the scene - it has no renderer.
            Gizmos.color = new Color(1f, 1f, 1f, 0.01f);
            Gizmos.DrawSphere(centre, DiscRadius);

            Handles.color = PostColour;
            Handles.DrawLine(foot, centre - transform.up * DiscRadius);

            Handles.color = FaceColour;
            Handles.DrawSolidDisc(centre, face, DiscRadius);

            Handles.color = RimColour;
            Handles.DrawWireDisc(centre, face, DiscRadius, 4f);

            numberStyle ??= new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.black },
            };

            Handles.Label(centre, Mathf.RoundToInt(sign.limitKmh).ToString(), numberStyle);

            StreetSegment segment = sign.Segment;
            if (segment == null || !segment.TryGetSignSlot(sign.side, sign.distance, out StreetSignSlot slot))
            {
                Handles.color = StrayColour;
                Handles.Label(foot, "not on a street");
                return;
            }

            // The lane runs a quarter of the road's width from the centre line, on the sign's side.
            Vector3 outward = foot - slot.centre;
            outward.y = 0f;
            Vector3 lanePoint = slot.centre + outward.normalized * (segment.RoadWidth * 0.25f);

            // The governed traffic drives towards the face, so it moves along -face.
            Handles.color = ArrowColour;
            Handles.ArrowHandleCap(
                0,
                lanePoint + Vector3.up * 0.3f,
                Quaternion.LookRotation(-face, Vector3.up),
                HandleUtility.GetHandleSize(lanePoint) * 0.4f,
                EventType.Repaint);
        }
    }
}
