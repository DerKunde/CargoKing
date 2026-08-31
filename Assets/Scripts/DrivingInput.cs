/// <summary>
/// One physics step worth of driving commands.
/// </summary>
public readonly struct DrivingInput
{
    /// <summary>
    /// Steering wheel position, -1 (left) to 1 (right)
    /// </summary>
    public readonly float Steer;
    /// <summary>
    /// Throttle pedal position, 0 to 1
    /// </summary>
    public readonly float Throttle;
    /// <summary>
    /// Brake pedal position, 0 to 1
    /// </summary>
    public readonly float Brake;
    /// <summary>
    /// Is handbrake applied
    /// </summary>
    public readonly bool Handbrake;
    /// <summary>
    /// Change request for gear change
    /// </summary>
    public readonly GearShift Shift;

    public DrivingInput(float steer, float throttle, float brake, bool handbrake, GearShift shift)
    {
        Steer = steer;
        Throttle = throttle;
        Brake = brake;
        Handbrake = handbrake;
        Shift = shift;
    }

    /// <summary>
    /// For cars that are not being driven. Every field at rest.
    /// </summary>
    public static DrivingInput None => default;
}

/// <summary>
/// Possible gear change orders
/// </summary>
public enum GearShift
{
    /// <summary>
    /// Stay in current gear
    /// </summary>
    None = 0,
    /// <summary>
    /// Shift one gear up
    /// </summary>
    Up,
    /// <summary>
    /// Shift one gear down
    /// </summary>
    Down
}