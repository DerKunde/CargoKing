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

        if (distanceToTarget > reachedTargetDistance)
        {
            //To far away from Target, keep moving
            float dot = Vector3.Dot(transform.forward, dirToMovePosition);
            if (dot > 0)
            {
                //Target in front
            }
            else
            {
                //Target in back
            }

            float angleToDirection = Vector3.SignedAngle(transform.forward, dirToMovePosition, Vector3.up);
            if (angleToDirection > 0)
            {
                //turn left (positiv)??
            }
            else
            {
                //turn right (negativ)??
            }
        }
        else
        {
            //Target reached -> Stop
            DrivingInput inputToStop = new DrivingInput(0f, 0f, 1f, false, GearShift.None);
        }

        DrivingInput input = new DrivingInput(1f, 0.3f, 0f, false, GearShift.None);
        carController.Drive(input);
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