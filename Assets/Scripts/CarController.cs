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

    [Header("Balance")]
    [Tooltip("Set the centre of mass explicitly instead of letting Unity derive it from the colliders.")]
    public bool overrideCenterOfMass = true;

    /// <summary>
    /// Centre of mass in body local space. Height is what decides whether a wheel lifts: the inside
    /// pair leaves the road at a lateral acceleration of g * track / (2 * height above ground), so
    /// halving the height doubles the margin. Unity's implicit centre sits wherever the colliders
    /// happen to average out, which for a body box is far too high.
    /// </summary>
    [Tooltip("Centre of mass in body local space. Lower means less roll and later wheel lift.")]
    public Vector3 centerOfMass = Vector3.zero;

    /// <summary>
    /// Anti roll bar strength in newtons per unit of difference in strut extension between the two
    /// wheels of an axle.
    ///
    /// Worth being clear about what this does and does not do: it takes the lean out of the body, but
    /// it does not stop a wheel lifting - it raises the load transfer at the axle it stiffens. That is
    /// also why the front bar is the stronger one here: more load transfer at the front makes the car
    /// run wide rather than snap round, which is the forgiving way for a delivery vehicle to fail.
    /// </summary>
    [Header("Anti Roll")]
    public float frontAntiRollStiffness = 2500f;

    public float rearAntiRollStiffness = 1200f;

    private Suspension[] brakedWheels;
    private Suspension frontLeftSuspension;
    private Suspension frontRightSuspension;
    private Suspension rearLeftSuspension;
    private Suspension rearRightSuspension;

    private void Awake()
    {
        // Looked up once instead of on every physics step.
        frontLeftSuspension = frontLeftWheel.GetComponent<Suspension>();
        frontRightSuspension = frontRightWheel.GetComponent<Suspension>();
        rearLeftSuspension = rearLeftWheel.GetComponent<Suspension>();
        rearRightSuspension = rearRightWheel.GetComponent<Suspension>();

        brakedWheels = new[]
        {
            frontLeftSuspension,
            frontRightSuspension,
            rearLeftSuspension,
            rearRightSuspension,
        };

        ApplyCenterOfMass();
    }

    private void OnValidate()
    {
        ApplyCenterOfMass();
    }

    private void FixedUpdate()
    {
        // Separate from Drive(): the bars work off suspension travel, not off driver input, and have
        // to keep working while nobody is steering.
        ApplyAntiRoll(frontLeftSuspension, frontRightSuspension, frontAntiRollStiffness);
        ApplyAntiRoll(rearLeftSuspension, rearRightSuspension, rearAntiRollStiffness);
    }

    private void ApplyCenterOfMass()
    {
        if (carBody == null)
        {
            return;
        }

        if (overrideCenterOfMass)
        {
            carBody.centerOfMass = centerOfMass;
        }
        else
        {
            carBody.automaticCenterOfMass = true;
        }
    }

    /// <summary>
    /// Applies the moment of one anti roll bar: the more the two wheels of an axle have parted, the
    /// harder it pushes the compressed side up and the extended side down.
    /// </summary>
    private void ApplyAntiRoll(Suspension left, Suspension right, float stiffness)
    {
        if (left == null || right == null || stiffness <= 0f)
        {
            return;
        }

        float force = (left.ExtensionRatio - right.ExtensionRatio) * stiffness;

        // Only a wheel on the ground has anything to push against. A bar with one wheel in the air
        // would otherwise shove the body around with nothing carrying the reaction.
        if (left.isGrounded)
        {
            carBody.AddForceAtPosition(left.transform.up * -force, left.transform.position);
        }

        if (right.isGrounded)
        {
            carBody.AddForceAtPosition(right.transform.up * force, right.transform.position);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!overrideCenterOfMass)
        {
            return;
        }

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.TransformPoint(centerOfMass), 0.08f);
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
            // ChangeGear wants m/s. Not carEngine.speedInKmH: wrong unit, and it is only
            // written further down in Drive().
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
        // Handed over every step, not only while the pedal is down - letting go has to reach
        // the wheels too. Suspension builds the force, where normal force and contact point
        // are known.
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
