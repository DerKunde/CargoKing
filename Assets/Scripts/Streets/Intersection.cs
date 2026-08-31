using System.Collections.Generic;
using UnityEngine;

namespace CargoKing.Streets
{
    /// <summary>
    /// An intersection. Collects the sockets below it and derives every driveable way across.
    ///
    /// The topology lives in the sockets, not in the model, which is why one mesh can serve as several
    /// intersections: four active sockets make a full crossing, three make a T junction.
    ///
    /// Like the street segment, nothing derived is serialised - the connections are rebuilt whenever a
    /// socket moves, so they cannot disagree with what the prefab actually looks like.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("CargoKing/Intersection")]
    public class Intersection : MonoBehaviour
    {
        private readonly List<IntersectionSocket> sockets = new List<IntersectionSocket>();
        private readonly List<IntersectionSocketPose> poses = new List<IntersectionSocketPose>();
        private readonly List<IntersectionSocketPose> previousPoses = new List<IntersectionSocketPose>();

        private IntersectionConnection[] connections = System.Array.Empty<IntersectionConnection>();
        private bool isDirty = true;

        /// <summary>The active sockets, in hierarchy order. Indices into this list address a socket.</summary>
        public IReadOnlyList<IntersectionSocket> Sockets => sockets;

        /// <summary>Every way across, one per ordered pair of sockets minus the U turns.</summary>
        public IReadOnlyList<IntersectionConnection> Connections => connections;

        private void OnEnable()
        {
            isDirty = true;
        }

        private void OnValidate()
        {
            isDirty = true;
        }

        private void Update()
        {
            // Sockets are moved with the ordinary transform tools, which raises no callback of its own,
            // so their poses are compared instead. There are only a handful of them per intersection.
            if (isDirty || CollectSockets())
            {
                Rebuild();
            }
        }

        /// <summary>
        /// Recomputes every connection. Safe to call at any time; the component calls it by itself
        /// whenever a socket appears, moves or goes away.
        /// </summary>
        public void Rebuild()
        {
            isDirty = false;

            CollectSockets();
            connections = IntersectionLaneBuilder.Build(poses);

            previousPoses.Clear();
            previousPoses.AddRange(poses);
        }

        /// <summary>
        /// Refreshes the socket list and their poses in local space.
        /// </summary>
        /// <returns>True when anything about them changed since the last rebuild.</returns>
        private bool CollectSockets()
        {
            sockets.Clear();
            poses.Clear();

            // Inactive sockets are skipped on purpose: deactivating one is how a crossing becomes a
            // T junction without touching the model.
            GetComponentsInChildren(false, sockets);

            for (int index = 0; index < sockets.Count; index++)
            {
                IntersectionSocket socket = sockets[index];

                poses.Add(new IntersectionSocketPose
                {
                    position = transform.InverseTransformPoint(socket.transform.position),
                    outward = transform.InverseTransformDirection(socket.Outward).normalized,
                    up = transform.InverseTransformDirection(socket.transform.up).normalized,
                    roadWidth = socket.roadWidth,
                });
            }

            return HasChanged();
        }

        private bool HasChanged()
        {
            if (poses.Count != previousPoses.Count)
            {
                return true;
            }

            for (int index = 0; index < poses.Count; index++)
            {
                IntersectionSocketPose current = poses[index];
                IntersectionSocketPose previous = previousPoses[index];

                if (current.position != previous.position
                    || current.outward != previous.outward
                    || current.up != previous.up
                    || !Mathf.Approximately(current.roadWidth, previous.roadWidth))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
