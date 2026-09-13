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
    /// </summary>
    internal static class StreetTestFactory
    {
        private static readonly List<GameObject> created = new List<GameObject>();
        private static readonly List<Object> createdAssets = new List<Object>();
        private static StreetProfile sharedProfile;

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
            GameObject gameObject = new GameObject(name);
            created.Add(gameObject);

            // Recorded as an undo step on purpose. The Test Runner reverts every undo step of a run
            // when the run ends. Street tools destroy segments through Undo, and without a matching
            // record of the creation, reverting that destruction put the segment back into the open
            // scene - where it stayed. With the creation recorded, the revert takes it away again.
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

        public static void DestroyAll()
        {
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
