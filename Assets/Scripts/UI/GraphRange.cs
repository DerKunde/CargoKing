using System;

/// <summary>
/// Bildet einen Messbereich auf 0..1 ab - also auf die Hoehe der Zeichenflaeche.
/// Entweder mit festen Grenzen (Gaspedal 0..1, Drehzahl 0..6500) oder automatisch aus
/// den Daten im Fenster.
///
/// Bewusst frei von UnityEngine, damit die Rechnung ausserhalb des Editors testbar bleibt.
/// </summary>
public class GraphRange
{
    // Mindestbreite eines automatisch ermittelten Bereichs. Ohne sie faellt der Bereich
    // bei konstanten Werten auf Null zusammen und die Beschriftung zeigte zweimal
    // denselben Wert.
    private const float MinimumAutoSpan = 1f;

    public float Min;
    public float Max;
    public bool AutoScale;

    public static GraphRange Fixed(float min, float max)
    {
        return new GraphRange { Min = min, Max = max, AutoScale = false };
    }

    public static GraphRange Auto()
    {
        // Startbereich, bis der erste Wert eintrifft.
        return new GraphRange { Min = 0f, Max = 1f, AutoScale = true };
    }

    /// <summary>
    /// Zieht die Grenzen bei AutoScale auf das aktuelle Datenfenster nach. Feste Grenzen
    /// bleiben unangetastet.
    /// </summary>
    public void ResolveFrom(GraphBuffer buffer)
    {
        if (!AutoScale || buffer == null)
        {
            return;
        }

        float min, max;
        if (!buffer.TryGetMinMax(out min, out max))
        {
            return;
        }

        float span = max - min;
        if (span < MinimumAutoSpan)
        {
            // Um die Mitte der Daten herum aufspannen, damit die Linie mittig liegt.
            float center = (min + max) * 0.5f;
            min = center - MinimumAutoSpan * 0.5f;
            max = center + MinimumAutoSpan * 0.5f;
        }

        Min = min;
        Max = max;
    }

    /// <summary>
    /// Wert auf 0..1 abbilden. Werte ausserhalb der Grenzen werden geklemmt, damit die
    /// Linie nicht aus der Zeichenflaeche laeuft.
    /// </summary>
    public float Normalize(float value)
    {
        float span = Max - Min;

        // Entarteter Bereich: keine Division durch Null, sondern flach in die Mitte.
        if (Math.Abs(span) < float.Epsilon)
        {
            return 0.5f;
        }

        float normalized = (value - Min) / span;

        if (normalized < 0f)
        {
            return 0f;
        }

        if (normalized > 1f)
        {
            return 1f;
        }

        return normalized;
    }
}
