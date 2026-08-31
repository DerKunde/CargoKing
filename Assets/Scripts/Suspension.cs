using UnityEngine;

public class Suspension : MonoBehaviour
{
    [Header("References")]
    public Rigidbody carBody;

    [Header("Suspension Settings")]
    private float restLength = 0.6f;
    public float springStrength = 300;
    public float damping = 25;
    public float tireGripFactor = 0.9f;
    [Header("Brakes")]
    public float maxBrakeForce = 2200f;
    public float brakeFrictionCoefficient = 0.9f;

    [SerializeField] private float brakeInput;

    [Header("Rollwiderstand")]
    public float rollingResistanceCoefficient = 0.015f;
    private float tireMass = 1f;
    private float wheelRadius = 0.1f;
    public LayerMask groundMask = ~0;

    [Header("Wheel Visual")]
    public Transform wheelMesh;
    public Transform wheelmeshToRotate;

    public Vector3 suspensionForce;
    public Vector3 lateralAxis;
    public Vector3 tireWorldVelocity;
    public Vector3 tireSlip;

    [Header("Debug / Visualisierung (nur Anzeige, keine Physik)")]
    public Vector3 rollDirection;
    public Vector3 tireLongitudinalForce;
    public float brakeForceDemand;
    public float gripLimit;
    public Vector3 tireForce;
    public float rollResistanceLimit;
    public float rollStopLimit;
    public bool rollForceAtStopLimit;

    public bool isGrounded;

    /// <summary>
    /// How far the strut is extended: 0 fully compressed, 1 at rest length or in the air. The
    /// anti roll bar reads it to see how far the two wheels of an axle have parted.
    /// </summary>
    public float ExtensionRatio => extensionRatio;

    private float extensionRatio = 1f;
    private Vector3 _suspensionForcePointLocal;
    private Vector3 _tireForcePointLocal;
    private Vector3 _contactPointLocal;
    private float _spinAngle;
    private Quaternion _baseLocalRotation;

    public Vector3 suspensionForcePoint => transform.TransformPoint(_suspensionForcePointLocal);
    public Vector3 tireForcePoint => transform.TransformPoint(_tireForcePointLocal);
    public Vector3 contactPoint => transform.TransformPoint(_contactPointLocal);
    private Quaternion _debugFrame = Quaternion.identity;

    private Vector3 ToCurrentPose(Vector3 frozenWorldVector)
    {
        return transform.rotation * Quaternion.Inverse(_debugFrame) * frozenWorldVector;
    }

    public Vector3 displaySuspensionForce => ToCurrentPose(suspensionForce);
    public Vector3 displayLateralAxis => ToCurrentPose(lateralAxis);
    public Vector3 displayRollDirection => ToCurrentPose(rollDirection);
    public Vector3 displayTireLongitudinalForce => ToCurrentPose(tireLongitudinalForce);
    public Vector3 displayTireSlipForce => ToCurrentPose(tireSlip);
    public Vector3 displayTireForce => ToCurrentPose(tireForce);

    void Awake()
    {
        if (wheelMesh != null)
        {
            wheelRadius = wheelMesh.localScale.z / 2; // Unit in meters
            tireMass = carBody.mass / 4;
            _baseLocalRotation = wheelmeshToRotate.localRotation;
        }
    }

    void FixedUpdate()
    {
        Vector3 springDirection = transform.up;

        Vector3 origin = transform.position;
        Vector3 rayDirection = -transform.up;
        float maxDist = restLength + wheelRadius;

        if (Physics.Raycast(origin, rayDirection, out RaycastHit hit, maxDist, groundMask))
        {
            float offset = restLength - (hit.distance - wheelRadius);

            Vector3 tireWorldVelocity = carBody.GetPointVelocity(wheelMesh.position);
            float velocity = Vector3.Dot(springDirection, tireWorldVelocity);

            float force = CalculateSpringForce(offset, velocity);

            isGrounded = true;
            extensionRatio = Mathf.Clamp01((hit.distance - wheelRadius) / restLength);
            _contactPointLocal = transform.InverseTransformPoint(hit.point);

            Vector3 springForcePoint = transform.position;
            _suspensionForcePointLocal = transform.InverseTransformPoint(springForcePoint);
            carBody.AddForceAtPosition(transform.up * (float)force, springForcePoint);
            wheelMesh.position = transform.position - transform.up * (hit.distance - wheelRadius);
            wheelmeshToRotate.position = transform.position - transform.up * (hit.distance - wheelRadius);


            Vector3 gripForcePoint = wheelMesh.position;
            _tireForcePointLocal = transform.InverseTransformPoint(gripForcePoint);
            carBody.AddForceAtPosition(CalculateTireForces(gripForcePoint), gripForcePoint);

            _debugFrame = transform.rotation;
            VisualWheelRotation(rollDirection, tireWorldVelocity);
        }
        else
        {
            // Wheel in the air: reset the display values, or the old arrows stay put.
            isGrounded = false;
            extensionRatio = 1f;
            _debugFrame = transform.rotation;
            suspensionForce = Vector3.zero;
            tireSlip = Vector3.zero;
            tireLongitudinalForce = Vector3.zero;
            brakeForceDemand = 0f;
            gripLimit = 0f;
            tireForce = Vector3.zero;
            rollResistanceLimit = 0f;
            rollStopLimit = 0f;
            rollForceAtStopLimit = false;
        }
    }

