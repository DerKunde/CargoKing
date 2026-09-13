using UnityEngine;

namespace CargoKing.World
{
    /// <summary>
    /// Keeps a finite ground plane under the car by following it along X. Only works for an
    /// untextured ground - a pattern would visibly travel with the car.
    /// </summary>
    public class GroundFollower : MonoBehaviour
    {
        public Transform carTransform;

        private void LateUpdate()
        {
            Vector3 position = transform.position;
            position.x = carTransform.position.x;
            transform.position = position;
        }
    }
}
