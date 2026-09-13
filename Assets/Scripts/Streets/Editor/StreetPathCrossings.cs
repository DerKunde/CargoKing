using UnityEngine;

namespace CargoKing.Streets.Editor
{
    /// <summary>
    /// Works out which paths of an intersection conflict with which, and writes that down as a bit per
    /// path.
    ///
    /// Done once at bake time so the runtime never has to intersect anything: granting a reservation
    /// becomes a single AND against the mask of what is already granted.
    /// </summary>
    public static class StreetPathCrossings
    {
        /// <summary>How close two paths have to come before they count as conflicting, in metres.
        /// Roughly half a car's width - paths that pass this close cannot both be driven.</summary>
        private const float ClearanceDistance = 1.5f;

        /// <summary>How close two path ends have to be to count as the same exit, in metres.</summary>
        private const float SameExitDistance = 2f;

        /// <summary>Bits available for paths of one intersection. A six-arm junction stays inside this.</summary>
        private const int MaximumPaths = 31;

        /// <summary>Fills in the crossing mask of every intersection path in the result.</summary>
        public static void Apply(StreetNetworkBakeResult result)
        {
            for (int first = 0; first < result.lanes.Count; first++)
            {
                StreetNetworkLane one = result.lanes[first];

                if (one.intersection < 0 || one.pathIndex < 0 || one.pathIndex >= MaximumPaths)
                {
                    continue;
                }

                for (int second = first + 1; second < result.lanes.Count; second++)
                {
                    StreetNetworkLane other = result.lanes[second];

                    if (other.intersection != one.intersection
                        || other.pathIndex < 0
                        || other.pathIndex >= MaximumPaths)
                    {
                        continue;
                    }

                    if (!Conflicts(result, one, other))
                    {
                        continue;
                    }

                    one.crossingMask |= 1 << other.pathIndex;
                    other.crossingMask |= 1 << one.pathIndex;

                    result.lanes[second] = other;
                }

                result.lanes[first] = one;
            }
        }

        /// <summary>
        /// Whether two paths of the same intersection can be driven at the same time. They cannot when
        /// their courses come within a car's width of each other, and they cannot when they end at the
        /// same exit - crossing and merging are the same problem for whoever is in the way.
        /// </summary>
        public static bool Conflicts(
            StreetNetworkBakeResult result,
            in StreetNetworkLane first,
            in StreetNetworkLane second)
        {
            if (first.sampleCount < 2 || second.sampleCount < 2)
            {
                return false;
            }

            if (EndsTogether(result, first, second))
            {
                return true;
            }

            bool sharesStart = SharesStart(result, first, second);

            for (int a = 0; a < first.sampleCount - 1; a++)
            {
                Vector3 fromA = result.samples[first.firstSample + a].position;
                Vector3 toA = result.samples[first.firstSample + a + 1].position;

                for (int b = 0; b < second.sampleCount - 1; b++)
                {
                    Vector3 fromB = result.samples[second.firstSample + b].position;
                    Vector3 toB = result.samples[second.firstSample + b + 1].position;

                    if (Near(fromA, toA, fromB, toB) > ClearanceDistance)
                    {
                        continue;
                    }

                    // Paths leaving the same arm share their first metres. That is a queue, not a
                    // conflict, so a shared start is not enough on its own.
                    if (sharesStart && a == 0 && b == 0)
                    {
                        continue;
                    }

                    return true;
                }
            }

            return false;
        }

        private static bool EndsTogether(
            StreetNetworkBakeResult result,
            in StreetNetworkLane first,
            in StreetNetworkLane second)
        {
            Vector3 endOfFirst = result.samples[first.firstSample + first.sampleCount - 1].position;
            Vector3 endOfSecond = result.samples[second.firstSample + second.sampleCount - 1].position;

            return Vector3.Distance(endOfFirst, endOfSecond) <= SameExitDistance;
        }

        private static bool SharesStart(
            StreetNetworkBakeResult result,
            in StreetNetworkLane first,
            in StreetNetworkLane second)
        {
            Vector3 startOfFirst = result.samples[first.firstSample].position;
            Vector3 startOfSecond = result.samples[second.firstSample].position;

            return Vector3.Distance(startOfFirst, startOfSecond) <= SameExitDistance;
        }

        /// <summary>
        /// Shortest distance between two line segments in the ground plane. Height is ignored on
        /// purpose: an intersection is flat enough, and a bridge is a separate intersection.
        /// </summary>
        private static float Near(Vector3 fromA, Vector3 toA, Vector3 fromB, Vector3 toB)
        {
            Vector2 p = new Vector2(fromA.x, fromA.z);
            Vector2 q = new Vector2(fromB.x, fromB.z);
            Vector2 u = new Vector2(toA.x, toA.z) - p;
            Vector2 v = new Vector2(toB.x, toB.z) - q;
            Vector2 w = p - q;

            float a = Vector2.Dot(u, u);
            float b = Vector2.Dot(u, v);
            float c = Vector2.Dot(v, v);
            float d = Vector2.Dot(u, w);
            float e = Vector2.Dot(v, w);

            float denominator = a * c - b * b;
            float s;
            float t;

            if (denominator < 0.000001f)
            {
                // Parallel. Any point of the first will do to measure against the second.
                s = 0f;
                t = c > 0f ? Mathf.Clamp01(e / c) : 0f;
            }
            else
            {
                s = Mathf.Clamp01((b * e - c * d) / denominator);
                t = Mathf.Clamp01((a * e - b * d) / denominator);
            }

            Vector2 closestOnFirst = p + u * s;
            Vector2 closestOnSecond = q + v * t;

            return Vector2.Distance(closestOnFirst, closestOnSecond);
        }
    }
}
