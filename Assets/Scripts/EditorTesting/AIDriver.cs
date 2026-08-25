using R3;
using UnityEngine;

public class AIDriver : MonoBehaviour
{
    private CarController carController;

    public float reachedTargetDistance = 1f;

    private Vector3 target;
    private MouseToFloorPositioning targetProvider;

    private void Awake()
    {
        targetProvider = FindFirstObjectByType<MouseToFloorPositioning>();
        carController = GetComponent<CarController>();

        if (targetProvider != null)
        {
            targetProvider.carAITarget.Subscribe(mouseSetTarget =>
            {
                target = mouseSetTarget;
            }).AddTo(this);
        }
    }

    private void FixedUpdate()
    {
        Vector3 dirToMovePosition = (target - transform.position).normalized;
        float distanceToTarget = Vector3.Distance(transform.position, target);

        //To far away from Target, keep moving
        if (distanceToTarget > reachedTargetDistance)
        {
            float steerInput = 0f;
            float throttleInput = 0.2f;
            float brakeInput = 0f;
            bool handbrakeInput = false;
            GearShift shiftInput = GearShift.None;
            //Determin when braking to start
            if(distanceToTarget <= CalculateBrakingDistance(carController.CarSpeedInMS()))
            {
                brakeInput = 1f;
                throttleInput = 0f;
            }
            float dot = Vector3.Dot(transform.forward, dirToMovePosition);
            if (dot > 0)
            {
                if(carController.carEngine.currentGear == 0)
                {
                    shiftInput = GearShift.Up;
                }
            }
            else
            {
                if(carController.carEngine.currentGear == 1)
                {
                    shiftInput = GearShift.Down;
                }
            }

            //TODO: Steering needs to be smoother, if angleToDirection is small steering flicks around
            float angleToDirection = Vector3.SignedAngle(transform.forward, dirToMovePosition, Vector3.up);
            if (angleToDirection > 0)
            {
                steerInput = 1f;
            }
            else
            {
                steerInput = -1f;
            }
            if(Mathf.Abs(angleToDirection) < 10f)
            {
                steerInput = 0f;
            }
            DrivingInput input = new DrivingInput(steerInput, throttleInput, brakeInput, handbrakeInput, shiftInput);
            carController.Drive(input);
        }
        else
        {
            //Target reached -> Stop
            DrivingInput inputToStop = new DrivingInput(0f, 0f, 1f, false, GearShift.None);
            carController.Drive(inputToStop);
        }
    }

    private float CalculateBrakingDistance(float speedInMS)
    {
        float brakeDecelartion = carController.MaxBrakeDecelartion();
        float brakingDistance = Mathf.Pow(speedInMS, 2) / (2 * brakeDecelartion);
        return brakingDistance;
    }

    private void CalculateNeededSteeringAngle()
    {
        
    }


}