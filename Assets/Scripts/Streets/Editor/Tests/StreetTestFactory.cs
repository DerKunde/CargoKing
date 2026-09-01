using System.Collections.Generic;
using Unity.Mathematics;
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

        /// <summary>
        /// A segment whose spline runs through the given points, expressed in its own local space.
        /// </summary>
        public static StreetSegment Create(string name, params Vector3[] localKnots)
        {
            GameObject gameObject = new GameObject(name);
            created.Add(gameObject);

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
            segment.roadWidth = 16f;
            segment.tileLength = 0f;
            segment.forwardAxis = StreetMeshAxis.X;

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
        }
    }
}
