using UnityEngine;

/// <summary>
/// Reine Anzeige-Komponente. Rechnet keine Physik, sondern liest ausschliesslich
/// die Felder aus Suspension und zeichnet sie als Gizmo-Pfeile.
/// </summary>
public class VisualizeWheelForces : MonoBehaviour
{
    [Header("References")]
    public Transform wheelOrigin;
    public Suspension suspension;

    [Header("Darstellung")]
    // Richtungen sind Einheitsvektoren und bekommen eine feste Laenge.
    public float directionLength = 0.4f;
    // Kraefte sind in Newton und werden logarithmisch abgebildet, weil sie ueber
    // mehrere Groessenordnungen laufen. referenceForce ist der Bezugspunkt.
    public float baseLength = 0.35f;
    public float referenceForce = 10f;
    public float maxLength = 1.5f;
    public float arrowHeadSize = 0.06f;

    [Header("Anzeigen")]
    public bool showSpringForce = true;
    public bool showLateralAxis = true;
    public bool showRollDirection = true;
    public bool showTireRollForce = true;
    public bool showTireSlipForce = true;
    public bool showTireForce = true;

    [Header("Farben")]
    public Color springForceColor = Color.green;
    public Color lateralAxisColor = Color.cyan;
    public Color rollDirectionColor = Color.blue;
    public Color tireRollForceColor = new Color(1f, 0.55f, 0.1f);
    // Andere Farbe, solange nicht der Reibwert die Kraft begrenzt, sondern der
    // Anti-Rueckwaerts-Clamp - also genau in den letzten Metern vor dem Stillstand.
    public Color tireRollForceClampedColor = Color.yellow;
    public Color tireSlipForceColor = Color.red;
    public Color tireForceColor = Color.magenta;
    public Color contactPointColor = Color.yellow;

    [Header("Messwerte in N (nur Anzeige)")]
    public float springForceNewton;
    public float tireRollForceNewton;
    public float rollResistanceLimitNewton;
    public float rollStopLimitNewton;
    public float tireSlipForceNewton;
    public float tireForceNewton;

    private void OnDrawGizmos()
    {
        if (wheelOrigin == null || suspension == null)
        {
            return;
        }

        // Radmitte: Bezugspunkt fuer die Richtungspfeile.
        Vector3 wheelCenter = wheelOrigin.position;
        Gizmos.color = Color.magenta;
        Gizmos.DrawSphere(wheelCenter, 0.04f);

        // Aufstandspunkt, so wie ihn der Raycast in Suspension gefunden hat.
        if (suspension.isGrounded)
        {
            Gizmos.color = contactPointColor;
            Gizmos.DrawSphere(suspension.contactPoint, 0.03f);
        }

        springForceNewton = suspension.suspensionForce.magnitude;
        tireRollForceNewton = suspension.tireRollForce.magnitude;
        rollResistanceLimitNewton = suspension.rollResistanceLimit;
        rollStopLimitNewton = suspension.rollStopLimit;
        tireSlipForceNewton = suspension.tireSlip.magnitude;
        tireForceNewton = suspension.tireForce.magnitude;

        // Richtungen gehoeren an die Radmitte, Kraefte an ihren jeweiligen Angriffspunkt.
        if (showRollDirection)
        {
            DrawArrow(wheelCenter, suspension.displayRollDirection, directionLength, rollDirectionColor);
        }

        if (showLateralAxis)
        {
            DrawArrow(wheelCenter, suspension.displayLateralAxis, directionLength, lateralAxisColor);
        }

        if (showSpringForce)
        {
            DrawForce(suspension.suspensionForcePoint, suspension.displaySuspensionForce, springForceColor);
        }

        if (showTireRollForce)
        {
            DrawForce(suspension.tireForcePoint, suspension.displayTireRollForce,
                suspension.rollForceAtStopLimit ? tireRollForceClampedColor : tireRollForceColor);
        }

        if (showTireSlipForce)
        {
            DrawForce(suspension.tireForcePoint, suspension.displayTireSlipForce, tireSlipForceColor);
        }

        if (showTireForce)
        {
            DrawForce(suspension.tireForcePoint, suspension.displayTireForce, tireForceColor);
        }
    }

    private void DrawForce(Vector3 origin, Vector3 force, Color color)
    {
        DrawArrow(origin, force, ForceToLength(force.magnitude), color);
    }

    private float ForceToLength(float magnitude)
    {
        if (magnitude <= 0.001f)
        {
            return 0f;
        }

        return Mathf.Min(maxLength, baseLength * Mathf.Log10(1f + magnitude / referenceForce));
    }

    private void DrawArrow(Vector3 origin, Vector3 direction, float length, Color color)
    {
        if (length <= 0.0001f || direction.sqrMagnitude <= 0.000001f)
        {
            return;
        }

        Vector3 dir = direction.normalized;
        Vector3 tip = origin + dir * length;

        Gizmos.color = color;
        Gizmos.DrawLine(origin, tip);

        Vector3 side = Vector3.Cross(dir, Vector3.up);
        if (side.sqrMagnitude < 0.001f)
        {
            side = Vector3.Cross(dir, Vector3.forward);
        }
        side.Normalize();
        Vector3 other = Vector3.Cross(dir, side);

        float head = Mathf.Min(arrowHeadSize, length * 0.5f);
        Gizmos.DrawLine(tip, tip + (-dir + side) * head);
        Gizmos.DrawLine(tip, tip + (-dir - side) * head);
        Gizmos.DrawLine(tip, tip + (-dir + other) * head);
        Gizmos.DrawLine(tip, tip + (-dir - other) * head);
    }
}
