using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Root shell for the in-game UI overlay. Owns the one UIDocument for ingame UI and mounts
/// individual panels (CarHud today, more later) into named slots declared in
/// Root_Ingame_Overlay.uxml, instead of each panel owning its own UIDocument.
///
/// Setup: lives on Assets/Prefabs/UI_Prefabs/Ingame_UI_Overlay.prefab, alongside a CarHud
/// component. RequireComponent brings the UIDocument along - assign PanelSettings and
/// Root_Ingame_Overlay.uxml as its Source Asset, and assign carHudAsset to CarHud.uxml.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class IngameOverlay : MonoBehaviour
{
    [Header("Panels")]
    public VisualTreeAsset carHudAsset;

    private UIDocument document;
    private CarHud carHud;
    private VisualElement bottomCenterSlot;

    private void Awake()
    {
        document = GetComponent<UIDocument>();
        carHud = GetComponent<CarHud>();
    }

    private void OnDisable()
    {
        // The UIDocument throws its tree away on deactivation, leaving the reference dangling.
        // Reset here and rebuild on the next pass.
        bottomCenterSlot = null;
    }

    private void LateUpdate()
    {
        EnsurePanelsMounted();
    }

    private void EnsurePanelsMounted()
    {
        if (bottomCenterSlot != null && bottomCenterSlot.panel != null)
        {
            return;
        }

        if (document == null)
        {
            document = GetComponent<UIDocument>();
        }

        VisualElement root = document != null ? document.rootVisualElement : null;

        if (root == null)
        {
            return;
        }

        bottomCenterSlot = root.Q<VisualElement>("bottom-center-slot");

        if (bottomCenterSlot == null || carHudAsset == null || carHud == null)
        {
            return;
        }

        carHudAsset.CloneTree(bottomCenterSlot);
        carHud.Bind(bottomCenterSlot);
    }
}
