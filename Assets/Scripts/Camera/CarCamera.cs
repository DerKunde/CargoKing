using CargoKing.Car;
using R3;
using Reflex.Attributes;
using UnityEngine;

namespace CargoKing.Camera
{
    public class CarCamera : MonoBehaviour
    {
        [Inject] private CurrentCarProvider currentCarProvider;

        [Header("Setup")]
        private Transform cameraTransform;
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
            cameraTransform = UnityEngine.Camera.main.transform;

            // ReactiveProperty replays the latest value on subscribe and every change after, so
            // this stays correct whether PlayerDriver has already set the car or not yet.
            currentCarProvider.Current.Subscribe(car =>
            {
                carTransform = car != null ? car.transform : null;
                if (carTransform != null)
                {
                    currentYRotation = carTransform.eulerAngles.y;
                }
            }).AddTo(this);
        }

        private void LateUpdate()
        {
            if (carTransform == null)
            {
                return;
            }

            float targetYRotation = carTransform.eulerAngles.y;
            currentYRotation = Mathf.LerpAngle(currentYRotation, targetYRotation, smoothingSpeed * Time.fixedDeltaTime);
            Quaternion rotation = Quaternion.Euler(35, currentYRotation, 0);

            Vector3 offset = rotation * Vector3.back * distance;
            offset.y = height;

            cameraTransform.position = carTransform.position + offset;
            cameraTransform.LookAt(carTransform.position + Vector3.up * (height * 0.5f));
        }
    }
}
