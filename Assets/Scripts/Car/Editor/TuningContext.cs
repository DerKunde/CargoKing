using UnityEngine;

namespace CargoKing.Car.Editor
{
    /// <summary>
    /// The car the previews read what a profile does not hold - the gearbox for the drive torque,
    /// the engine's max rpm and the wheel radius for road speed: in Play Mode the one being driven,
    /// otherwise the first one in the open scene.
    /// </summary>
    internal static class TuningContext
    {
        public static CarController Car => VehicleLiveSampler.CurrentCar != null
            ? VehicleLiveSampler.CurrentCar
            : Object.FindFirstObjectByType<CarController>();

        /// <summary>
        /// The car's engine profile as the previews should see it: the Vehicle Tuning window's
        /// working copy when there is one, so unsaved edits show up here too.
        /// </summary>
        public static EngineProfile EngineOf(CarController car)
        {
            return car != null && car.carEngine != null
                ? ProfileWorkingCopies.instance.CopyIfAny(car.carEngine.engineProfile)
                : null;
        }

        /// <summary>The car's gearbox profile, like <see cref="EngineOf"/>.</summary>
        public static GearboxProfile GearboxOf(CarController car)
        {
            return car != null && car.carEngine != null
                ? ProfileWorkingCopies.instance.CopyIfAny(car.carEngine.gearboxProfile)
                : null;
        }

        /// <summary>Radius of the driven (rear) wheels, m; null without a car or wheel.</summary>
        public static float? WheelRadiusOf(CarController car)
        {
            if (car == null || car.rearLeftWheel == null)
            {
                return null;
            }

            Suspension wheel = car.rearLeftWheel.GetComponent<Suspension>();
            return wheel != null ? wheel.WheelRadius : (float?)null;
        }

        /// <summary>The engaged gear while driving; null outside Play Mode.</summary>
        public static int? EngagedGear(CarController car)
        {
            return Application.isPlaying && car != null && car.carEngine != null
                ? car.carEngine.currentGear
                : (int?)null;
        }
    }
}
