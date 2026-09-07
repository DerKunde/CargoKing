using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Shows speed, RPM and current gear for the assigned car.
///
/// Content-only: this component does not own a UIDocument or position itself on screen - it is
/// mounted into a slot of Assets/Prefabs/UI_Prefabs/Root_Ingame_Overlay.uxml by IngameOverlay,
/// which calls Bind() once this panel's visual tree has been cloned in.
/// </summary>
public class CarHud : MonoBehaviour
{
    private const string RpmLimiterClass = "car-hud__rpm--limiter";
    private const string GearHiddenClass = "car-hud__gear--hidden";

    [Header("Data source")]
    public CarController carController;

    [Header("Rev limiter feedback")]
    public float blinkSpeed = 6f;

    private Label speedLabel;
    private Label rpmLabel;
    private Label gearLabel;

    /// <summary>
    /// Queries this panel's labels out of its cloned visual tree. Called once by IngameOverlay
    /// after CloneTree() - LateUpdate is a no-op until this has run.
    /// </summary>
    public void Bind(VisualElement panelRoot)
    {
        speedLabel = panelRoot.Q<Label>("car-hud-speed");
        rpmLabel = panelRoot.Q<Label>("car-hud-rpm");
        gearLabel = panelRoot.Q<Label>("car-hud-gear");
    }

    private void LateUpdate()
    {
        // After FixedUpdate/CarController.Drive() so the labels show the values used this frame,
        // not the previous one.
        if (speedLabel == null || carController == null || carController.carEngine == null)
        {
            return;
        }

        CarEngine engine = carController.carEngine;

        // speedInKmH is signed (negative in reverse, see CarEngine.DriveDirection) - a
        // speedometer shows magnitude only, direction is already conveyed by the gear label.
        speedLabel.text = Mathf.RoundToInt(Mathf.Abs(engine.speedInKmH)) + " km/h";
        rpmLabel.text = Mathf.RoundToInt(engine.revolutionsPerMinute).ToString();
        gearLabel.text = engine.currentGear == 0 ? "R" : engine.currentGear.ToString();

        bool inLimiter = engine.revLimiterFactor < 1f;
        rpmLabel.EnableInClassList(RpmLimiterClass, inLimiter);

        // Blink the gear label while the limiter is active, solid otherwise - no coroutine needed.
        bool blinkedOff = inLimiter && Mathf.PingPong(Time.time * blinkSpeed, 1f) <= 0.5f;
        gearLabel.EnableInClassList(GearHiddenClass, blinkedOff);
    }
}
