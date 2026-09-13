using System.Collections.Generic;

namespace CargoKing.Streets.Editor
{
    /// <summary>
    /// Decides which class of road a street gets when it is pulled out of an intersection socket.
    ///
    /// A street already docked to the same intersection wins, because the arms of one junction are
    /// built from the same kit. Otherwise the caller's fallback - the project's default profile.
    /// Width plays no part in the choice: a mismatch with the socket is reported when the street
    /// docks, not quietly worked around by picking some other class that happens to fit.
    /// </summary>
    public static class StreetProfileChoice
    {
        /// <returns>The profile to give the new street. Null when there is none to give.</returns>
        public static StreetProfile ForSocket(
            IntersectionSocket socket,
            IReadOnlyList<StreetSegment> segments,
            StreetProfile fallback)
        {
            Intersection intersection = socket != null ? socket.Owner : null;

            if (intersection != null && segments != null)
            {
                for (int index = 0; index < segments.Count; index++)
                {
                    StreetSegment segment = segments[index];

                    if (segment != null && segment.profile != null && DocksTo(segment, intersection))
                    {
                        return segment.profile;
                    }
                }
            }

            return fallback;
        }

        private static bool DocksTo(StreetSegment segment, Intersection intersection)
        {
            return Owns(intersection, segment.startConnection.socket)
                || Owns(intersection, segment.endConnection.socket);
        }

        private static bool Owns(Intersection intersection, IntersectionSocket socket)
        {
            return socket != null && socket.Owner == intersection;
        }
    }
}
