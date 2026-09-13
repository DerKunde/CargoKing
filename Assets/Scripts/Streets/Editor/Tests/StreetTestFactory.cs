using System.Collections.Generic;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace CargoKing.Streets.Editor.Tests
{
    /// <summary>
    /// Builds throwaway street segments for tests and takes them away again.
    ///
    /// Every object it hands out is remembered, because a leaked StreetSegment keeps its subscription
    /// to the static Spline.Changed event and would go on reacting to splines in later tests.
    ///
    /// It also owns the undo history a test leaves behind. The street tools record their work through
    /// Undo, and the Test Runner reverts everything recorded during a run - but only after it has
    /// closed the scene the tests ran in. Any step that brings an object back then has nowhere to put
    /// it. So the factory opens an undo group with the first segment of a test and
    /// <see cref="DestroyAll"/> reverts it while that scene still exists.
    /// </summary>
    internal static class StreetTestFactory
    {
        private static readonly List<GameObject> created = new List<GameObject>();
        private static readonly List<Object> createdAssets = new List<Object>();
        private static StreetProfile sharedProfile;
        private static int undoGroup = -1;

        /// <summary>
        /// The profile every factory segment starts with: 16 m, no tile. Shared, so two factory
        /// segments count as the same class of road and can be merged.
        /// </summary>
        public static StreetProfile SharedProfile
        {
            get
            {
                if (sharedProfile == null)
                {
                    sharedProfile = Profile(16f);
                }

                return sharedProfile;
            }
        }

        /// <summary>A fresh profile of the given width, taken away again by <see cref="DestroyAll"/>.</summary>
        public static StreetProfile Profile(float width)
        {
            StreetProfile profile = ScriptableObject.CreateInstance<StreetProfile>();
            profile.name = $"Test Profile {width:0.#} m";
            profile.roadWidth = width;
            createdAssets.Add(profile);
            return profile;
        }

        /// <summary>
        /// A segment whose spline runs through the given points, expressed in its own local space.
        /// </summary>
        public static StreetSegment Create(string name, params Vector3[] localKnots)
        {
            if (undoGroup < 0)
            {
                Undo.IncrementCurrentGroup();
                undoGroup = Undo.GetCurrentGroup();
            }

            GameObject gameObject = new GameObject(name);
            created.Add(gameObject);

            // Recorded so that reverting the test's undo group takes the segment away again, even
            // after a street tool destroyed it through Undo and the revert brought it back first.
            Undo.RegisterCreatedObjectUndo(gameObject, "Create Test Street");

            SplineContainer container = gameObject.AddComponent<SplineContainer>();
            Spline spline = container.Spline;
            spline.Clear();

            for (int index = 0; index < localKnots.Length; index++)
            {
                Vector3 point = localKnots[index];
                spline.Add(new BezierKnot(new float3(point.x, point.y, point.z)), TangentMode.AutoSmooth);
            }

            // Added after the container so StreetSegment.OnEnable finds it.
            StreetSegment segment = gameObject.AddComponent<StreetSegment>();
            segment.profile = SharedProfile;

            return segment;
        }

        /// <summary>A speed sign standing on a segment, on the given side and distance.</summary>
        public static StreetSpeedSign Sign(StreetSegment segment, StreetSide side, float distance, float limitKmh = 30f)
        {
            GameObject gameObject = new GameObject($"Speed Sign {limitKmh:0}");
            created.Add(gameObject);
            Undo.RegisterCreatedObjectUndo(gameObject, "Create Test Sign");

            gameObject.transform.SetParent(segment.transform, false);

            StreetSpeedSign sign = gameObject.AddComponent<StreetSpeedSign>();
            sign.side = side;
            sign.distance = distance;
            sign.limitKmh = limitKmh;

            return sign;
        }

        public static void DestroyAll()
        {
            // First, while the test's scene is still there: everything the test recorded through
            // Undo is taken back, so the Test Runner finds nothing of it left to revert later.
            if (undoGroup >= 0)
            {
                Undo.RevertAllDownToGroup(undoGroup);
                undoGroup = -1;
            }

            for (int index = 0; index < created.Count; index++)
            {
                if (created[index] != null)
                {
                    Object.DestroyImmediate(created[index]);
                }
            }

            created.Clear();

            for (int index = 0; index < createdAssets.Count; index++)
            {
                if (createdAssets[index] != null)
                {
                    Object.DestroyImmediate(createdAssets[index]);
                }
            }

            createdAssets.Clear();
            sharedProfile = null;
        }
    }
}
