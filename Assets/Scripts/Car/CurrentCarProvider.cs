using R3;

namespace CargoKing.Car
{
    /// <summary>
    /// Holds a reference to whichever car the player is currently driving, so HUD, camera and
    /// other consumers can react to it instead of each holding their own serialized link.
    /// </summary>
    public class CurrentCarProvider
    {
        public ReactiveProperty<CarController> Current { get; } = new ReactiveProperty<CarController>();

        public void SetCurrent(CarController car) => Current.Value = car;
    }
}
