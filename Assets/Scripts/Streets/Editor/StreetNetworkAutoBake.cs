using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CargoKing.Streets.Editor
{
    /// <summary>
    /// Bakes a stale network by itself, on saving the scene and on entering play.
    ///
    /// Without this the failure mode is silent and confusing: the scene looks right, the cars drive
    /// the network as it was ten edits ago, and nothing says so.
    /// </summary>
    [InitializeOnLoad]
    public static class StreetNetworkAutoBake
    {
        static StreetNetworkAutoBake()
        {
            EditorSceneManager.sceneSaving += OnSceneSaving;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnSceneSaving(Scene scene, string path)
        {
            BakeStaleNetworks();
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode)
            {
                BakeStaleNetworks();
            }
        }

        private static void BakeStaleNetworks()
        {
            StreetNetworkAuthoring[] networks =
                Object.FindObjectsByType<StreetNetworkAuthoring>(FindObjectsSortMode.None);

            for (int index = 0; index < networks.Length; index++)
            {
                StreetNetworkAuthoring authoring = networks[index];

                if (authoring.asset != null && StreetNetworkBakeJob.IsStale(authoring))
                {
                    StreetNetworkBakeJob.Bake(authoring);
                }
            }
        }
    }
}
