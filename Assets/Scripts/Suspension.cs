using System;
using UnityEngine;

public class Suspension : MonoBehaviour
{
    [Header("References")]
    public Rigidbody carBody;

    [Header("Suspension Settings")]
    private float restLength = 0.4f;
    public float springStrength = 300;
    public float damping = 25;
    public float tireGripFactor = 0.9f;
    public float powerScale = 0.1f;
    public float brakingPower = 10f;
    private float tireMass = 1f;
    private float wheelRadius = 0.1f;
    public LayerMask groundMask = ~0;

    [Header("Wheel Visual")]
    public Transform wheelMesh;

    float _lastForce;

    public Vector3 suspensionForce;
    // Querachse des Reifens: die Drehachse des Rades (wheelMesh.up).
    // Achse, keine Richtung - das Vorzeichen ist fuer die Kraftberechnung ohne Belang.
    public Vector3 lateralAxis;
    public Vector3 tireWorldVelocity;
    public Vector3 tireSlip;

    [Header("Debug / Visualisierung (nur Anzeige, keine Physik)")]
    public Vector3 rollDirection;
    public Vector3 tireRollForce;
    public Vector3 tireForce;

    public bool isGrounded;

    // Angriffspunkte: genau die Positionen, die an AddForceAtPosition uebergeben werden,
    // aber relativ zum Federbein gespeichert. Als Weltkoordinate eingefroren wuerden sie
    // einen Physikschritt hinterherhaengen: FixedUpdate laeuft vor dem Solver, gezeichnet
    // wird gegen die Pose danach.
    private Vector3 _suspensionForcePointLocal;
    private Vector3 _tireForcePointLocal;
    private Vector3 _contactPointLocal;

    public Vector3 suspensionForcePoint => transform.TransformPoint(_suspensionForcePointLocal);
    public Vector3 tireForcePoint => transform.TransformPoint(_tireForcePointLocal);
    public Vector3 contactPoint => transform.TransformPoint(_contactPointLocal);

    // Dasselbe Problem betrifft die Richtungen: die Vektoren unten sind Weltvektoren,
    // eingefroren vor dem Solve. Beim Lenken und Wanken zeigen sie sonst einen Schritt
    // zu spaet. _debugFrame haelt die Pose fest, in der sie geschrieben wurden.
    private Quaternion _debugFrame = Quaternion.identity;

    private Vector3 ToCurrentPose(Vector3 frozenWorldVector)
    {
        return transform.rotation * Quaternion.Inverse(_debugFrame) * frozenWorldVector;
    }

    public Vector3 displaySuspensionForce  => ToCurrentPose(suspensionForce);
    public Vector3 displayLateralAxis => ToCurrentPose(lateralAxis);
    public Vector3 displayRollDirection     => ToCurrentPose(rollDirection);
    public Vector3 displayTireRollForce     => ToCurrentPose(tireRollForce);
    public Vector3 displayTireSlipForce     => ToCurrentPose(tireSlip);
    public Vector3 displayTireForce         => ToCurrentPose(tireForce);

    public AnimationCurve torqueCurve;
    public AnimationCurve gasPadelCurve;

    void Awake()
    {
        if (wheelMesh != null)
        {
            wheelRadius = wheelMesh.localScale.z / 2; // Unit in meters
            tireMass = carBody.mass / 4;
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
            _contactPointLocal = transform.InverseTransformPoint(hit.point);

            Vector3 springForcePoint = transform.position;
            _suspensionForcePointLocal = transform.InverseTransformPoint(springForcePoint);
            carBody.AddForceAtPosition(transform.up * (float)force, springForcePoint);
            wheelMesh.position = transform.position - transform.up * (hit.distance - wheelRadius);

            Vector3 gripForcePoint = wheelMesh.position;
            _tireForcePointLocal = transform.InverseTransformPoint(gripForcePoint);
            carBody.AddForceAtPosition(CalculateSteeringAndGrip(), gripForcePoint);

            _debugFrame = transform.rotation;
        }
        else
        {
            // Rad in der Luft: Anzeige-Werte zuruecksetzen, sonst stehen alte Pfeile stehen.
            isGrounded = false;
            _debugFrame = transform.rotation;
            suspensionForce = Vector3.zero;
            tireSlip = Vector3.zero;
            tireRollForce = Vector3.zero;
            tireForce = Vector3.zero;
        }
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

    private Vector3 CalculateSteeringAndGrip()
    {
    lateralAxis = wheelMesh.up;
    rollDirection     = wheelMesh.forward;
    tireWorldVelocity = carBody.GetPointVelocity(transform.position);

    float steeringValue        = Vector3.Dot(lateralAxis, tireWorldVelocity);
    float desiredVelocityChange = -steeringValue * tireGripFactor;
    float desiredAcceleration   = desiredVelocityChange / Time.fixedDeltaTime;

    float effMass = EffectiveMassAt(transform.position, lateralAxis);
    tireSlip = lateralAxis * (effMass * desiredAcceleration);

    // Dieses Modell kennt nur die Seitenkraft: keine Rollkomponente, kein Grip-Clamp.
    tireRollForce = Vector3.zero;
    tireForce     = tireSlip + tireRollForce;

    return tireSlip;
    }

    private float EffectiveMassAt(Vector3 point, Vector3 dir)
    {
    Vector3 r    = point - carBody.worldCenterOfMass;
    Vector3 rxd  = Vector3.Cross(r, dir);

    Quaternion tensorRot = carBody.rotation * carBody.inertiaTensorRotation;
    Vector3 local        = Quaternion.Inverse(tensorRot) * rxd;
    Vector3 it           = carBody.inertiaTensor;
    Vector3 scaled       = new Vector3(local.x / it.x, local.y / it.y, local.z / it.z);

    float angular = Vector3.Dot(rxd, tensorRot * scaled);
    return 1f / (1f / carBody.mass + angular);
    }

    public void CalculateBraking()
    {
        carBody.AddForceAtPosition(-transform.forward * brakingPower, transform.position);
    }
}