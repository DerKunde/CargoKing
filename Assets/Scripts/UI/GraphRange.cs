using System;

/// <summary>
/// Maps a measuring range onto 0..1, that is onto the height of the plot area. Either with
/// fixed bounds (throttle 0..1, revolutions 0..6500) or automatically from the data in the
/// window.
///
/// Deliberately free of UnityEngine, so the maths stays testable outside the editor.
/// </summary>
public class GraphRange
{
    // Minimum width of an automatic range. Without it the range collapses to zero on
    // constant values and the labels would show the same number twice.
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
        // Starting range until the first value arrives.
        return new GraphRange { Min = 0f, Max = 1f, AutoScale = true };
    }

    /// <summary>
    /// Follows the current data window while AutoScale is on. Fixed bounds are left alone.
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
            // Spread around the centre of the data so the line sits in the middle.
            float center = (min + max) * 0.5f;
            min = center - MinimumAutoSpan * 0.5f;
            max = center + MinimumAutoSpan * 0.5f;
        }

        Min = min;
        Max = max;
    }

    /// <summary>
    /// Maps a value onto 0..1. Values outside the bounds are clamped so the line cannot
    /// run out of the plot area.
    /// </summary>
    public float Normalize(float value)
    {
        float span = Max - Min;

        // Degenerate range: no division by zero, flat through the middle instead.
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
