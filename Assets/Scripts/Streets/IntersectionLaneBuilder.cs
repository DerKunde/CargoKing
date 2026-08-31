using System.Collections.Generic;
using UnityEngine;

namespace CargoKing.Streets
{
    /// <summary>
    /// A socket reduced to what the lane geometry needs, in the local space of the intersection.
    /// </summary>
    public struct IntersectionSocketPose
    {
        /// <summary>Where the arm ends and a street docks.</summary>
        public Vector3 position;

        /// <summary>Unit vector pointing away from the intersection.</summary>
        public Vector3 outward;

        /// <summary>Up vector of the arm, for working out which side is right.</summary>
        public Vector3 up;

        /// <summary>Width of the carriageway docking here, in metres.</summary>
        public float roadWidth;
    }

    /// <summary>
    /// Derives every way through an intersection from its sockets.
    ///
    /// The connections are computed rather than authored. Twelve of them for a four armed crossing is
    /// busywork to maintain by hand, and a wrong one shows up as a car driving through a kerb rather
    /// than as an error.
    /// </summary>
    public static class IntersectionLaneBuilder
    {
        private const float MinimumLength = 0.001f;

        /// <summary>Beyond this angle a way across counts as a turn rather than as going straight on.</summary>
        private const float StraightAngle = 45f;

        /// <summary>At or beyond this angle it is a U turn, which no intersection offers.</summary>
        private const float UTurnAngle = 135f;

        /// <summary>
        /// Length of the Bezier handles as a fraction of the straight line between entry and exit.
        /// A quarter circle wants 0.5523 times its radius, which for a right angle works out at about
        /// 0.39 of the chord - so this is the value that makes a 90 degree turn come out round.
        /// </summary>
        private const float HandleFraction = 0.4f;

        /// <summary>Roughly how far apart the samples of a connection sit, in metres.</summary>
        private const float SampleSpacing = 0.5f;

        private const int MinimumSampleCount = 8;
        private const int MaximumSampleCount = 128;

        /// <summary>
        /// Builds one connection for every ordered pair of sockets, minus the U turns.
        /// </summary>
        public static IntersectionConnection[] Build(IReadOnlyList<IntersectionSocketPose> sockets)
        {
            List<IntersectionConnection> connections = new List<IntersectionConnection>();

            if (sockets == null || sockets.Count < 2)
            {
                return System.Array.Empty<IntersectionConnection>();
            }

            for (int from = 0; from < sockets.Count; from++)
            {
                for (int to = 0; to < sockets.Count; to++)
                {
                    if (from == to)
                    {
                        continue;
                    }

                    IntersectionConnection connection = TryConnect(sockets[from], sockets[to], from, to);
                    if (connection != null)
                    {
                        connections.Add(connection);
                    }
                }
            }

            return connections.ToArray();
        }

        private static IntersectionConnection TryConnect(
            IntersectionSocketPose from,
            IntersectionSocketPose to,
            int fromIndex,
            int toIndex)
        {
            // A vehicle enters against the socket's outward direction and leaves along it.
            Vector3 entryDirection = -from.outward;
            Vector3 exitDirection = to.outward;

            if (entryDirection.sqrMagnitude < MinimumLength || exitDirection.sqrMagnitude < MinimumLength)
            {
                return null;
            }

            entryDirection.Normalize();
            exitDirection.Normalize();

            float angle = Vector3.SignedAngle(entryDirection, exitDirection, from.up);
            if (Mathf.Abs(angle) >= UTurnAngle)
            {
                return null;
            }

            // Entry and exit sit a quarter of the road's width to the right of the direction of travel,
            // the same offset a street segment gives its lanes, so the two meet flush at the socket.
            Vector3 entry = from.position + RightOf(entryDirection, from.up) * (from.roadWidth * 0.25f);
            Vector3 exit = to.position + RightOf(exitDirection, to.up) * (to.roadWidth * 0.25f);

            StreetLane lane = BuildLane(entry, entryDirection, exit, exitDirection);
            if (lane == null)
            {
                return null;
            }

            return new IntersectionConnection(fromIndex, toIndex, TurnFrom(angle), lane);
        }

        private static StreetTurn TurnFrom(float signedAngle)
        {
            if (Mathf.Abs(signedAngle) <= StraightAngle)
            {
                return StreetTurn.Straight;
            }

            // Positive turns towards the right hand side, because cross(forward, right) points up.
            return signedAngle > 0f ? StreetTurn.Right : StreetTurn.Left;
        }

        private static Vector3 RightOf(Vector3 direction, Vector3 up)
        {
            return StreetFrame.At(direction, up) * Vector3.right;
        }

        /// <summary>
        /// Lays a cubic Bezier from entry to exit, leaving and arriving along the socket directions,
        /// and samples it into a lane.
        /// </summary>
        private static StreetLane BuildLane(Vector3 entry, Vector3 entryDirection, Vector3 exit, Vector3 exitDirection)
        {
            float chord = Vector3.Distance(entry, exit);
            if (chord < MinimumLength)
            {
                return null;
            }

            float handle = chord * HandleFraction;
            Vector3 control1 = entry + entryDirection * handle;
            Vector3 control2 = exit - exitDirection * handle;

            int sampleCount = Mathf.Clamp(
                Mathf.CeilToInt(chord / SampleSpacing) + 1,
                MinimumSampleCount,
                MaximumSampleCount);

            StreetLaneSample[] samples = new StreetLaneSample[sampleCount];
            float distance = 0f;

            for (int index = 0; index < sampleCount; index++)
            {
                float t = index / (float)(sampleCount - 1);

                Vector3 position = Evaluate(entry, control1, control2, exit, t);
                Vector3 velocity = EvaluateVelocity(entry, control1, control2, exit, t);
                Vector3 acceleration = EvaluateAcceleration(entry, control1, control2, exit, t);

                if (index > 0)
                {
                    distance += Vector3.Distance(samples[index - 1].position, position);
                }

                samples[index] = new StreetLaneSample
                {
                    position = position,
                    direction = velocity.sqrMagnitude > MinimumLength ? velocity.normalized : entryDirection,
                    distance = distance,
                    radius = RadiusFrom(velocity, acceleration),
                };
            }

            // A connection only ever runs one way, so within its own definition it is always forward.
            return new StreetLane(StreetLaneDirection.Forward, samples);
        }

        /// <summary>Curve radius from the first and second derivative: k = |v x a| / |v|^3.</summary>
        private static float RadiusFrom(Vector3 velocity, Vector3 acceleration)
        {
            float speed = velocity.magnitude;
            if (speed < MinimumLength)
            {
                return float.PositiveInfinity;
            }

            float curvature = Vector3.Cross(velocity, acceleration).magnitude / (speed * speed * speed);
            return curvature > MinimumLength ? 1f / curvature : float.PositiveInfinity;
        }

        private static Vector3 Evaluate(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float u = 1f - t;
            return (u * u * u) * p0
                + (3f * u * u * t) * p1
                + (3f * u * t * t) * p2
                + (t * t * t) * p3;
        }

        private static Vector3 EvaluateVelocity(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float u = 1f - t;
            return (3f * u * u) * (p1 - p0)
                + (6f * u * t) * (p2 - p1)
                + (3f * t * t) * (p3 - p2);
        }

        private static Vector3 EvaluateAcceleration(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float u = 1f - t;
            return (6f * u) * (p2 - 2f * p1 + p0)
                + (6f * t) * (p3 - 2f * p2 + p1);
        }
    }
}
