using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class CarEngine : MonoBehaviour
{
    public AnimationCurve torqueCurve;
    public AnimationCurve gasPadleCurve;
    public float[] gears = {3.2f, 1.9f, 1.3f, 1.0f, 0.8f};
    public float axleRatio = 4f;
    public float efficiency = 0.85f;
    public float tireRadius = 0.31f;
    public float idleRevolutions = 1000f;
    public float maxRevolutions = 6000f;
    public float rpmChangeSpeed = 3000f;

    private bool isOnGasPadle = false;

    [Header("Calculated Values !!! Do not change !!!")]
    public float revolutionsPerMinute;
    public float speedInMS;
    public float calculatedTorque = 0f;
    public float currentTimeOnPedle = 0f;
    public int currentGear = 1;

    public float CalculateRPM(float currentSpeedMS, int currentGear)
    {
        float tireCircumference = 2 * (float)Math.PI * tireRadius;

        float tireRPM = (currentSpeedMS / tireCircumference) * 60;
        float calculatedRPM = tireRPM * axleRatio * gears[currentGear - 1];

        if(calculatedRPM < idleRevolutions)
        {
            calculatedRPM = idleRevolutions;
        }
        if(calculatedRPM > maxRevolutions)
        {
            calculatedRPM = maxRevolutions;
        }

        revolutionsPerMinute = calculatedRPM;
        return calculatedRPM;
    }

    public float CalculateWheelTorque(float currentRPM, int currentGear, float gasPadleValue)
    {
        float maxPossibleTorque = GetMaxTorqueForRPM(currentRPM);
        float currentTorque = maxPossibleTorque * gasPadleValue;

        float wheelTorque = currentTorque * gears[currentGear - 1] * axleRatio * efficiency;
        return wheelTorque;
    }

    public float CalculateGasInput(float timeOnPadle)
    {
        if(timeOnPadle >= 1)
        {
            timeOnPadle = 1;
        }

        return gasPadleCurve.Evaluate(timeOnPadle);
    }

    private float GetMaxTorqueForRPM(float currentRPM)
    {
        return LookUpOnTorqueCurve(torqueCurve, currentRPM);
    }

    private void ChangeGear(GearChange direction)
    {
        if(direction == GearChange.Up && currentGear + 1 < gears.Length)
        {
            currentGear += 1;
        }

        if(direction == GearChange.Down && currentGear - 1 > 1)
        {
            currentGear -= 1;
        }
    }

    void FixedUpdate()
    {
        var keyboard = Keyboard.current;

        if (keyboard.qKey.wasPressedThisFrame)
        {
            ChangeGear(GearChange.Down);
        }

        if (keyboard.eKey.wasPressedThisFrame)
        {
            ChangeGear(GearChange.Up);
        }
    }

    enum GearChange
    {
        Up,
        Down,
    }


    //Dont know about this but for now we will stick with it
    private int rpmFactor = 100000;
    private int torqueFactor = 10000;

    private float LookUpOnTorqueCurve(AnimationCurve curve, float xValueToLookAt)
    {
        return curve.Evaluate(xValueToLookAt * rpmFactor) * torqueFactor;
    }

}