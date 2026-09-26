using NightShift.Core;
using UnityEngine;

namespace NightShift.Game
{
    /// <summary>
    /// The single authoring site for Story 002's playable content: the fixed starting network and
    /// night 1's spawn schedule. Implements acceptance criterion 5 of
    /// `production/epics/night-shift/story-002-unity-night-view.md` ("night 1 on a fixed starting
    /// network plays from start to finish").
    /// </summary>
    /// <remarks>
    /// <para><b>Why this lives in the Unity layer.</b> <c>NightShift.Core</c> ships the
    /// <see cref="NightData"/> and <see cref="NetworkGraph"/> types but deliberately no night-1
    /// instance and no fixed topology - those are content, not simulation. Story 005 replaces this
    /// class with the full five-night data set; nothing in the views reads a balance number
    /// directly, they all read <see cref="GameData"/> / <see cref="NightData"/> through the
    /// simulation.</para>
    ///
    /// <para><b>Determinism.</b> Both seeds below are fixed constants and the schedule is built
    /// from an injected <see cref="IRandomSource"/>, so every run of night 1 is identical.</para>
    ///
    /// <para><b>The starting network.</b> Gateway (0,3) and Core (11,3) are seeded by
    /// <see cref="NetworkGraph"/> itself. On top of those this class places two Firewalls and a
    /// Server in series along the main path, and hangs a Honeypot, a second Server and an Ids off
    /// it as dead-end branches. Because the branches are dead ends, the Gateway-to-Core shortest
    /// hop path is unique and packet routing is fully predictable.</para>
    ///
    /// <para><b>Expected night 1 outcome</b>, traced against
    /// <see cref="NetworkSimulation"/>'s arrival pipeline at the story-001 default
    /// <see cref="GameData"/> values: the Honeypot (capacity 3) captures the first three arrivals
    /// at the first Firewall; the two Firewalls together remove 40 HP, which kills every 30 HP
    /// Standard packet; Stealth packets are invisible to both Firewalls - the Ids sits off the main
    /// path on purpose, so it only reveals them at the relay Server, after the last Firewall - and
    /// leak for 8 Core damage each. Report: 9 blocked, 3 leaked, 0 dissipated, 24 Core damage, Core
    /// 76/100. Survivable but not free, and every counter on the shift report is non-zero.</para>
    /// </remarks>
    public static class NightContent
    {
        /// <summary>Seed for the simulation's own randomness stream.</summary>
        public const int SimulationSeed = 20260926;

        /// <summary>
        /// Seed for authoring night 1's spawn schedule. A separate stream, so schedule jitter can
        /// never shift simulation randomness.
        /// </summary>
        public const int ScheduleSeed = 1337;

        /// <summary>
        /// Credits the player owns when night 1 begins. The starting network is a given, not a
        /// purchase, so it is not credit-gated.
        /// </summary>
        public const float StartingCredits = 0f;

        // --- Night 1 schedule shape (content, authored here; Story 005 supersedes it) ---

        private const int Night1Number = 1;

        /// <summary>Night length in seconds. The brief specifies 5-7 minute nights; night 1 is the short end.</summary>
        private const float Night1DurationSeconds = 300f;

        private const float Night1FirstSpawnTime = 8f;
        private const float Night1SpawnInterval = 25f;
        private const int Night1PacketCount = 12;
        private const float Night1SpawnJitterSeconds = 2f;

        /// <summary>Every Nth spawn is a Stealth packet; the rest are Standard.</summary>
        private const int Night1StealthEveryNth = 3;

        /// <summary>Margin before the night ends after which nothing spawns, so every packet can resolve before the report.</summary>
        private const float Night1SpawnTailMargin = 12f;

        // --- Fixed starting network layout, in grid cells on the 12x7 grid ---

