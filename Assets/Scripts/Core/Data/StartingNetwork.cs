namespace NightShift.Core.Data
{
    /// <summary>
    /// The fixed network the player is given on day 1 — the topology every authored night is
    /// balanced against, and the one the Story 005 winnability test builds
    /// (`tests/unit/core/five-nights-escalation_test.cs`).
    /// </summary>
    /// <remarks>
    /// <para><b>Why it lives in Core.</b> Until Story 005 this topology was authored in the Unity
    /// layer (<c>NightShift.Game.NightContent</c>), which put it out of reach of the headless test
    /// suite. Acceptance criterion 6 requires a test that a reasonable starting defence survives
    /// nights 1-2, and "reasonable" is only meaningful relative to the real starting network — so the
    /// network moved here, and the Unity layer now delegates to it. Engine-independent: grid cells
    /// are plain ints, not <c>Vector2Int</c>.</para>
    ///
    /// <para><b>Shape.</b> Gateway (0,3) and Core (11,3) are seeded by <see cref="NetworkGraph"/>
    /// itself. On top of those: two Firewalls and a relay Server in series along the main path, with
    /// a Honeypot, a second Server and an IDS hanging off it as dead-end branches. Because the
    /// branches are dead ends, the Gateway-to-Core shortest hop path is unique and packet routing is
    /// fully predictable.</para>
    ///
    /// <para><b>Not charged.</b> Placement goes through <see cref="NetworkSimulation.Graph"/> rather
    /// than <see cref="NetworkSimulation.TryPlaceNode"/>: the starting network is a given, so it must
    /// not be debited from the opening credit balance.</para>
    /// </remarks>
    public static class StartingNetwork
    {
        // Cells on the default 12x7 grid, as (column, row).
        private const int FirewallAX = 3, FirewallAY = 3;
        private const int FirewallBX = 6, FirewallBY = 3;
        private const int RelayServerX = 8, RelayServerY = 3;
        private const int SideServerX = 6, SideServerY = 1;
        private const int HoneypotX = 3, HoneypotY = 5;
        private const int IdsX = 8, IdsY = 5;

        /// <summary>
        /// Places the starting topology into <paramref name="simulation"/>'s graph.
        /// </summary>
        /// <param name="simulation">A freshly constructed simulation with an empty grid.</param>
        /// <param name="error">The first failure encountered, or an empty string on success.</param>
        /// <returns>True when every node and link was created.</returns>
        public static bool TryBuild(NetworkSimulation simulation, out string error)
        {
            if (simulation == null)
            {
                error = "Simulation is null.";
                return false;
            }

            NetworkGraph graph = simulation.Graph;
            error = string.Empty;

            Node gateway = graph.GatewayNode;
            Node core = graph.CoreNode;

            if (!TryPlace(graph, FirewallAX, FirewallAY, NodeType.Firewall, out Node firewallA, ref error) ||
                !TryPlace(graph, FirewallBX, FirewallBY, NodeType.Firewall, out Node firewallB, ref error) ||
                !TryPlace(graph, RelayServerX, RelayServerY, NodeType.Server, out Node relayServer, ref error) ||
                !TryPlace(graph, SideServerX, SideServerY, NodeType.Server, out Node sideServer, ref error) ||
                !TryPlace(graph, HoneypotX, HoneypotY, NodeType.Honeypot, out Node honeypot, ref error) ||
                !TryPlace(graph, IdsX, IdsY, NodeType.Ids, out Node ids, ref error))
            {
                return false;
            }

            // Main Gateway -> Core path.
            bool linked =
                TryLink(graph, gateway, firewallA, ref error) &&
                TryLink(graph, firewallA, firewallB, ref error) &&
                TryLink(graph, firewallB, relayServer, ref error) &&
                TryLink(graph, relayServer, core, ref error) &&
                // Dead-end branches: they never appear on a shortest path, so routing stays unique.
                TryLink(graph, firewallA, honeypot, ref error) &&
                TryLink(graph, firewallB, sideServer, ref error) &&
                TryLink(graph, relayServer, ids, ref error);

            return linked;
        }

        private static bool TryPlace(NetworkGraph graph, int x, int y, NodeType type, out Node node, ref string error)
        {
            if (graph.TryPlaceNode(x, y, type, out node, out string placeError))
            {
                return true;
            }

            error = "Could not place " + type + " at (" + x + "," + y + "): " + placeError;
            return false;
        }

        private static bool TryLink(NetworkGraph graph, Node a, Node b, ref string error)
        {
            if (graph.TryAddLink(a.Id, b.Id, out _, out string linkError))
            {
                return true;
            }

            error = "Could not link node " + a.Id + " to node " + b.Id + ": " + linkError;
            return false;
        }
    }
}
