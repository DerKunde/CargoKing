using UnityEditor;
using UnityEngine;

namespace CargoKing.Streets.Editor
{
    /// <summary>
    /// The inspector of a speed sign and its drag handle. The limit is edited here; where the sign stands
    /// is changed only by dragging it to another slot, so it never leaves the grid.
    /// </summary>
    [CustomEditor(typeof(StreetSpeedSign))]
    [CanEditMultipleObjects]
    public class StreetSpeedSignEditor : UnityEditor.Editor
    {
        private SerializedProperty limitKmh;

        private void OnEnable()
        {
            limitKmh = serializedObject.FindProperty(nameof(StreetSpeedSign.limitKmh));
        }

        private void OnDisable()
        {
            // Hidden while a sign was selected; the Move tool has to come back with the next selection.
            Tools.hidden = false;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(limitKmh, new GUIContent("Limit (km/h)"));

            if (serializedObject.ApplyModifiedProperties())
            {
                StreetSignPlacement.LastLimitKmh = limitKmh.floatValue;
                Rename();
            }

            if (targets.Length != 1)
            {
                return;
            }

            StreetSpeedSign sign = (StreetSpeedSign)target;
            StreetSegment segment = sign.Segment;

            if (segment == null)
            {
                EditorGUILayout.HelpBox(
                    "This sign does not stand on a street and has no effect. Delete it and place a new one "
                    + "with a street's Place Speed Signs mode.",
                    MessageType.Warning);
                return;
            }

            string side = sign.side == StreetSide.Right ? "right" : "left";
            EditorGUILayout.LabelField("Street", segment.name);
            EditorGUILayout.LabelField("Stands", $"{side} of the spline, {sign.distance:0.#} m along");
            EditorGUILayout.LabelField("Governs", sign.side == StreetSide.Right ? "forward lane" : "backward lane");
            EditorGUILayout.HelpBox(
                $"Drag the handle in the scene to move it. It snaps every {StreetSignSlots.Spacing:0} m, on either side.",
                MessageType.None);
        }

        /// <summary>Names follow the number, so the hierarchy reads as a list of limits.</summary>
        private void Rename()
        {
            for (int index = 0; index < targets.Length; index++)
            {
                StreetSpeedSign sign = (StreetSpeedSign)targets[index];
                string name = $"Speed Sign {sign.limitKmh:0}";

                if (sign.gameObject.name != name)
                {
                    Undo.RecordObject(sign.gameObject, "Rename Speed Sign");
                    sign.gameObject.name = name;
                }
            }
        }

        private void OnSceneGUI()
        {
            StreetSpeedSign sign = (StreetSpeedSign)target;
            StreetSegment segment = sign.Segment;

            // Unity's own Move tool would drag the sign off its slot, and the segment would snap it back
            // on its next rebuild. The handle below replaces it.
            Tools.hidden = segment != null;

            if (segment == null || !StreetDrawing.Enabled)
            {
                return;
            }

            Vector3 position = sign.transform.position;
            float size = HandleUtility.GetHandleSize(position) * 0.15f;

            Handles.color = Color.white;
            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.FreeMoveHandle(position, size, Vector3.zero, Handles.SphereHandleCap);

            if (EditorGUI.EndChangeCheck())
            {
                StreetSignPlacement.NearestSlot(segment, moved, out StreetSide side, out float distance);
                StreetSignPlacement.MoveTo(sign, side, distance);
            }
        }
    }
}
