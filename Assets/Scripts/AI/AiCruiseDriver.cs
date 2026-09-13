using CargoKing.Car;
using CargoKing.Input;
using UnityEngine;

namespace CargoKing.AI
{
    /// <summary>
    /// Drives straight ahead holding a target speed, steering only to hold the road's centre
    /// line. No braking, shifting or manoeuvring - background traffic for the main menu, not a
    /// full driver. CarEngine's own launch assist handles pulling away from a standstill.
    /// </summary>
    public class AiCruiseDriver : MonoBehaviour
    {
        public float targetSpeedKmh = 30f;
        public float throttlePerKmhError = 0.02f;
        public float lookAheadDistance = 15f;
        public float centerLineZ = 0f;

        private CarController carController;

        private void Awake()
        {
            carController = GetComponent<CarController>();
        }

        private void FixedUpdate()
        {
            Vector3 forwardPoint = transform.position + transform.forward * lookAheadDistance;
            Vector3 aimPoint = new Vector3(forwardPoint.x, transform.position.y, centerLineZ);

            Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            Vector3 dirToAimPoint = Vector3.ProjectOnPlane(aimPoint - transform.position, Vector3.up);

            float angleToDirection = Vector3.SignedAngle(flatForward, dirToAimPoint, Vector3.up);
            float steerInput = Mathf.Clamp(angleToDirection / carController.maxSteerAngle, -1f, 1f);

            float currentSpeedKmh = carController.CarSpeedInMS() * 3.6f;
            float throttle = Mathf.Clamp01((targetSpeedKmh - currentSpeedKmh) * throttlePerKmhError);

            carController.Drive(new DrivingInput(steerInput, throttle, 0f, false, GearShift.None, false, false));
        }
    }
}
