using System;
using UnityEngine;

public class Suspension : MonoBehaviour
{
    [Header("References")]
    public Rigidbody carBody;

    [Header("Suspension Settings")]
    private float restLength = 0.45f;
    public float springStrength = 300;
    public float damping = 25;
    public float tireGripFactor = 0.9f;
    public float powerScale = 0.1f;
    public float brakingPower = 10f;

    [Header("Rollwiderstand")]
    // Realistischer Bereich fuer Reifen auf Asphalt. Bei 800 kg sind das rund 118 N
    // Gesamtwiderstand - der Antrieb liefert im Leerlauf nur ~213 N, viel hoeher darf
    // Crr also nicht werden, sonst kommt das Auto nicht vom Fleck.
    public float rollingResistanceCoefficient = 0.015f;
    private float tireMass = 1f;
    private float wheelRadius = 0.1f;
    public LayerMask groundMask = ~0;

    [Header("Wheel Visual")]
    public Transform wheelMesh;
    public Transform wheelmeshToRotate;

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

    // Die beiden Obergrenzen des Rollwiderstands einzeln, damit im Gizmo sichtbar wird,
    // welche gerade bindet: der Reibwert oder der Anti-Rueckwaerts-Clamp beim Anhalten.
    public float rollResistanceLimit;
    public float rollStopLimit;
    public bool rollForceAtStopLimit;

    public bool isGrounded;

    // Angriffspunkte: genau die Positionen, die an AddForceAtPosition uebergeben werden,
    // aber relativ zum Federbein gespeichert. Als Weltkoordinate eingefroren wuerden sie
    // einen Physikschritt hinterherhaengen: FixedUpdate laeuft vor dem Solver, gezeichnet
    // wird gegen die Pose danach.
    private Vector3 _suspensionForcePointLocal;
    private Vector3 _tireForcePointLocal;
    private Vector3 _contactPointLocal;
    private float _spinAngle;
    private Quaternion _baseLocalRotation;

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
            _contactPointLocal = transform.InverseTransformPoint(hit.point);

            Vector3 springForcePoint = transform.position;
            _suspensionForcePointLocal = transform.InverseTransformPoint(springForcePoint);
            carBody.AddForceAtPosition(transform.up * (float)force, springForcePoint);
            wheelMesh.position = transform.position - transform.up * (hit.distance - wheelRadius);

            Vector3 gripForcePoint = wheelMesh.position;
            _tireForcePointLocal = transform.InverseTransformPoint(gripForcePoint);
            carBody.AddForceAtPosition(CalculateTireForces(gripForcePoint), gripForcePoint);

            _debugFrame = transform.rotation;
            VisualWheelRotation(rollDirection, tireWorldVelocity);
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
            rollResistanceLimit = 0f;
            rollStopLimit = 0f;
            rollForceAtStopLimit = false;
        }
    }

    private void VisualWheelRotation(Vector3 rollDirection, Vector3 tireWorldVelocity)
    {
        float v = Vector3.Dot(rollDirection, tireWorldVelocity);
        float omega = v / wheelRadius;
        float deltaDeg = omega * Time.fixedDeltaTime *  Mathf.Rad2Deg;
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

    // forcePoint ist der Punkt, an dem der Aufrufer die Kraft ansetzt. Geschwindigkeit
    // und Effektivmasse MUESSEN am selben Punkt genommen werden, sonst passen Hebelarm
    // und Kraft nicht zusammen und die Kompensation ist systematisch daneben.
    private Vector3 CalculateTireForces(Vector3 forcePoint)
    {
    lateralAxis = wheelMesh.up;
    rollDirection     = wheelMesh.forward;
    tireWorldVelocity = carBody.GetPointVelocity(forcePoint);

    float steeringValue        = Vector3.Dot(lateralAxis, tireWorldVelocity);
    float desiredVelocityChange = -steeringValue * tireGripFactor;
    float desiredAcceleration   = desiredVelocityChange / Time.fixedDeltaTime;

    float effMass = EffectiveMassAt(forcePoint, lateralAxis);
    tireSlip = lateralAxis * (effMass * desiredAcceleration);

    // Rollwiderstand: konstante Kraft entgegen der Rollrichtung. Nur eine konstante
    // Kraft bringt das Auto in endlicher Zeit zum Stehen - eine geschwindigkeits-
    // proportionale Daempfung faellt mit v gegen Null und kriecht ewig weiter.
    float forwardVel  = Vector3.Dot(rollDirection, tireWorldVelocity);
    float normalForce = Mathf.Max(0f, suspensionForce.y);

    // Anti-Rueckwaerts-Clamp: nie mehr Kraft als noetig, um forwardVel in diesem Schritt
    // auf Null zu bringen. Ohne das schiebt der Rollwiderstand das stehende Auto
    // rueckwaerts und es zittert um Null. Bei forwardVel == 0 wird stopForce == 0, damit
    // spielt auch Mathf.Sign(0) == 1 keine Rolle mehr.
    //
    // Bezugsmasse ist tireMass (= carBody.mass / 4), NICHT EffectiveMassAt: hier wirken
    // vier Raeder gleichzeitig auf denselben Koerper, jedes darf also nur seinen Anteil
    // am Gesamtimpuls beanspruchen. Mit EffectiveMassAt (5.6 kg pro Rad bei 10 kg Auto)
    // bremsen die vier zusammen mit dem 2.2-fachen des noetigen Impulses und das Auto
    // schwingt um Null. Sind Raeder in der Luft, wird untersteuert geklemmt - unkritisch.
    rollResistanceLimit  = rollingResistanceCoefficient * normalForce;
    rollStopLimit        = Mathf.Abs(forwardVel) * tireMass / Time.fixedDeltaTime;
    rollForceAtStopLimit = rollStopLimit < rollResistanceLimit;

    float rollMagnitude = Mathf.Min(rollResistanceLimit, rollStopLimit);

    tireRollForce = -Mathf.Sign(forwardVel) * rollDirection * rollMagnitude;
    tireForce     = tireSlip + tireRollForce;

    return tireForce;
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