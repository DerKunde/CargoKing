using Unity.VisualScripting;
using UnityEditor.EngineDiagnostics;
using UnityEngine;
using UnityEngine.InputSystem;

public class CarController : MonoBehaviour
{
    public Rigidbody carBody;
    public CarEngine carEngine;
    public Transform frontLeftWheel;
    public Transform frontRightWheel;
    public Transform rearLeftWheel;
    public Transform rearRightWheel;

    public float maxSteerAngle = 25f;
    private float steeringTime = 0f;
    private const float MAX_TIME = 0.3f;
    private float currentTimeOnPedle = 0f;

    [Header("Curves")]
    public AnimationCurve steeringInputCurve;

    void FixedUpdate()
    {
        var keyboard = Keyboard.current;
        if(keyboard == null)
        {
            return;
        }

        float forwardSpeedMS = Vector3.Dot(carBody.linearVelocity, transform.forward);
        float currentRPM = carEngine.CalculateRPM(forwardSpeedMS, carEngine.currentGear);

        if (keyboard.wKey.isPressed)
        {
            currentTimeOnPedle += Time.fixedDeltaTime;
            currentTimeOnPedle = Mathf.Clamp(currentTimeOnPedle, 0f, 1f);
        }
        else
        {
            currentTimeOnPedle = 0f;
        }

        float wheelTorque = 0f;
        if (keyboard.wKey.isPressed)
        {
            wheelTorque = carEngine.CalculateWheelTorque(currentRPM, carEngine.currentGear, carEngine.CalculateGasInput(currentTimeOnPedle));
        }
        else
        {
            //Just dummy now!!!
            wheelTorque = 0f;
        }

        float forceInNewton = wheelTorque / carEngine.tireRadius;
        carBody.AddForceAtPosition(transform.forward * forceInNewton * 0.01f, rearLeftWheel.position);
        carBody.AddForceAtPosition(transform.forward * forceInNewton * 0.01f, rearRightWheel.position);

        bool isSteering = keyboard.aKey.isPressed || keyboard.dKey.isPressed;
        if (isSteering)
        {
            steeringTime += Time.fixedDeltaTime;
        }
        else
        {
            steeringTime -= Time.fixedDeltaTime;
        }

        steeringTime = Mathf.Clamp(steeringTime, 0f, MAX_TIME);
        float currentCurveValue = GetSmoothedSteeringAngle(steeringTime);

        if (keyboard.aKey.isPressed)
        {
            float currentSteeringAngle = maxSteerAngle * currentCurveValue;
            frontLeftWheel.localEulerAngles = new Vector3(0,-currentSteeringAngle,0);
            frontRightWheel.localEulerAngles = new Vector3(0,-currentSteeringAngle,0);
        }
        else if (keyboard.dKey.isPressed)
        {
            float currentSteeringAngle = maxSteerAngle * currentCurveValue;
            frontLeftWheel.localEulerAngles = new Vector3(0,currentSteeringAngle,0);
            frontRightWheel.localEulerAngles = new Vector3(0,currentSteeringAngle,0);
        }
        else
        {
            float lastUsedMaxAngle = (frontLeftWheel.localEulerAngles.y > 180f || frontLeftWheel.localEulerAngles.y < 0f) ? -maxSteerAngle : maxSteerAngle;

            float currentAngle = lastUsedMaxAngle * currentCurveValue;

            if(steeringTime <= 0)
            {
                currentAngle = 0f;
            }
            frontLeftWheel.localEulerAngles = new Vector3(0, currentAngle, 0);
            frontRightWheel.localEulerAngles = new Vector3(0, currentAngle, 0);
        }

        if (keyboard.sKey.isPressed)
        {
            frontLeftWheel.GetComponent<Suspension>().CalculateBraking();
            frontRightWheel.GetComponent<Suspension>().CalculateBraking();
        }

        if (keyboard.rKey.isPressed)
        {
            ResetCar();
        }
    }

    private float GetSmoothedSteeringAngle(float steeringTime)
    {
        return steeringInputCurve.Evaluate(steeringTime);
    }

    private void ResetCar()
    {
        transform.Translate(0, 0.3f, 0);
        Vector3 currentEuler = transform.localEulerAngles;
        currentEuler.z = 0f;
        transform.localEulerAngles = currentEuler;
    }
}
