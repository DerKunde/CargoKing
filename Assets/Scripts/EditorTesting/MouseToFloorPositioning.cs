using R3;
using UnityEngine;
using UnityEngine.InputSystem;

public class MouseToFloorPositioning : MonoBehaviour
{
    [SerializeField] private Transform targetVisual;
    public float maxRayDistance = 100f;


    public ReactiveProperty<Vector3> carAITarget = new ReactiveProperty<Vector3>();
    
    void FixedUpdate()
    {
        Vector2 mouseScreenPosition = Mouse.current.position.ReadValue();
        Ray ray = Camera.main.ScreenPointToRay(mouseScreenPosition);

        if(Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, LayerMask.GetMask("Floor")))
        {
            targetVisual.position = hit.point;
            carAITarget.Value = hit.point;
        }
    }
}