using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CargoKing.Streets.Editor
{
    /// <summary>
    /// The list of intersection prefabs the knot menu offers.
    ///
    /// Listed rather than scanned. Scanning a folder every time would put every test leftover in the
    /// palette and would keep doing it. The one scan this asset does happens when it is created, as a
    /// convenience, and never again - after that the list is curated by hand.
    /// </summary>
    [CreateAssetMenu(fileName = "StreetKit", menuName = "CargoKing/Street Kit")]
    public class StreetKit : ScriptableObject
    {
        [Tooltip("Intersection prefabs offered when a knot is replaced by a junction.")]
        public List<GameObject> intersections = new List<GameObject>();

        /// <summary>Whether an object can serve as a junction: an Intersection on its root.</summary>
        public static bool IsValidEntry(GameObject candidate)
        {
            return candidate != null && candidate.GetComponent<Intersection>() != null;
        }

        /// <summary>
        /// The kit this project uses, or null when there is none yet.
        ///
        /// Sorted by GUID so the choice is the same in every session. More than one kit is not an
        /// error we can resolve, so it is reported rather than guessed at.
        /// </summary>
        public static StreetKit Find(out string problem)
        {
            problem = null;

            string[] guids = AssetDatabase.FindAssets("t:StreetKit");
            if (guids.Length == 0)
            {
                return null;
            }

            System.Array.Sort(guids, System.StringComparer.Ordinal);

            if (guids.Length > 1)
            {
                problem = $"There are {guids.Length} StreetKit assets in this project. "
                    + $"'{AssetDatabase.GUIDToAssetPath(guids[0])}' is being used.";
            }

            return AssetDatabase.LoadAssetAtPath<StreetKit>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        /// <summary>
        /// Creates the kit and fills it once from what the project already holds, so the palette is
        /// not empty on the first use.
        /// </summary>
        public static StreetKit CreateSeeded()
        {
            StreetKit kit = CreateInstance<StreetKit>();

            string[] guids = AssetDatabase.FindAssets("t:Prefab");
            for (int index = 0; index < guids.Length; index++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[index]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (IsValidEntry(prefab))
                {
                    kit.intersections.Add(prefab);
                }
            }

            // A unique path, not a fixed one. CreateAsset onto a taken path logs an error and quietly
            // fails to persist, and this method would still hand back the in-memory object as though
            // it had worked - a kit that vanishes on the next reload, after junctions were inserted
            // from it. If the path is taken by something that is not a kit, Find would not have seen
            // it, so stepping aside is the right move.
            string uniquePath = AssetDatabase.GenerateUniqueAssetPath("Assets/StreetKit.asset");

            AssetDatabase.CreateAsset(kit, uniquePath);
            AssetDatabase.SaveAssets();

            return kit;
        }
    }

    /// <summary>
    /// Says which entries of a kit cannot serve as a junction. A list of prefab slots gives no hint
    /// by itself that one of them is the wrong kind of prefab.
    /// </summary>
    [CustomEditor(typeof(StreetKit))]
    public class StreetKitEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            StreetKit kit = (StreetKit)target;

            for (int index = 0; index < kit.intersections.Count; index++)
            {
                GameObject entry = kit.intersections[index];

                if (entry == null)
                {
                    EditorGUILayout.HelpBox($"Entry {index} is empty.", MessageType.Warning);
                }
                else if (!StreetKit.IsValidEntry(entry))
                {
                    EditorGUILayout.HelpBox(
                        $"'{entry.name}' has no Intersection component on its root, so no street can "
                        + "dock to it.",
                        MessageType.Warning);
                }
            }
        }
    }
}
