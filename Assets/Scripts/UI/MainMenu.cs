using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace CargoKing.UI
{
    /// <summary>
    /// Wires the main menu's buttons. Lives next to the menu's UIDocument.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class MainMenu : MonoBehaviour
    {
        [SerializeField] private string gameSceneName = "Main_Scene";

        private Button playButton;
        private Button exitButton;

        private void OnEnable()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;

            playButton = root.Q<Button>("Play");
            exitButton = root.Q<Button>("Exit");

            playButton.clicked += OnPlay;
            exitButton.clicked += OnExit;
        }

        private void OnDisable()
        {
            playButton.clicked -= OnPlay;
            exitButton.clicked -= OnExit;
        }

        private void OnPlay()
        {
            SceneManager.LoadScene(gameSceneName);
        }

        private void OnExit()
        {
#if UNITY_EDITOR
            // Application.Quit is ignored in the Editor, so leave Play Mode instead.
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
