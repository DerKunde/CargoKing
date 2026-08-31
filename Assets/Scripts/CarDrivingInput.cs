using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The only place in the vehicle that knows about the Input System. Translates the
/// "Driving" map into device independent values for physics and engine.
/// </summary>
public class CarDrivingInput : MonoBehaviour
{
    [Header("Keyboard smoothing")]
    // Keyboard only: a key reports 0 or 1, so the demand has to be ramped. Triggers,
    // sticks and pedals report real intermediate values and would only lag behind.
    public float pedalRiseRate = 3.3f;
    public float pedalFallRate = 5f;
    public float steerRate = 3.3f;
    public float steerReturnRate = 3.3f;

    [Header("Keyboard response curves")]
    // Travel on x, demand on y, both 0..1. A flat start spends more of the hold time on
    // the lower range and gives fine control off idle. Keyboard only, same reason as
    // above. Left linear: the shape is a tuning decision and belongs in the Inspector.
    public AnimationCurve throttleCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    public AnimationCurve steerCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    public AnimationCurve brakeCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Header("Sampled values !!! Do not change !!!")]
    // Control position before the response curve. On an analog device already final.
    [SerializeField] private float steerTravel;
    [SerializeField] private float throttleTravel;
    [SerializeField] private float brakeTravel;

    // Final values handed to the consumers.
    [SerializeField] private float steer;
    [SerializeField] private float throttle;
    [SerializeField] private float brake;
    [SerializeField] private bool handbrake;

    /// <summary>Steering wheel position, -1 (left) to 1 (right).</summary>
    public float Steer => steer;

    /// <summary>Throttle pedal position, 0 to 1.</summary>
    public float Throttle => throttle;

    /// <summary>Brake pedal position, 0 to 1.</summary>
    public float Brake => brake;

    /// <summary>Handbrake, pressed or not.</summary>
    public bool Handbrake => handbrake;

    private InputSystem_Actions actions;

    // Shifting and reset arrive as events on the update tick but are consumed in
    // FixedUpdate. Without the buffer a press is lost when two frames share one step.
    private bool shiftUpPending;
    private bool shiftDownPending;
    private bool resetPending;

    // activeControl turns null once an action falls back to zero, so the last known
    // source keeps applying: on release the keyboard ramps out, a trigger snaps to zero.
    private bool steerIsDigital;
    private bool throttleIsDigital;
    private bool brakeIsDigital;

    private float lastSampleTime = float.NegativeInfinity;
    private DrivingInput lastSample = DrivingInput.None;

    private void Awake()
    {
        actions = new InputSystem_Actions();
    }

    private void OnEnable()
    {
        actions.Driving.ShiftUp.performed += OnShiftUp;
        actions.Driving.ShiftDown.performed += OnShiftDown;
        actions.Driving.ResetCar.performed += OnResetCar;
        actions.Driving.Enable();
    }

    private void OnDisable()
    {
        actions.Driving.Disable();
        actions.Driving.ShiftUp.performed -= OnShiftUp;
        actions.Driving.ShiftDown.performed -= OnShiftDown;
        actions.Driving.ResetCar.performed -= OnResetCar;

        // Otherwise the car comes back from a re-enable with the throttle stuck.
        steerTravel = 0f;
        throttleTravel = 0f;
        brakeTravel = 0f;
        steer = 0f;
        throttle = 0f;
        brake = 0f;
        handbrake = false;
        shiftUpPending = false;
        shiftDownPending = false;
        resetPending = false;
        lastSampleTime = float.NegativeInfinity;
        lastSample = DrivingInput.None;
    }

    private void OnDestroy()
    {
        actions?.Dispose();
    }

    // The one place the devices are read; Sample() only hands the fields out. Reading
    // them there as well would advance the ramps twice per step. FixedUpdate because
    // every consumer is physics and the ramps run on fixedDeltaTime. Without a Script
    // Execution Order entry a consumer may sample one step behind.
    private void FixedUpdate()
    {
        steerTravel = ReadAxis(actions.Driving.Steer, steerTravel, ref steerIsDigital, steerRate, steerReturnRate);
        throttleTravel = ReadAxis(actions.Driving.Throttle, throttleTravel, ref throttleIsDigital, pedalRiseRate, pedalFallRate);
        brakeTravel = ReadAxis(actions.Driving.Brake, brakeTravel, ref brakeIsDigital, pedalRiseRate, pedalFallRate);

        // Analog travel goes through untouched: the held position is already the demand.
        steer = steerIsDigital ? ApplyResponse(steerCurve, steerTravel) : steerTravel;
        throttle = throttleIsDigital ? ApplyResponse(throttleCurve, throttleTravel) : throttleTravel;
        brake = brakeIsDigital ? ApplyResponse(brakeCurve, brakeTravel) : brakeTravel;
        handbrake = actions.Driving.Handbrake.IsPressed();
    }

    /// <summary>Reports a request to shift up and consumes it, so one press counts once.</summary>
    public bool ConsumeShiftUp()
    {
        bool pending = shiftUpPending;
        shiftUpPending = false;
        return pending;
    }

    /// <summary>Same as <see cref="ConsumeShiftUp"/>, but downwards.</summary>
    public bool ConsumeShiftDown()
    {
        bool pending = shiftDownPending;
        shiftDownPending = false;
        return pending;
    }

    /// <summary>Reports a reset request and consumes it in the process.</summary>
    public bool ConsumeReset()
    {
        bool pending = resetPending;
        resetPending = false;
        return pending;
    }

    /// <summary>
    /// Shapes keyboard travel through the response curve. The curve is authored over
    /// positive travel, so a signed axis is shaped by magnitude and re-signed after -
    /// otherwise left and right could not share one curve.
    /// </summary>
    private float ApplyResponse(AnimationCurve curve, float travel)
    {
        // An empty curve evaluates to zero and would silently kill the input.
        if (curve == null || curve.length == 0)
        {
            return travel;
        }

        // Hand drawn curves overshoot their end key easily.
        float shaped = curve.Evaluate(Mathf.Abs(travel));
        return Mathf.Clamp01(shaped) * Mathf.Sign(travel);
    }

    private float ReadAxis(InputAction action, float currentValue, ref bool isDigital, float riseRate, float fallRate)
    {
        float target = action.ReadValue<float>();

        // The source is only known while the action runs; afterwards the last one holds.
        InputControl activeControl = action.activeControl;
        if (activeControl != null)
        {
            isDigital = activeControl.device is Keyboard;
        }

        if (!isDigital)
        {
            return target;
        }

        // Away from zero uses the rise rate, towards zero the fall rate. Steering from
        // left to right counts as releasing until the centre is passed.
        bool movingAwayFromZero = Mathf.Abs(target) > Mathf.Abs(currentValue) && Mathf.Sign(target) == Mathf.Sign(currentValue);
        bool startingFromZero = Mathf.Approximately(currentValue, 0f);
        float rate = (movingAwayFromZero || startingFromZero) ? riseRate : fallRate;

        return Mathf.MoveTowards(currentValue, target, rate * Time.fixedDeltaTime);
    }

    private void OnShiftUp(InputAction.CallbackContext context)
    {
        shiftUpPending = true;
    }

    private void OnShiftDown(InputAction.CallbackContext context)
    {
        shiftDownPending = true;
    }

    private void OnResetCar(InputAction.CallbackContext context)
    {
        resetPending = true;
    }

    private GearShift ConsumeShift()
    {
        GearShift shift = GearShift.None;

        if (shiftUpPending)
        {
            shift = GearShift.Up;
        }
        else if (shiftDownPending)
        {
            shift = GearShift.Down;
        }

        shiftUpPending = false;
        shiftDownPending = false;
        return shift;
    }

    /// <summary>
    /// The driving commands of the current physics step, sampled and shaped in
    /// <see cref="FixedUpdate"/>. Repeated calls within one step return the same struct,
    /// so a pending gear change is consumed once and every caller sees it.
    /// </summary>
    public DrivingInput Sample()
    {
        if (Time.fixedTime == lastSampleTime)
        {
            return lastSample;
        }

        lastSampleTime = Time.fixedTime;
        lastSample = new DrivingInput(steer, throttle, brake, handbrake, ConsumeShift());
        return lastSample;
    }
}
