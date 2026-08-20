using UnityEngine;

/// <summary>
/// Luftwiderstand des Fahrzeugs: F = 0.5 * rho * Cd * A * v^2, entgegen der
/// Bewegungsrichtung. Greift am Fahrzeug an, nicht am einzelnen Rad - deshalb eine
/// eigene Komponente statt eines Anbaus an Suspension.
///
/// Ersetzt Unitys linearDamping, das linear in v ist und damit bei kleinen
/// Geschwindigkeiten zu stark und bei grossen zu schwach bremst. Wichtig: linearDamping
/// am Rigidbody auf 0 setzen, sonst wirken beide.
/// </summary>
public class AeroDrag : MonoBehaviour
{
    [Header("References")]
    public Rigidbody carBody;

    [Header("Luftwiderstand")]
    public float airDensity = 1.225f;       // kg/m^3, Meereshoehe bei 15 Grad
    public float dragCoefficient = 0.33f;   // Cd, typisch fuer einen Kleinwagen
    public float frontalArea = 2.0f;        // m^2, Stirnflaeche

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

        // Bewusst gegen die tatsaechliche Bewegungsrichtung, nicht gegen transform.forward:
        // beim Driften oder Rueckwaertsrollen soll der Widerstand auch dort bremsen.
        dragForceNewton = 0.5f * airDensity * dragCoefficient * frontalArea * speed * speed;
        dragForce = -(velocity / speed) * dragForceNewton;

        // Ein Anti-Rueckwaerts-Clamp wie beim Rollwiderstand ist hier unnoetig: die Kraft
        // faellt quadratisch mit v, ein Ueberschwingen waere erst bei ~99000 m/s moeglich.
        carBody.AddForce(dragForce, ForceMode.Force);
    }
}
