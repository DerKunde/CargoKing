using UnityEngine;

public class CarCamera : MonoBehaviour
{
    [Header("Setup")]
    private Transform cameraTransform;
    [SerializeField]
    private Transform carTransform;

    [Header("Camera following settings")]
    [SerializeField]
    private float distance = 3f;
    [SerializeField]
    private float height = 3.5f;
    [SerializeField]
    private float smoothingSpeed = 3f;

    private float currentYRotation = 0f;

    void Awake()
    {
        currentYRotation = carTransform.eulerAngles.y;
        cameraTransform = Camera.main.transform;
    }

    private void LateUpdate()
    {
        float targetYRotation = carTransform.eulerAngles.y;
        currentYRotation = Mathf.LerpAngle(currentYRotation, targetYRotation, smoothingSpeed * Time.fixedDeltaTime);
        Quaternion rotation = Quaternion.Euler(35, currentYRotation, 0);

        Vector3 offset = rotation * Vector3.back * distance;
        offset.y = height;

        cameraTransform.position = carTransform.position + offset;
        cameraTransform.LookAt(carTransform.position + Vector3.up * (height * 0.5f));
    }
}