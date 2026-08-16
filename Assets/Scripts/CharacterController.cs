using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;

public class CharacterController : MonoBehaviour
{
    public Transform characterModel;
    public Camera characterCamera;
    public float speed = 1f;
    public float RideHeight = 1.5f;
    public float RideSpringStrength = 5f;
    public float RideSpringDamper = 1.5f;

    public Vector3 _uprightJointTargetRot;

    private Rigidbody _RB;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _RB = GetComponent<Rigidbody>();
    }

    // Update is called once per frame
    void Update()
    {
        var keyboard = Keyboard.current;
        characterCamera.transform.LookAt(characterModel.position);
        //characterCamera.transform.position = new Vector3(characterModel.position.x, characterModel.position.y + 2.5f, characterModel.position.z -4f);
        if(keyboard == null)
        {
            return;
        }

        HandleMovement(keyboard, Mouse.current);


        Vector3 rayOrigin = transform.position;
        Vector3 rayDirection = -transform.up;
        float maxDistance = 2f;

        if(Physics.Raycast(rayOrigin, rayDirection, out RaycastHit hit, maxDistance))
        {
            Vector3 vel = _RB.linearVelocity;
            Vector3 rayDir = transform.TransformDirection(rayDirection);

            Vector3 otherVel = Vector3.zero;
            Rigidbody hitBody = hit.rigidbody;
            if(hitBody != null)
            {
                otherVel = hitBody.linearVelocity;
            }

            float rayDirVel = Vector3.Dot(rayDir, vel);
            float otherDirVel = Vector3.Dot(rayDir, otherVel);

            float relVel = rayDirVel - otherDirVel;

            float x = hit.distance - RideHeight;

            float springForce = (x * RideSpringStrength) - (relVel * RideSpringDamper);

            _RB.AddForce(rayDir * springForce);
            if(hitBody != null)
            {
                hitBody.AddForceAtPosition(rayDir * -springForce, hit.point);
            }
        }
    }

    public void UpdateUprightForce(float elapsed)
    {
        Quaternion characterCurrent = transform.rotation;
        Quaternion toGoal = GetShortestRotation(characterCurrent, _uprightJointTargetRot);

        Vector3 rotAxis;
        float rotDegrees;

        toGoal.ToAngleAxis(out rotDegrees, out rotAxis);
        rotAxis.Normalize();

        float rotRadians = rotDegrees * Mathf.Deg2Rad;

        _RB.AddTorque((rotAxis * (rotRadians * RideSpringStrength)) - (_RB.angularVelocity * RideSpringDamper));
    }


    private void HandleMovement(Keyboard keyboard, Mouse mouse)
    {
        Vector3 input = new Vector3(
            (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f),
            keyboard.spaceKey.isPressed ? 3f : 0f,
            (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f)  
        );

        if(input != Vector3.zero)
        {
            transform.position += transform.TransformDirection(input.normalized) * speed * Time.fixedDeltaTime;
        }
    }

    public Quaternion GetShortestRotation(Quaternion currentRotation, Vector3 goalRotation)
    {
        Quaternion goalQuaternion = Quaternion.Euler(goalRotation);
        Quaternion shortestRotation = goalQuaternion * Quaternion.Inverse(currentRotation);
        return shortestRotation;
    }
}
