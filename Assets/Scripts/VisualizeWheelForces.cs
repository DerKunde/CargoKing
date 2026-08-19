using UnityEngine;

public class VisualizeWheelForces : MonoBehaviour
{
    public Transform wheelOrigin;
    public Suspension suspension;

    public Vector3 springForce = new Vector3(0f, 0f, 0f);
    public Vector3 accelerationForce = new Vector3(0f, 0f, 1f);
    public Vector3 gripForce = new Vector3(1f, 0f, 0f);
    public Vector3 tireWorldVelocity = new Vector3(0f, 0f, 0f);
    public float scale = 0.1f;

    public Vector3 tireSteeringDirection = new Vector3(0f,0f,0f);



    private void DrawWheelForward()
    {
        Gizmos.color = Color.blue;
        Vector3 origin = suspension.transform.position;
        Vector3 end = origin + suspension.transform.forward * scale;

        Gizmos.DrawLine(origin, end);
    }

    private void DrawWheelUp()
    {
        Gizmos.color = Color.darkGreen;
        Vector3 origin = suspension.transform.position;
        Vector3 end = origin + wheelOrigin.transform.up * scale;

        Gizmos.DrawLine(origin, end);
    }

    private void DrawWheelBackward()
    {
        Gizmos.color = Color.red;
        Vector3 origin = suspension.transform.position;
        Vector3 end = origin + -suspension.transform.forward * scale;

        Gizmos.DrawLine(origin, end);
    }

    private void DrawSpringForce()
    {
        Gizmos.color = Color.green;
        Vector3 origin = wheelOrigin.position;
        springForce = suspension.suspensionForce;
        Vector3 end = origin + springForce * scale;

        Gizmos.DrawLine(origin, end);
    }

    private void DrawTireSteeringDirection()
    {
        Gizmos.color = Color.blue;
        Vector3 origin = wheelOrigin.position;
        tireSteeringDirection = suspension.steeringDirection;
        Vector3 end = origin + tireSteeringDirection * scale;

        Gizmos.DrawLine(origin, end);
    }

    private void DrawAccelerationForce()
    {
        Gizmos.color = Color.blue;
        Vector3 origin = wheelOrigin.position;
        Vector3 end = origin + accelerationForce * scale;

        Gizmos.DrawLine(origin, end);
    }

    private void DrawGripForce()
    {
        Gizmos.color = Color.red;
        Vector3 origin = wheelOrigin.position;
        gripForce = suspension.tireSlip;
        Vector3 end = origin + gripForce * scale;

        Gizmos.DrawLine(origin, end);
    }

    private void DrawTireWorldVelocity()
    {
        Gizmos.color = Color.darkRed;
        Vector3 origin = wheelOrigin.position;
        tireWorldVelocity = suspension.tireSlip;
        Vector3 end = origin + tireWorldVelocity * scale;

        Gizmos.DrawLine(origin, end);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawSphere(wheelOrigin.position, 0.05f);
        DrawSpringForce();
        DrawAccelerationForce();
        DrawGripForce();
        DrawTireWorldVelocity();
        DrawTireSteeringDirection();
    }
}
