using UnityEngine;

namespace CargoKing.Car
{
    /// <summary>
    /// Pure drivetrain math with no MonoBehaviour or scene dependency, so it can run under a
    /// plain EditMode test without a Play Mode session.
    /// </summary>
    public static class DriveTrainMath
    {
        public static float AngularVelocityToRpm(float angularVelocityRadPerSec)
        {
            return angularVelocityRadPerSec * 60f / (2f * Mathf.PI);
        }

        /// <summary>
        /// Clutch plate torque: proportional to the RPM gap between engine and driveline, capped
        /// by the plate's maximum friction torque. Positive means torque flows from engine to
        /// driveline (engine turning faster than the wheels expect).
        /// </summary>
        public static float ClutchTorque(float engineRpm, float wheelRpmGeared, float clutchStiffness, float maxClutchTorque)
        {
            float rawTorque = clutchStiffness * (engineRpm - wheelRpmGeared);
            return Mathf.Clamp(rawTorque, -maxClutchTorque, maxClutchTorque);
        }

        public static bool IsLocked(float engineRpm, float wheelRpmGeared, float lockEpsilonRpm)
        {
            return Mathf.Abs(engineRpm - wheelRpmGeared) <= lockEpsilonRpm;
        }

        public static bool IsStalled(float engineRpm, float stallRpm)
        {
            return engineRpm < stallRpm;
        }

        /// <summary>
        /// One integration step for a freely-turning (not rigidly locked) engine: net torque over
        /// inertia gives an angular acceleration, converted to RPM/s and rate-limited by
        /// <paramref name="rpmChangeSpeed"/> so the response stays believable rather than instant.
        /// </summary>
        public static float IntegrateEngineRpm(float currentRpm, float combustionTorque, float clutchReactionTorque, float engineInertia, float rpmChangeSpeed, float deltaTime)
        {
            float netTorque = combustionTorque - clutchReactionTorque;
            float rpmAccelPerSecond = netTorque / engineInertia * 60f / (2f * Mathf.PI);
            float target = currentRpm + rpmAccelPerSecond * deltaTime;
            return Mathf.MoveTowards(currentRpm, target, rpmChangeSpeed * deltaTime);
        }
    }
}
