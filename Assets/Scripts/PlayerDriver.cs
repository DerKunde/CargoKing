using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerDriver : MonoBehaviour
{
    [SerializeField] private CarDrivingInput drivingInput;
    [SerializeField] private CarController currentCar;

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