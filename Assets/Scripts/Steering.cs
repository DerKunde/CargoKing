using UnityEngine;

public class Steering : MonoBehaviour
{

    public Rigidbody carBody;
    public Transform tireTransform;
    public float tireGripFactor = 0.9f;
    public float tireMass = 1f;


    void FixedUpdate()
    {
        Vector3 steeringDirection = tireTransform.right;
        Vector3 tireWorldVelocity = carBody.GetPointVelocity(tireTransform.position);

        float steeringValue = Vector3.Dot(steeringDirection, tireWorldVelocity);

        float desiredVelocityChange = -steeringValue * tireGripFactor;
        float desiredAcceleration = desiredVelocityChange / Time.fixedDeltaTime;

        carBody.AddForceAtPosition(steeringDirection * tireMass * desiredAcceleration, tireTransform.position);
    }
}
