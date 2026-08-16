using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    [Header("Fly Speed")]
    public float moveSpeed = 10f;
    public float fastMultiplier = 3f;
    public float scrollSpeed = 0.1f;

    [Header("Look Sensitivity")]
    public float lookSensitivity = 0.15f;

    [Header("Pan Sensitivity")]
    public float panSensitivity = 0.01f;

    float _yaw;
    float _pitch;

    void Start()
    {
        Vector3 euler = transform.eulerAngles;
        _yaw = euler.y;
        _pitch = euler.x;
    }

    void Update()
    {
        var mouse = Mouse.current;
        var keyboard = Keyboard.current;
        if (mouse == null || keyboard == null) return;

        if (mouse.rightButton.isPressed)
            HandleFlyLook(mouse, keyboard);

        if (mouse.middleButton.isPressed)
            HandlePan(mouse);

        HandleScroll(mouse);
    }

    void HandleFlyLook(Mouse mouse, Keyboard keyboard)
    {
        Vector2 delta = mouse.delta.ReadValue();
        _yaw += delta.x * lookSensitivity;
        _pitch -= delta.y * lookSensitivity;
        _pitch = Mathf.Clamp(_pitch, -89f, 89f);
        transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);

        float speed = moveSpeed * (keyboard.leftShiftKey.isPressed ? fastMultiplier : 1f);
        Vector3 input = new Vector3(
            (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f),
            (keyboard.eKey.isPressed ? 1f : 0f) - (keyboard.qKey.isPressed ? 1f : 0f),
            (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f)
        );

        if (input != Vector3.zero)
            transform.position += transform.TransformDirection(input.normalized) * speed * Time.deltaTime;
    }

    void HandlePan(Mouse mouse)
    {
        Vector2 delta = mouse.delta.ReadValue();
        transform.position += transform.right * (-delta.x * panSensitivity)
                            + transform.up   * (-delta.y * panSensitivity);
    }

    void HandleScroll(Mouse mouse)
    {
        float scroll = mouse.scroll.ReadValue().y;
        if (scroll != 0f)
            transform.position += transform.forward * scroll * scrollSpeed;
    }
}
