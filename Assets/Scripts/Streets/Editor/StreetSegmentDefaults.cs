using UnityEditor;
using UnityEngine;

namespace CargoKing.Streets.Editor
{
    /// <summary>
    /// Gives a street segment added through Add Component the kit's default profile.
    ///
    /// Done from the editor because the segment itself cannot: it lives in the runtime assembly and
    /// must not know the Street Kit, which is editor-only. Segments created by the street tools set
    /// their profile themselves; this covers the one path that has no tool in front of it.
    /// </summary>
    [InitializeOnLoad]
    public static class StreetSegmentDefaults
    {
        static StreetSegmentDefaults()
        {
            ObjectFactory.componentWasAdded += OnComponentAdded;
        }

        private static void OnComponentAdded(Component component)
        {
            if (component is StreetSegment segment && segment.profile == null)
            {
                segment.profile = StreetKit.DefaultProfile();
                EditorUtility.SetDirty(segment);
            }
        }
    }
}
