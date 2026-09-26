using System;

namespace NightShift.Core
{
    /// <summary>
    /// An undirected connection between two distinct nodes, up to
    /// <see cref="GameData.MaxLinkLength"/> cells apart (Manhattan distance). Equality and
    /// hashing use only the endpoints and ignore their order: <c>new Link(a, b, n)</c> equals
    /// <c>new Link(b, a, m)</c>. <see cref="NetworkGraph"/> relies on this to reject duplicate
    /// links whichever order the two node ids are passed in.
    /// </summary>
    /// <example>
    /// <code>
    /// if (sim.Graph.TryGetLink(fromId, toId, out Link link))
    /// {
    ///     float secondsToCross = link.Length / packetSpeedCellsPerSecond;
    /// }
    /// </code>
    /// </example>
    public readonly struct Link : IEquatable<Link>
    {
        public int NodeAId { get; }
        public int NodeBId { get; }

        /// <summary>
        /// Manhattan distance in grid cells between the two endpoints. Drives both the purchase
        /// cost (<see cref="GameData.GetLinkCost"/>) and packet travel time (length / speed).
        /// Not part of equality.
        /// </summary>
        public int Length { get; }

        public Link(int nodeAId, int nodeBId, int length)
        {
            // Canonicalize so equality/hashing is order-independent.
            if (nodeAId <= nodeBId)
            {
                NodeAId = nodeAId;
                NodeBId = nodeBId;
            }
            else
            {
                NodeAId = nodeBId;
                NodeBId = nodeAId;
            }
            Length = length;
        }

        public bool Contains(int nodeId) => NodeAId == nodeId || NodeBId == nodeId;

        public int OtherEnd(int nodeId)
        {
            if (nodeId == NodeAId) return NodeBId;
            if (nodeId == NodeBId) return NodeAId;
            throw new ArgumentException("nodeId is not an endpoint of this link.", nameof(nodeId));
        }

        public bool Equals(Link other) => NodeAId == other.NodeAId && NodeBId == other.NodeBId;

        public override bool Equals(object obj) => obj is Link other && Equals(other);

        public override int GetHashCode() => (NodeAId * 397) ^ NodeBId;

        public override string ToString() => $"Link({NodeAId}-{NodeBId}, len {Length})";
    }
}
