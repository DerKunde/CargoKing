using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CargoKing.Streets
{
    /// <summary>
    /// One street network in a scene: which asset it bakes into, and the runtime built from it.
    ///
    /// Two of these means two independent networks, each with its own asset. Nothing here bakes -
    /// baking is editor work; this object only says what belongs together and hands out the runtime.
    /// </summary>
    [AddComponentMenu("CargoKing/Street Network")]
    public class StreetNetworkAuthoring : MonoBehaviour
    {
        [Tooltip("Asset this network bakes into. The runtime reads this, never the scene objects.")]
        public StreetNetworkAsset asset;

        private StreetNetworkRuntime runtime;

        /// <summary>
        /// The runtime over this network's asset, created on first use. Null while no asset is set.
        /// </summary>
        public StreetNetworkRuntime Runtime
        {
            get
            {
                if (asset == null)
                {
                    return null;
                }

                if (runtime == null || runtime.Asset != asset)
                {
                    runtime = new StreetNetworkRuntime(asset);
                }

                return runtime;
            }
        }

        /// <summary>
        /// Every street segment of this network. Scoped to the scene this object is in, so two scenes
        /// loaded at once keep their networks apart.
        /// </summary>
        public void CollectSegments(List<StreetSegment> results)
        {
            results.Clear();
            Scene scene = gameObject.scene;

            StreetSegment[] found = FindObjectsByType<StreetSegment>(FindObjectsSortMode.None);
            for (int index = 0; index < found.Length; index++)
            {
                if (found[index].gameObject.scene == scene)
                {
                    results.Add(found[index]);
                }
            }
        }

        /// <summary>Every intersection of this network, scoped the same way.</summary>
        public void CollectIntersections(List<Intersection> results)
        {
            results.Clear();
            Scene scene = gameObject.scene;

            Intersection[] found = FindObjectsByType<Intersection>(FindObjectsSortMode.None);
            for (int index = 0; index < found.Length; index++)
            {
                if (found[index].gameObject.scene == scene)
                {
                    results.Add(found[index]);
                }
            }
        }

        /// <summary>Every speed sign of this network, scoped the same way - including stray ones.</summary>
        public void CollectSpeedSigns(List<StreetSpeedSign> results)
        {
            results.Clear();
            Scene scene = gameObject.scene;

            StreetSpeedSign[] found = FindObjectsByType<StreetSpeedSign>(FindObjectsSortMode.None);
            for (int index = 0; index < found.Length; index++)
            {
                if (found[index].gameObject.scene == scene)
                {
                    results.Add(found[index]);
                }
            }
        }
    }
}
