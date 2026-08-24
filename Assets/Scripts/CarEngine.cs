using System;
using R3;
using UnityEngine;
using UnityEngine.InputSystem;

public class CarEngine : MonoBehaviour
{
    public AnimationCurve torqueCurve;
    public float[] gears = {3.2f, 1.9f, 1.3f, 1.0f, 0.8f};
    public float axleRatio = 4f;
    public float efficiency = 0.85f;
    public float tireRadius = 0.31f;
    public float idleRevolutions = 1000f;
    public float maxRevolutions = 6000f;
    public float rpmChangeSpeed = 3000f;

    [Header("Anfahrhilfe (vereinfachte Kupplung)")]
    // Die Drehzahl wird sonst rein aus der Raddrehzahl abgeleitet. Beim Anfahren ist
    // die Null, also haengt der Motor auf idleRevolutions fest - und dort liefert die
    // Drehmomentkurve nur 3 Nm. Ein echtes Auto entkoppelt hier mit der Kupplung.
    // Solange Gas anliegt und das Getriebe langsamer dreht, schleift sie und haelt den
    // Motor auf dieser Drehzahl.
    public float launchRevolutions = 2000f;

    [Header("Drehzahlbegrenzer")]
    // Ueber diesen Bereich vor maxRevolutions wird das Moment ausgeblendet. Ohne
    // Begrenzer bleibt das Moment oberhalb maxRevolutions auf dem Wert der Maximal-
    // drehzahl stehen, statt wegzufallen - dann erreicht der KURZE Gang die hoechste
    // Endgeschwindigkeit, weil er staerker uebersetzt. Genau verkehrt herum.
    public float revLimiterFadeRange = 300f;

    [Header("Calculated Values !!! Do not change !!!")]
    public float revolutionsPerMinute;
    public float speedInKmH;
    public float calculatedTorque = 0f;
    public float currentTimeOnPedle = 0f;
    public int currentGear = 1;
    public bool isOnGasPadle = false;
    public bool isClutchSlipping = false;
    public float gearboxRevolutions;
    public float revLimiterFactor = 1f;

    private float CalculateRPM(float currentSpeedMS, bool throttleApplied)
    {
        float tireCircumference = 2 * (float)Math.PI * tireRadius;

        float tireRPM = (currentSpeedMS / tireCircumference) * 60;
        float calculatedRPM = tireRPM * axleRatio * gears[currentGear - 1];

        // Ungeklemmt festhalten: der Begrenzer muss wissen, wie weit das Getriebe
        // ueber die Maximaldrehzahl hinausdreht. Nach dem Clamp unten waere das nicht
        // mehr erkennbar.
        gearboxRevolutions = calculatedRPM;
        revLimiterFactor = Mathf.Clamp01((maxRevolutions - calculatedRPM) / revLimiterFadeRange);

        // Kupplung: unterhalb der Anfahrdrehzahl zieht das Getriebe den Motor nicht
        // herunter. Sobald die Getriebedrehzahl die Anfahrdrehzahl ueberholt, greift
        // die Untergrenze nicht mehr - der Uebergang ist stetig, ohne Sprung.
        isOnGasPadle = throttleApplied;
        isClutchSlipping = throttleApplied && calculatedRPM < launchRevolutions;

        float lowerLimit = isClutchSlipping ? launchRevolutions : idleRevolutions;
        calculatedRPM = Mathf.Clamp(calculatedRPM, lowerLimit, maxRevolutions);

        revolutionsPerMinute = calculatedRPM;
        return calculatedRPM;
    }

    public float CalculateWheelTorque(float gasPadleValue, float currentSpeedMS)
    {
        float maxPossibleTorque = GetMaxTorqueForRPM(CalculateRPM(currentSpeedMS, gasPadleValue > 0f));
        if(gasPadleValue > 0f)
        {
            float currentTorque = maxPossibleTorque * gasPadleValue * revLimiterFactor;

            float wheelTorque = currentTorque * gears[currentGear - 1] * axleRatio * efficiency;
            return wheelTorque / tireRadius;   
        }
        else
        {
            return 0f;
        }
    }

    private float GetMaxTorqueForRPM(float currentRPM)
    {
        return LookUpOnTorqueCurve(torqueCurve, currentRPM);
    }

    public void ChangeGear(GearShift direction)
    {
        if(direction == GearShift.Up && currentGear < gears.Length)
        {
            currentGear += 1;
        }
        if(direction == GearShift.Down && currentGear > 1)
        {
            currentGear -= 1;
        }
    }

    // Die Drehmomentkurve ist ueber rpm/10000 aufgetragen: ihre Keys liegen bei
    // 0.10..0.60, das entspricht 1000..6000 U/min und damit exakt dem Bereich zwischen
    // idleRevolutions und maxRevolutions. Der Kurvenwert ist das Drehmoment in kNm,
    // Maximum 0.1083 = 108.3 Nm bei 2996 U/min.
    // Vorher wurde die Drehzahl mit 100000 multipliziert statt geteilt. Jede Abfrage
    // landete dadurch bei 1e8, weit hinter dem letzten Key, und m_PostInfinity=2
    // (ClampForever) lieferte konstant denselben Wert - die Kurve war wirkungslos.
    private int rpmDivisor = 10000;
    private int torqueFactor = 1000;

    private float LookUpOnTorqueCurve(AnimationCurve curve, float xValueToLookAt)
    {
        return curve.Evaluate(xValueToLookAt / rpmDivisor) * torqueFactor;
    }

}