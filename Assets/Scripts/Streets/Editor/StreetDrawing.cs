using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace CargoKing.Streets.Editor
{
    /// <summary>
    /// Whether the street tools draw anything in the scene view.
    ///
    /// Lanes, end handles and socket handles are what makes a road readable while it is being laid
    /// out, and exactly what gets in the way once it is. Unity's own spline editor wants those pixels
    /// and those clicks; this is the switch that gives them back.
    ///
    /// The curvature warning on <see cref="StreetSegment"/> is deliberately not covered. It is a fault
    /// report rather than an aid, and it never sits under the cursor.
    /// </summary>
    [InitializeOnLoad]
    public static class StreetDrawing
    {
        private const string MenuPath = "Tools/CargoKing/Street Drawings";
        private const string PreferenceKey = "CargoKing.Streets.Drawings";

        private static bool enabled;

        static StreetDrawing()
        {
            enabled = EditorPrefs.GetBool(PreferenceKey, true);
        }

        /// <summary>
        /// True while the street tools may draw. Kept in <see cref="EditorPrefs"/> so it survives a
        /// domain reload - a switch that silently turns itself back on after every recompile would be
        /// worse than no switch.
        /// </summary>
        public static bool Enabled
        {
            get => enabled;
            set
            {
                if (enabled == value)
                {
                    return;
                }

                enabled = value;
                EditorPrefs.SetBool(PreferenceKey, value);
                SceneView.RepaintAll();
            }
        }

        /// <summary>
        /// Says in the inspector that the scene view is empty on purpose. Without it the switch is
        /// a trap: everything looks broken and nothing points at the reason.
        /// </summary>
        public static void DrawInspectorNotice()
        {
            if (Enabled)
            {
                return;
            }

            EditorGUILayout.HelpBox(
                "Street drawings are off. Nothing is drawn or grabbable in the scene view, so Unity's "
                + "spline editor has it to itself. Switch them back on under Tools > CargoKing > "
                + "Street Drawings.",
                MessageType.Info);
        }

        [MenuItem(MenuPath)]
        private static void Toggle()
        {
            Enabled = !Enabled;
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return true;
        }

        // A rebindable shortcut rather than a hotkey baked into the menu path: this gets flipped often
        // enough that it has to sit wherever its owner wants it.
        [Shortcut("CargoKing/Toggle Street Drawings", KeyCode.D, ShortcutModifiers.Action | ShortcutModifiers.Shift)]
        private static void ToggleShortcut()
        {
            Enabled = !Enabled;
        }
    }
}
