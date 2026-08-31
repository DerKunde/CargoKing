using UnityEngine;

/// <summary>
/// Air resistance: F = 0.5 * rho * Cd * A * v^2, against the direction of travel. Acts on
/// the car rather than on a single wheel, which is why it is a component of its own.
///
/// Replaces Unity's linearDamping, which is linear in v and therefore brakes too hard at
/// low speed and too weakly at high speed. Set linearDamping to 0 on the Rigidbody,
/// otherwise both are applied.
/// </summary>
public class AeroDrag : MonoBehaviour
{
    [Header("References")]
    public Rigidbody carBody;

    [Header("Luftwiderstand")]
    public float airDensity = 1.225f;       // kg/m^3, sea level at 15 degrees
    public float dragCoefficient = 0.33f;   // Cd, typical for a small car
    public float frontalArea = 2.0f;        // m^2

    [Header("Debug (nur Anzeige)")]
    public float speedKmH;
    public float dragForceNewton;
    public Vector3 dragForce;

    private void Awake()
    {
        if (carBody == null)
        {
            carBody = GetComponent<Rigidbody>();
        }
    }

    private void FixedUpdate()
    {
        if (carBody == null)
        {
            return;
        }

        Vector3 velocity = carBody.linearVelocity;
        float speed = velocity.magnitude;
        speedKmH = speed * 3.6f;

        if (speed < 0.01f)
        {
            dragForce = Vector3.zero;
            dragForceNewton = 0f;
            return;
        }

        // Against the actual direction of travel, not transform.forward: while drifting or
        // rolling backwards the drag has to brake there as well.
        dragForceNewton = 0.5f * airDensity * dragCoefficient * frontalArea * speed * speed;
        dragForce = -(velocity / speed) * dragForceNewton;

        // No anti-reversal clamp as in the rolling resistance: the force grows with v^2,
        // so overshooting a standstill would take about 99000 m/s.
        carBody.AddForce(dragForce, ForceMode.Force);
    }
}
