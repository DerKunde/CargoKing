using CargoKing.Car;
using Reflex.Attributes;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CargoKing.Input
{
    public class PlayerDriver : MonoBehaviour
    {
        [SerializeField] private CarDrivingInput drivingInput;
        [SerializeField] private CarController currentCar;

        [Inject] private CurrentCarProvider currentCarProvider;

        private void Awake()
        {
            // This is the possession point: the scene wiring of "which car" starts here and gets
            // published so HUD, camera etc. can react to it without their own serialized link.
            currentCarProvider.SetCurrent(currentCar);
        }

        void FixedUpdate()
        {
            if(currentCar != null)
            {
                currentCar.Drive(drivingInput.Sample());

                if (Keyboard.current.rKey.isPressed)
                {
                    currentCar.ResetCar();
                }
            }
        }
    }
}
