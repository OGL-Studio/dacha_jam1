namespace NightShift.Core
{
    /// <summary>
    /// A node that appears in the network on its own, mid-night — Story 005 acceptance criterion 3
    /// («в сети сами появляются чужие ноды, связанные с существующими; их нельзя продать, только
    /// изолировать»). Pure data: <see cref="NetworkSimulation"/> reads the list off
    /// <see cref="NightData.AlienNodes"/> and never invents one.
    /// </summary>
    /// <remarks>
    /// <para><b>Placement is advisory, not absolute.</b> The player builds wherever they like, so
    /// the authored cell may already be taken by the time the spawn fires. The simulation then
    /// searches outward from (<see cref="X"/>, <see cref="Y"/>) in a deterministic order for the
    /// nearest free cell; only if the whole grid is full does the spawn do nothing.</para>
    ///
    /// <para><b>Linking is by proximity, not by id.</b> Authoring cannot know the ids of nodes the
    /// player built, so the spawn attaches itself to its <see cref="LinkCount"/> nearest existing
    /// nodes (within <see cref="GameData.MaxLinkLength"/>), nearest first, ties broken by ascending
    /// node id. That keeps the appearance deterministic for a given seed and player history.</para>
    /// </remarks>
    public sealed class AlienNodeSpawn
    {
        /// <summary>Seconds after night start at which the node appears.</summary>
        public float SpawnTime { get; set; }

        /// <summary>Preferred grid column. See the class remarks for what happens when it is occupied.</summary>
        public int X { get; set; }

        /// <summary>Preferred grid row.</summary>
        public int Y { get; set; }

        /// <summary>
        /// What the intruder presents itself as. Authored as <see cref="NodeType.Server"/> so it
        /// draws as a node the player recognises — but it is flagged <see cref="Node.IsAlien"/>,
        /// which bars it from earning income and from ever being removed by the player.
        /// </summary>
        public NodeType Type { get; set; } = NodeType.Server;

        /// <summary>How many existing nodes it wires itself into. At least 1 to matter at all.</summary>
        public int LinkCount { get; set; } = 1;
    }
}
