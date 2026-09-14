namespace CargoKing.Traffic
{
    public enum ShiftCommand
    {
        None,
        Up,
        Down,
    }

    /// <summary>What to do with gear lever and clutch this step.</summary>
    public struct ShiftDecision
    {
        public ShiftCommand shift;
        public bool holdClutch;
    }

    /// <summary>What the gear policy looks at.</summary>
    public struct ShiftSituation
    {
        /// <summary>
        /// Engine speed the driveline dictates in the engaged gear (CarEngine.gearboxRevolutions), not
        /// the engine's own. It follows a shift at once; the engine's own lags until the clutch has
        /// caught up, and reading it would drop two gears where one was meant.
        /// </summary>
        public float rpm;

        /// <summary>Engaged gear: 0 is reverse, 1 is first.</summary>
        public int gear;

        public int gearCount;

        /// <summary>Road speed in m/s, without sign.</summary>
        public float speed;

        /// <summary>Whether the driver is on the throttle this step.</summary>
        public bool accelerating;

        public float secondsSinceShift;
    }

    /// <summary>
    /// How an AI driver works a manual gearbox. The car stays a manual: this decides, the agent moves
    /// the lever through the ordinary DrivingInput, exactly as a player would.
    /// </summary>
    public static class ShiftPolicy
    {
        /// <summary>Below this speed, in m/s, the car counts as standing.</summary>
        public const float StandstillSpeed = 0.5f;

        public static ShiftDecision Decide(in ShiftSituation situation, DrivingProfile profile)
        {
            // Reverse belongs to the manoeuvres, which engage and leave it themselves.
            if (situation.gear <= 0)
            {
                return default;
            }

            // Standing in a high gear, after a hard stop or a stall: back to first before pulling away.
            if (situation.speed < StandstillSpeed && situation.gear > 1)
            {
                return new ShiftDecision { shift = ShiftCommand.Down, holdClutch = true };
            }

            // Slow and not pulling: clutch in, or the engine stalls on its way down to walking pace.
            // With the clutch held a downshift costs nothing, so it does not wait for the interval.
            if (situation.speed < profile.ClutchInSpeed && !situation.accelerating)
            {
                return new ShiftDecision
                {
                    shift = situation.gear > 1 ? ShiftCommand.Down : ShiftCommand.None,
                    holdClutch = true,
                };
            }

            if (situation.accelerating
                && situation.rpm >= profile.upshiftRpm
                && situation.gear < situation.gearCount
                && situation.secondsSinceShift >= profile.minimumShiftInterval)
            {
                return new ShiftDecision { shift = ShiftCommand.Up };
            }

            if (situation.rpm < profile.downshiftRpm && situation.gear > 1)
            {
                return new ShiftDecision { shift = ShiftCommand.Down };
            }

            return default;
        }
    }
}
