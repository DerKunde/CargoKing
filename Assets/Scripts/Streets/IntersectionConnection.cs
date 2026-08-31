namespace CargoKing.Streets
{
    /// <summary>
    /// Which way a vehicle turns while crossing an intersection.
    /// </summary>
    public enum StreetTurn
    {
        Left,
        Straight,
        Right,
    }

    /// <summary>
    /// One driveable way through an intersection, from the socket a vehicle enters by to the socket it
    /// leaves by.
    ///
    /// The path itself is a plain <see cref="StreetLane"/>, the same type a street segment produces.
    /// That is deliberate: for the routing graph a way through an intersection is then just another
    /// lane, only shorter, and the search needs no second kind of edge.
    /// </summary>
    public class IntersectionConnection
    {
        public IntersectionConnection(int fromSocket, int toSocket, StreetTurn turn, StreetLane lane)
        {
            this.fromSocket = fromSocket;
            this.toSocket = toSocket;
            this.turn = turn;
            this.lane = lane;
        }

        private readonly int fromSocket;
        private readonly int toSocket;
        private readonly StreetTurn turn;
        private readonly StreetLane lane;

        /// <summary>Index of the socket a vehicle enters by.</summary>
        public int FromSocket => fromSocket;

        /// <summary>Index of the socket a vehicle leaves by.</summary>
        public int ToSocket => toSocket;

        public StreetTurn Turn => turn;

        /// <summary>The path across, in the local space of the intersection.</summary>
        public StreetLane Lane => lane;
    }
}