        private static readonly Vector2Int FirewallACell = new Vector2Int(3, 3);
        private static readonly Vector2Int FirewallBCell = new Vector2Int(6, 3);
        private static readonly Vector2Int RelayServerCell = new Vector2Int(8, 3);
        private static readonly Vector2Int SideServerCell = new Vector2Int(6, 1);
        private static readonly Vector2Int HoneypotCell = new Vector2Int(3, 5);
        private static readonly Vector2Int IdsCell = new Vector2Int(8, 5);

        /// <summary>
        /// The tuning set for Story 002: <see cref="GameData"/> at its story-001 defaults. Kept as a
        /// factory so a later story can re-balance in exactly one place.
        /// </summary>
        public static GameData CreateGameData() => new GameData();

        /// <summary>Builds night 1's deterministic spawn schedule.</summary>
        /// <param name="random">
        /// Seeded source, used only for spawn-time jitter. Pass
        /// <c>new SystemRandomSource(NightContent.ScheduleSeed)</c>.
        /// </param>
        public static NightData CreateNight1(IRandomSource random)
        {
            var night = new NightData
            {
                NightNumber = Night1Number,
                NightDuration = Night1DurationSeconds,
            };

            float latestSpawn = Night1DurationSeconds - Night1SpawnTailMargin;

            for (int i = 0; i < Night1PacketCount; i++)
            {
                float jitter = (float)(random.NextDouble() * 2d - 1d) * Night1SpawnJitterSeconds;
                float spawnTime = Mathf.Clamp(
                    Night1FirstSpawnTime + i * Night1SpawnInterval + jitter,
                    0f,
                    latestSpawn);

                PacketType type = (i + 1) % Night1StealthEveryNth == 0
                    ? PacketType.Stealth
                    : PacketType.Standard;

                night.SpawnSchedule.Add(new PacketSpawnEvent { SpawnTime = spawnTime, Type = type });
            }

            return night;
        }

        /// <summary>Places the fixed starting topology described in the class remarks.</summary>
        /// <remarks>
        /// Goes through <see cref="NetworkSimulation.Graph"/> rather than
        /// <see cref="NetworkSimulation.TryPlaceNode"/> on purpose: the starting network is given to
        /// the player, so it must not be charged against the opening credit balance.
        /// </remarks>
        public static void BuildStartingNetwork(NetworkSimulation simulation)
        {
            NetworkGraph graph = simulation.Graph;

            Node gateway = graph.GatewayNode;
            Node core = graph.CoreNode;

            Node firewallA = Place(graph, FirewallACell, NodeType.Firewall);
            Node firewallB = Place(graph, FirewallBCell, NodeType.Firewall);
            Node relayServer = Place(graph, RelayServerCell, NodeType.Server);
            Node sideServer = Place(graph, SideServerCell, NodeType.Server);
            Node honeypot = Place(graph, HoneypotCell, NodeType.Honeypot);
            Node ids = Place(graph, IdsCell, NodeType.Ids);

            // Main Gateway -> Core path.
            Connect(graph, gateway, firewallA);
            Connect(graph, firewallA, firewallB);
            Connect(graph, firewallB, relayServer);
            Connect(graph, relayServer, core);

            // Dead-end branches: they never appear on a shortest path, so routing stays unique.
            Connect(graph, firewallA, honeypot);
            Connect(graph, firewallB, sideServer);
            Connect(graph, relayServer, ids);
        }

        private static Node Place(NetworkGraph graph, Vector2Int cell, NodeType type)
        {
            if (graph.TryPlaceNode(cell.x, cell.y, type, out Node node, out string error))
            {
                return node;
            }

            Debug.LogError("[NightShift] Starting network: could not place " + type +
                           " at (" + cell.x + "," + cell.y + "): " + error);
            return null;
        }

        private static void Connect(NetworkGraph graph, Node a, Node b)
        {
            if (a == null || b == null)
            {
                return;
            }

            if (!graph.TryAddLink(a.Id, b.Id, out _, out string error))
            {
                Debug.LogError("[NightShift] Starting network: could not link node " + a.Id +
                               " to node " + b.Id + ": " + error);
            }
        }
    }
}
