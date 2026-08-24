using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The only place in the vehicle that knows about the Input System. Translates the
/// actions of the "Driving" map into device independent values that physics and
/// engine can consume directly.
/// </summary>
public class CarDrivingInput : MonoBehaviour
{
    [Header("Keyboard smoothing")]
    // A key only ever reports 0 or 1. Triggers, sticks and later on the pedals of a
    // steering wheel report real intermediate values - ramping those would make the
    // input lag behind the pedal. These rates therefore only apply when the input
    // actually came from the keyboard.
    // The rise rates reproduce the previous timing: gasPadleCurve is already at 1.0
    // by x = 0.3, so full throttle took 0.3 seconds - the clamp of currentTimeOnPedle
    // to 1 only caps the accumulator, it never gated the curve. Steering used the
    // same 0.3 seconds through MAX_TIME.
    // Releasing is a deliberate deviation: CarController dropped the throttle to zero
    // in a single step, which no real pedal does.
    public float pedalRiseRate = 3.3f;
    public float pedalFallRate = 5f;
    public float steerRate = 3.3f;
    public float steerReturnRate = 3.3f;

    [Header("Keyboard response curves")]
    // Keyboard only, by design. A trigger or a pedal already lets the player meter
    // the input with their own hand, and shaping that would work against them. A key
    // is pressed or not, so there the demand has to be derived from how long it is
    // held - and these curves decide what that hold time is worth.
    // Travel on x, resulting demand on y, both 0..1. A curve that stays flat at the
    // start spends more of the hold time on the lower range, which is what gives
    // fine control off idle. Since the keyboard ramp is linear, plotting over travel
    // is equivalent to plotting over hold time, just with x normalised to 0..1.
    // Left linear on purpose: the shape is a tuning decision and belongs to the
    // Inspector, not to a default in code. An empty curve is passed through.
    public AnimationCurve throttleCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    public AnimationCurve steerCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    public AnimationCurve brakeCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Header("Sampled values !!! Do not change !!!")]
    // Travel before the response curve. This is what the rates ramp, so that the
    // curve shapes the result rather than distorting the ramp speed. On an analog
    // device this is already the final value - it is the physical control position.
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
    // FixedUpdate. Without this buffer a key press is lost as soon as two frames
    // fall into the same physics step - exactly the flaw the wasPressedThisFrame
    // checks in FixedUpdate still have today.
    private bool shiftUpPending;
    private bool shiftDownPending;
    private bool resetPending;

    // activeControl turns null as soon as an action falls back to zero. On release
    // it still has to be known what was driving last: the keyboard wants to ramp
    // out, a trigger snaps to zero immediately.
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

    // Deliberately FixedUpdate rather than Update: every consumer is physics, and
    // the ramps already ran on Time.fixedDeltaTime. Values smoothed in Update would
    // be one frame stale by the time the physics step reads them.
    private void FixedUpdate()
    {
        steerTravel = ReadAxis(actions.Driving.Steer, steerTravel, ref steerIsDigital, steerRate, steerReturnRate);
        throttleTravel = ReadAxis(actions.Driving.Throttle, throttleTravel, ref throttleIsDigital, pedalRiseRate, pedalFallRate);
        brakeTravel = ReadAxis(actions.Driving.Brake, brakeTravel, ref brakeIsDigital, pedalRiseRate, pedalFallRate);

        // Analog travel goes through untouched: on a trigger or a pedal the position
        // the player is holding is already the demand they mean.
        steer = steerIsDigital ? ApplyResponse(steerCurve, steerTravel) : steerTravel;
        throttle = throttleIsDigital ? ApplyResponse(throttleCurve, throttleTravel) : throttleTravel;

        // Suspension turns this into a longitudinal force at the contact patch,
        // clamped against grip and against the impulse needed to reach a standstill.
        brake = brakeIsDigital ? ApplyResponse(brakeCurve, brakeTravel) : brakeTravel;
        handbrake = actions.Driving.Handbrake.IsPressed();
    }

    /// <summary>
    /// Reports a request to shift up and consumes it in the process, so a single key
    /// press is answered exactly once.
    /// </summary>
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
    /// Maps keyboard travel through the response curve. The curve is authored over
    /// positive travel only, so a signed axis is shaped by magnitude and gets its
    /// sign back afterwards - otherwise steering left and right could not share one
    /// curve.
    /// </summary>
    private float ApplyResponse(AnimationCurve curve, float travel)
    {
        // An empty curve evaluates to zero and would silently kill the input.
        if (curve == null || curve.length == 0)
        {
            return travel;
        }

        float shaped = curve.Evaluate(Mathf.Abs(travel));

        // Hand drawn curves overshoot their end key easily; the consumers expect a
        // normalised demand.
        return Mathf.Clamp01(shaped) * Mathf.Sign(travel);
    }

    private float ReadAxis(InputAction action, float currentValue, ref bool isDigital, float riseRate, float fallRate)
    {
        float target = action.ReadValue<float>();

        // The source is only known while the action is in progress. Afterwards
        // whatever was detected last keeps applying.
        InputControl activeControl = action.activeControl;
        if (activeControl != null)
        {
            isDigital = activeControl.device is Keyboard;
        }

        if (!isDigital)
        {
            return target;
        }

        // Moving away from zero uses the lock rate, moving towards zero uses the
        // release rate. Across zero - steering from left to right - the way back to
        // centre still counts as releasing.
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

    public DrivingInput Sample()
    {
        if(Time.fixedDeltaTime == lastSampleTime)
        {
            return lastSample;
        }

        float steeringTravel = ReadAxis(actions.Driving.Steer, this.steerTravel, ref steerIsDigital, steerRate, steerReturnRate);
        float throttleTravel = ReadAxis(actions.Driving.Throttle, this.throttleTravel, ref throttleIsDigital, pedalRiseRate, pedalFallRate);
        float brakeTravel = ReadAxis(actions.Driving.Brake, this.brakeTravel, ref brakeIsDigital, pedalRiseRate, pedalFallRate);
        
        steer = steerIsDigital ? ApplyResponse(steerCurve, steerTravel) : steerTravel;
        throttle = throttleIsDigital ? ApplyResponse(throttleCurve, this.throttleTravel) : throttleTravel;
        brake = brakeIsDigital ? ApplyResponse(brakeCurve, brakeTravel) : brakeTravel;
        handbrake = actions.Driving.Handbrake.IsPressed();

        lastSample = new DrivingInput(steer, throttle, brake, handbrake, ConsumeShift());
        return lastSample;
    }
}
