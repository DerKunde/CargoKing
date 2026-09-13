using System.Collections.Generic;
using UnityEngine;

namespace CargoKing.Streets.Editor
{
    public enum StreetNetworkIssueSeverity
    {
        /// <summary>Worth knowing about. Does not stop a bake.</summary>
        Warning,

        /// <summary>Would bake into a network that cannot be driven. Stops the bake.</summary>
        Error,
    }

    /// <summary>One thing wrong with the network, and the object to select to go and fix it.</summary>
    public struct StreetNetworkIssue
    {
        public StreetNetworkIssueSeverity severity;
        public string message;
        public Object target;
    }

    /// <summary>
    /// Checks a network before it is baked.
    ///
    /// The line between warning and error is whether the result would still be driveable. An open end
    /// is a cul-de-sac; two streets on one socket is geometry that overlaps itself.
    /// </summary>
    public static class StreetNetworkValidation
    {
        /// <summary>How far two carriageway widths may differ at a seam before it matters, in metres.</summary>
        private const float WidthTolerance = 0.01f;

        /// <summary>
        /// Collects everything wrong with the network.
        /// </summary>
        /// <returns>False when at least one error was found, meaning the bake must not run.</returns>
        public static bool Run(
            IReadOnlyList<StreetSegment> segments,
            IReadOnlyList<Intersection> intersections,
            List<StreetNetworkIssue> issues)
        {
            issues.Clear();

            Dictionary<IntersectionSocket, StreetSegment> occupancy =
                new Dictionary<IntersectionSocket, StreetSegment>();

            for (int index = 0; index < segments.Count; index++)
            {
                StreetSegment segment = segments[index];
                if (segment == null)
                {
                    continue;
                }

                CheckEnd(segment, StreetEnd.Start, occupancy, issues);
                CheckEnd(segment, StreetEnd.End, occupancy, issues);
            }

            for (int index = 0; index < intersections.Count; index++)
            {
                Intersection intersection = intersections[index];

                if (intersection != null && intersection.Sockets.Count == 0)
                {
                    issues.Add(new StreetNetworkIssue
                    {
                        severity = StreetNetworkIssueSeverity.Warning,
                        message = $"Intersection '{intersection.name}' has no active sockets.",
                        target = intersection,
                    });
                }
            }

            for (int index = 0; index < issues.Count; index++)
            {
                if (issues[index].severity == StreetNetworkIssueSeverity.Error)
                {
                    return false;
                }
            }

            return true;
        }

        private static void CheckEnd(
            StreetSegment segment,
            StreetEnd end,
            Dictionary<IntersectionSocket, StreetSegment> occupancy,
            List<StreetNetworkIssue> issues)
        {
            StreetEndConnector connector = segment.ConnectorAt(end);

            if (connector == null || !connector.IsConnected)
            {
                issues.Add(new StreetNetworkIssue
                {
                    severity = StreetNetworkIssueSeverity.Warning,
                    message = $"'{segment.name}' has an open {end} end. Traffic will run out of road there.",
                    target = segment,
                });

                return;
            }

            if (connector.socket == null)
            {
                return;
            }

            if (occupancy.TryGetValue(connector.socket, out StreetSegment other))
            {
                issues.Add(new StreetNetworkIssue
                {
                    severity = StreetNetworkIssueSeverity.Error,
                    message =
                        $"'{segment.name}' and '{other.name}' both dock to socket '{connector.socket.name}'.",
                    target = segment,
                });
            }
            else
            {
                occupancy[connector.socket] = segment;
            }

            if (Mathf.Abs(connector.socket.roadWidth - segment.roadWidth) > WidthTolerance)
            {
                issues.Add(new StreetNetworkIssue
                {
                    severity = StreetNetworkIssueSeverity.Error,
                    message =
                        $"'{segment.name}' is {segment.roadWidth} m wide but socket "
                        + $"'{connector.socket.name}' expects {connector.socket.roadWidth} m. "
                        + "The lanes would miss each other at the seam.",
                    target = segment,
                });
            }
        }
    }
}
