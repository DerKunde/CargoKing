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
    public Vector3 steeringDirection;
    public Vector3 tireWorldVelocity;
    public Vector3 tireSlip;

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
        float maxDist = restLength + wheelRadius + 0.04f;

        if (Physics.Raycast(origin, rayDirection, out RaycastHit hit, maxDist, groundMask))
        {
            float offset = restLength - (hit.distance - wheelRadius);

            Vector3 tireWorldVelocity = carBody.GetPointVelocity(transform.position);
            float velocity = Vector3.Dot(springDirection, tireWorldVelocity);

            float force = CalculateSpringForce(offset, velocity);
            force = Mathf.Max(force, 0f);

            carBody.AddForceAtPosition(transform.up * (float)force, transform.position);
            CalculateSteeringAndGrip();
            wheelMesh.position = transform.position - transform.up * (hit.distance - wheelRadius);

            carBody.AddForceAtPosition(CalculateSteeringAndGrip(), transform.position);
        }
        else
        {
            wheelMesh.position = origin - transform.up * maxDist;
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

        DrawWheelSteeringGizmo();
    }


    private void DrawWheelSteeringGizmo()
    {
        Gizmos.color = Color.orange;
        Vector3 origin = wheelMesh.position;
        Vector3 end = origin + -carBody.GetPointVelocity(transform.position);
        Gizmos.DrawLine(origin, end);
    }

    public float CalculateSpringForce(float offset, float velocity)
    {
        var force = (offset * springStrength) - (velocity * damping);
        suspensionForce = new Vector3(0f, force, 0f);
        return (offset * springStrength) - (velocity * damping);
    }

    private Vector3 CalculateSteeringAndGrip()
    {
        steeringDirection = wheelMesh.up;
        tireWorldVelocity = carBody.GetPointVelocity(transform.position);

        float steeringValue = Vector3.Dot(steeringDirection, tireWorldVelocity);

        float desiredVelocityChange = -steeringValue * tireGripFactor;
        float desiredAcceleration = desiredVelocityChange / Time.fixedDeltaTime;

        return tireSlip = steeringDirection * tireMass * desiredAcceleration;
    }

    public void Acceleration()
    {
        carBody.AddForceAtPosition(transform.forward * powerScale, transform.position);
    }

    public void CalculateBraking()
    {
        carBody.AddForceAtPosition(-transform.forward * brakingPower, transform.position);
    }
}