    private void VisualWheelRotation(Vector3 rollDirection, Vector3 tireWorldVelocity)
    {
        float v = Vector3.Dot(rollDirection, tireWorldVelocity);
        float omega = v / wheelRadius;
        float deltaDeg = omega * Time.fixedDeltaTime * Mathf.Rad2Deg;
        _spinAngle = Mathf.Repeat(_spinAngle + deltaDeg, 360f);
        wheelmeshToRotate.localRotation = _baseLocalRotation * Quaternion.Euler(0f, -_spinAngle, 0f);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.coral;
        Vector3 origin = wheelMesh.position;
        Vector3 end = origin + new Vector3(0f, wheelMesh.localScale.z / 2, 0f) * -1f;

        Gizmos.DrawLine(origin, end);

        Gizmos.color = Color.yellow;
        Vector3 upperSuspensionJoint = transform.position;
        Vector3 wheelOrigin = wheelMesh.position;
        Gizmos.DrawSphere(upperSuspensionJoint, 0.02f);
        Gizmos.DrawSphere(wheelOrigin, 0.02f);
        Gizmos.DrawLine(upperSuspensionJoint, wheelOrigin);
    }

    public float CalculateSpringForce(float offset, float velocity)
    {
        var force = (offset * springStrength) - (velocity * damping);
        suspensionForce = new Vector3(0f, force, 0f);
        return (offset * springStrength) - (velocity * damping);
    }

    private Vector3 CalculateTireForces(Vector3 forcePoint)
    {
        lateralAxis = wheelMesh.up;
        rollDirection = wheelMesh.forward;
        tireWorldVelocity = carBody.GetPointVelocity(forcePoint);

        float steeringValue = Vector3.Dot(lateralAxis, tireWorldVelocity);
        float desiredVelocityChange = -steeringValue * tireGripFactor;
        float desiredAcceleration = desiredVelocityChange / Time.fixedDeltaTime;

        float effMass = EffectiveMassAt(forcePoint, lateralAxis);
        float lateralDemand = effMass * desiredAcceleration;

        // Rolling resistance is a constant force against the roll direction. Only a constant
        // force stops the car in finite time; damping proportional to v decays with v and
        // creeps on forever.
        float forwardVel = Vector3.Dot(rollDirection, tireWorldVelocity);
        float normalForce = Mathf.Max(0f, suspensionForce.y);

        // Anti-reversal clamp below: never more force than it takes to bring forwardVel to
        // zero this step, otherwise the resistance pushes the standing car backwards and it
        // jitters around zero. At forwardVel == 0 the limit is 0, so Mathf.Sign(0) == 1
        // does no harm.
        //
        // Reference mass is tireMass (= carBody.mass / 4), NOT EffectiveMassAt: four wheels
        // act on the same body, so each may only claim its quarter of the total impulse.
        // With EffectiveMassAt the four together brake with a multiple of the needed
        // impulse and the car oscillates.
        brakeForceDemand = brakeInput * maxBrakeForce;
        rollResistanceLimit = rollingResistanceCoefficient * normalForce;

        // A tire cannot pass on more than its share of the load allows. This is the budget for
        // everything the contact patch does, sideways and lengthways together.
        gripLimit = brakeFrictionCoefficient * normalForce;
        rollStopLimit = Mathf.Abs(forwardVel) * tireMass / Time.fixedDeltaTime;

        // Both are longitudinal resistive forces and are summed BEFORE the clamps. Clamped
        // separately they exceed the stopping impulse together - same failure as above.
        float longitudinalDemand = Mathf.Min(brakeForceDemand + rollResistanceLimit, rollStopLimit);
        rollForceAtStopLimit = rollStopLimit < brakeForceDemand + rollResistanceLimit;

        // Friction circle. One contact patch serves cornering and braking, so both draw on the
        // same budget and are scaled down together when they ask for more than it holds.
        //
        // The lateral force used to have no limit at all - only the longitudinal part was checked
        // against the load. A tire could therefore corner at any force its slip called for, the
        // car pulled well over 1 g, and the load transfer that follows from that lifted the inside
        // wheels off the road in nearly every bend.
        float combined = Mathf.Sqrt(lateralDemand * lateralDemand + longitudinalDemand * longitudinalDemand);
        float scale = combined > gripLimit && combined > 0f ? gripLimit / combined : 1f;

        tireSlip = lateralAxis * (lateralDemand * scale);
        tireLongitudinalForce = -Mathf.Sign(forwardVel) * rollDirection * (longitudinalDemand * scale);
        tireForce = tireSlip + tireLongitudinalForce;

        return tireForce;
    }

    private float EffectiveMassAt(Vector3 point, Vector3 dir)
    {
        Vector3 r = point - carBody.worldCenterOfMass;
        Vector3 rxd = Vector3.Cross(r, dir);

        Quaternion tensorRot = carBody.rotation * carBody.inertiaTensorRotation;
        Vector3 local = Quaternion.Inverse(tensorRot) * rxd;
        Vector3 it = carBody.inertiaTensor;
        Vector3 scaled = new Vector3(local.x / it.x, local.y / it.y, local.z / it.z);

        float angular = Vector3.Dot(rxd, tensorRot * scaled);
        return 1f / (1f / carBody.mass + angular);
    }

    public void SetBrakeInput(float value)
    {
        brakeInput = Mathf.Clamp01(value);
    }

    public float AvailableBrakeForce()
    {
        if (!isGrounded) return 0f;

        float normalForce = Mathf.Max(0f, suspensionForce.y);
        float demand = maxBrakeForce + rollingResistanceCoefficient * normalForce;
        return Mathf.Min(demand, brakeFrictionCoefficient * normalForce);
    }
}
