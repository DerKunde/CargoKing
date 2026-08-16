using UnityEngine;
using UnityEngine.InputSystem;

public class CarController : MonoBehaviour
{
    public Transform frontLeftWheel;
    public Transform frontRightWheel;

    void Update()
    {
        var keyboard = Keyboard.current;

        if(keyboard == null)
        {
            return;
        }

        if (keyboard.aKey.isPressed)
        {
            frontLeftWheel.localEulerAngles = new Vector3(0,-25f,0);
            frontRightWheel.localEulerAngles = new Vector3(0,-25f,0);
        }

        if (keyboard.dKey.isPressed)
        {
            frontLeftWheel.localEulerAngles = new Vector3(0,35f,0);
            frontRightWheel.localEulerAngles = new Vector3(0,35f,0);
        }

        if (keyboard.wKey.isPressed)
        {
            frontLeftWheel.GetComponent<Suspension>().Acceleration();
            frontRightWheel.GetComponent<Suspension>().Acceleration();
        }

        if (keyboard.sKey.isPressed)
        {
            frontLeftWheel.GetComponent<Suspension>().CalculateBraking();
            frontRightWheel.GetComponent<Suspension>().CalculateBraking();
        }

        if(!keyboard.aKey.isPressed && !keyboard.dKey.isPressed)
        {
            frontLeftWheel.localEulerAngles = new Vector3(0,0,0);
            frontRightWheel.localEulerAngles = new Vector3(0,0,0);
        }
    }
}
