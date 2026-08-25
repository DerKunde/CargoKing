using UnityEngine;

public class CarController : MonoBehaviour
{
    public Rigidbody carBody;
    public CarEngine carEngine;
    public Transform frontLeftWheel;
    public Transform frontRightWheel;
    public Transform rearLeftWheel;
    public Transform rearRightWheel;

    public float maxSteerAngle = 25f;

    [Header("Curves")]
    public AnimationCurve steeringInputCurve;

    private Suspension[] brakedWheels;

    private void Awake()
    {
        // Looked up once instead of on every physics step.
        brakedWheels = new[]
        {
            frontLeftWheel.GetComponent<Suspension>(),
            frontRightWheel.GetComponent<Suspension>(),
            rearLeftWheel.GetComponent<Suspension>(),
            rearRightWheel.GetComponent<Suspension>(),
        };
    }

    public void Drive(in DrivingInput input)
    {
        ApplyGearShift(input.Shift);
        ApplyThrottle(input.Throttle);
        ApplySteering(input.Steer);
        ApplyBraking(input.Brake);

        carEngine.speedInKmH = Vector3.Dot(carBody.linearVelocity, transform.forward) * 3.6f;
    }

    private void ApplyThrottle(float throttle)
    {
        float totalWheelTorqueInNewton = carEngine.CalculateWheelTorque(throttle, Vector3.Dot(carBody.linearVelocity, transform.forward));
        if(throttle > 0f)
        {
            carBody.AddForceAtPosition(transform.forward * totalWheelTorqueInNewton / 2, rearLeftWheel.position);
            carBody.AddForceAtPosition(transform.forward * totalWheelTorqueInNewton / 2, rearRightWheel.position);   
        }
    }

    private void ApplyGearShift(GearShift shift)
    {
        if(shift != GearShift.None)
        {
            // speedInKmH is km/h and is written further down in Drive(), so it was both
            // the wrong unit and a frame stale. ChangeGear wants m/s.
            carEngine.ChangeGear(shift, CarSpeedInMS());
        }
    }

    private void ApplySteering(float steer)
    {
        float steerAngle = maxSteerAngle * steer;
        frontLeftWheel.localEulerAngles = new Vector3(0, steerAngle, 0);
        frontRightWheel.localEulerAngles = new Vector3(0, steerAngle, 0);
    }

    private void ApplyBraking(float brake)
    {
        // Handed over every step, not only while the pedal is down - letting go has
        // to reach the wheels as well. The force itself is built in Suspension, where
        // normal force and contact point are known.
        foreach (Suspension wheel in brakedWheels)
        {
            wheel.SetBrakeInput(brake);
        }
    }

    public void ResetCar()
    {
        transform.Translate(0, 0.3f, 0);
        Vector3 currentEuler = transform.localEulerAngles;
        currentEuler.z = 0f;
        transform.localEulerAngles = currentEuler;
    }

    public float MaxBrakeDecelartion()
    {
        float total = 0f;
        foreach(Suspension wheel in brakedWheels)
        {
            total += wheel.AvailableBrakeForce();
        }
        return total / carBody.mass;
    }

    public float CarSpeedInMS()
    {
        return Mathf.Abs(Vector3.Dot(carBody.linearVelocity, transform.forward));
    }
}
