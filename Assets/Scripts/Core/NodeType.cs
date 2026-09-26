namespace NightShift.Core
{
    /// <summary>
    /// The kind of network node that can exist on the simulation grid.
    /// </summary>
    public enum NodeType
    {
        /// <summary>Fixed entry point for attack packets. Exactly one per grid, left edge, never sellable.</summary>
        Gateway = 0,

        /// <summary>Produces credits per second while online and connected to both Gateway and Core.</summary>
        Server = 1,

        /// <summary>Fixed defense target. Exactly one per grid, right edge, never sellable. Has integrity.</summary>
        Core = 2,

        /// <summary>Security tool: removes HP from packets passing through it (its own node only).</summary>
        Firewall = 3,

        /// <summary>Security tool: reveals hidden packets arriving at its own node or any node directly linked to it; slows packets on its own links.</summary>
        Ids = 4,

        /// <summary>Security tool: diverts and destroys packets arriving at its own node or any node directly linked to it.</summary>
        Honeypot = 5,
    }
}
