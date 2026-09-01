using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace CargoKing.Streets.Editor
{
    /// <summary>
    /// A small handle on every inner knot of a street, and the menu it opens.
    ///
    /// This is the one place a road is cut or a junction is put into it, so both live on the same
    /// gesture. The end knots get nothing - there is no road on the far side of them to cut off.
    ///
    /// These handles are drawn only while <see cref="StreetDrawing"/> is on, which is what keeps them
    /// from fighting Unity's own knot handles for clicks.
    /// </summary>
    public static class StreetKnotHandles
    {
        private static readonly Color KnotColor = new Color(1f, 0.85f, 0.3f);

        /// <summary>Size of a knot button, as a share of the handle size at that distance.</summary>
        private const float ButtonSize = 0.06f;

        public static void Draw(StreetSegment segment)
        {
            Spline spline = StreetSurgery.SplineOf(segment);
            if (spline == null || spline.Count < 3)
            {
                return;
            }

            Handles.color = KnotColor;

            for (int index = 1; index < spline.Count - 1; index++)
            {
                Vector3 position = segment.transform.TransformPoint(
                    new Vector3(spline[index].Position.x, spline[index].Position.y, spline[index].Position.z));

                float size = HandleUtility.GetHandleSize(position) * ButtonSize;

                if (Handles.Button(position, Quaternion.identity, size, size * 2f, Handles.DotHandleCap))
                {
                    ShowMenu(segment, index);
                }
            }
        }

        private static void ShowMenu(StreetSegment segment, int knotIndex)
        {
            GenericMenu menu = new GenericMenu();

            if (StreetSurgery.CanSplit(segment, knotIndex, out string problem))
            {
                menu.AddItem(new GUIContent("Split here"), false, () => StreetSurgery.Split(segment, knotIndex));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent($"Split here - {problem}"));
            }

            menu.ShowAsContext();
        }
    }
}
