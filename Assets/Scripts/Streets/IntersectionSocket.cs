using UnityEngine;

namespace CargoKing.Streets
{
    /// <summary>
    /// One arm of an intersection: the place where a street can dock, and the direction it leaves in.
    ///
    /// A real child Transform rather than a serialised coordinate, so it is placed with the ordinary
    /// move tool and carried by the prefab workflow. Its local +Z points away from the intersection,
    /// the way a road leads out of it.
    ///
    /// Deactivate a socket's GameObject to take the arm out of the topology - that is how the same
    /// model serves as both a full crossing and a T junction.
    /// </summary>
    [AddComponentMenu("CargoKing/Intersection Socket")]
    public class IntersectionSocket : MonoBehaviour
    {
        [Tooltip("Width of the carriageway that docks here, in metres. Must match the street's own width.")]
        [Min(0f)]
        public float roadWidth = 7f;

        /// <summary>Direction the road leads away from the intersection, in world space.</summary>
        public Vector3 Outward => transform.forward;

        /// <summary>The intersection this socket belongs to, or null when it sits on its own.</summary>
        public Intersection Owner => GetComponentInParent<Intersection>();

        private void OnDrawGizmosSelected()
        {
            Vector3 position = transform.position;
            Vector3 outward = Outward;
            Vector3 sideways = transform.right * (roadWidth * 0.5f);

            Gizmos.color = new Color(0.4f, 1f, 0.6f);
            Gizmos.DrawLine(position - sideways, position + sideways);
            Gizmos.DrawLine(position, position + outward * Mathf.Max(1f, roadWidth * 0.5f));
        }
    }
}
