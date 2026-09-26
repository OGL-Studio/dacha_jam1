using System;
using System.Collections.Generic;

namespace NightShift.Core
{
    /// <summary>
    /// The engine-independent simulation for one Night Shift game: grid-graph topology, build
    /// economy, packet movement, the three security tools, Core integrity, and the night
    /// lifecycle. Zero <c>UnityEngine</c> references — the Unity layer (Story 002+) renders this
    /// state and forwards player input into it.
    ///
    /// Time only advances through <see cref="Tick"/>; randomness only through the injected
    /// <see cref="IRandomSource"/>. Given the same constructor arguments and the same sequence of
    /// method calls (build actions, <see cref="Tick"/> calls with the same deltas), a
    /// <see cref="NetworkSimulation"/> always reaches the same state.
    /// </summary>
    /// <example>
    /// <code>
    /// var sim = new NetworkSimulation(new GameData(), new SystemRandomSource(12345), startingCredits: 200f);
    /// sim.TryPlaceNode(3, 3, NodeType.Server, out Node server, out string error);
    /// sim.TryAddLink(sim.Graph.GatewayNode.Id, server.Id, out _, out error);
    /// sim.OnNightEnded += report =&gt; System.Console.WriteLine($"Earned {report.CreditsEarned}");
    /// sim.StartNight(new NightData { NightDuration = 60f });
    /// sim.Tick(0.016f); // called every frame by the Unity layer
    /// </code>
    /// </example>
    public sealed class NetworkSimulation
    {
        private readonly List<Packet> _activePackets = new List<Packet>();
        private int _nextPacketId;

        private NightData _currentNightData;
        private List<PacketSpawnEvent> _orderedSchedule = new List<PacketSpawnEvent>();
        private int _nextScheduleIndex;
        private List<AlienNodeSpawn> _orderedAlienSpawns = new List<AlienNodeSpawn>();
        private int _nextAlienSpawnIndex;

        private float _globalRevealUntilTime = float.NegativeInfinity;
        private bool _coreDestroyedFired;

        private float _nightCreditsEarned;
        private int _nightBlockedCount;
        private int _nightLeakedCount;
        private int _nightDissipatedCount;
        private int _nightDamageTaken;

        public GameData Data { get; }
        public NetworkGraph Graph { get; }
        public IRandomSource Random { get; }

        /// <summary>Monotonic simulation clock in seconds, advanced only by <see cref="Tick"/>. Never reset.</summary>
        public float SimulationTime { get; private set; }

        /// <summary>Seconds elapsed since <see cref="StartNight"/> was last called. Reset to 0 at the start of each night.</summary>
        public float NightElapsedTime { get; private set; }

        public bool IsNightActive { get; private set; }

        /// <summary>The night currently loaded, or null before the first <see cref="StartNight"/>. Read-only to callers.</summary>
        public NightData CurrentNight => _currentNightData;

        /// <summary>
        /// «сеть вне контроля» — true once the final night's timer has run out (Story 005 acceptance
        /// criterion 5). The night does not report in this state: packets keep moving and the only
        /// command the terminal still accepts is <c>shutdown --all</c>.
        /// </summary>
        public bool IsOutOfControl { get; private set; }

        /// <summary>True once <see cref="ShutdownAll"/> has succeeded — the victory ending.</summary>
        public bool IsVictory { get; private set; }

        /// <summary>
        /// Probability in [0, 1] that a node-targeted terminal command hits the wrong node right
        /// now, straight off the current <see cref="NightData.CommandMisfireChance"/>. 0 outside a
        /// night. <see cref="TerminalCommandProcessor"/> reads this rather than holding a number of
        /// its own (Story 005 acceptance criterion 4).
        /// </summary>
        public float CommandMisfireChance =>
            IsNightActive && _currentNightData != null ? _currentNightData.CommandMisfireChance : 0f;

        public float Credits { get; private set; }

        public int CoreIntegrity { get; private set; }

        public IReadOnlyList<Packet> ActivePackets => _activePackets;

        /// <summary>Raised when a packet is destroyed by a Firewall or a Honeypot before reaching the Core.</summary>
        public event Action<Packet, PacketBlockReason> OnPacketBlocked;

        /// <summary>Raised when a packet reaches the Core (damage has already been applied by the time this fires).</summary>
        public event Action<Packet> OnPacketLeaked;

        /// <summary>Raised when a packet dissipates with no damage because no path to the Core existed.</summary>
        public event Action<Packet> OnPacketDissipated;

        /// <summary>Raised whenever the Core takes damage: (damage dealt, integrity remaining).</summary>
        public event Action<int, int> OnCoreDamaged;

        /// <summary>Raised once, the instant Core integrity reaches 0 (defeat).</summary>
        public event Action OnCoreDestroyed;

        /// <summary>Raised at the end of a night with the summary report.</summary>
        public event Action<NightReport> OnNightEnded;

        /// <summary>Raised whenever the credit balance changes (build spend or server income).</summary>
        public event Action<float> OnCreditsChanged;

        /// <summary>Raised when a «DDoS» packet takes a Server offline: (server, seconds of downtime).</summary>
        public event Action<Node, float> OnServerDowned;

        /// <summary>Raised when a node that was offline comes back by itself, its downtime having expired.</summary>
        public event Action<Node> OnServerRecovered;

        /// <summary>Raised when a «червь» infects a Server. The Server starts spawning worms of its own until <c>patch</c>.</summary>
        public event Action<Node> OnServerInfected;

        /// <summary>Raised when an intruder node appears in the network by itself, already linked in (Story 005 acceptance criterion 3).</summary>
        public event Action<Node> OnAlienNodeAppeared;

        /// <summary>Raised once, when the final night's timer expires and <see cref="IsOutOfControl"/> becomes true.</summary>
        public event Action OnOutOfControl;

        /// <summary>Raised once, when <c>shutdown --all</c> succeeds. Fires before the final <see cref="OnNightEnded"/>.</summary>
        public event Action OnVictory;

        public NetworkSimulation(GameData data, IRandomSource random, float startingCredits = 0f)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            Random = random ?? throw new ArgumentNullException(nameof(random));
            Graph = new NetworkGraph(data);
            Credits = startingCredits;
            CoreIntegrity = data.CoreStartingIntegrity;
        }

        // ------------------------------------------------------------------
        // Build actions
        // ------------------------------------------------------------------

        /// <summary>
        /// Places a node, atomically debiting its <see cref="GameData"/> cost. On any rejection
        /// (insufficient credits, occupied cell, out of bounds, Gateway/Core) the balance and
        /// grid are left completely unchanged.
        /// </summary>
        /// <example>
        /// <code>
        /// if (!sim.TryPlaceNode(3, 3, NodeType.Firewall, out Node node, out string error))
        /// {
        ///     Console.WriteLine($"Could not place: {error}");
        /// }
        /// </code>
        /// </example>
        public bool TryPlaceNode(int x, int y, NodeType type, out Node node, out string error)
        {
            node = null;

            int cost;
            try
            {
                cost = Data.GetNodeCost(type);
            }
            catch (ArgumentException ex)
            {
                error = ex.Message;
                return false;
            }

            if (Credits < cost)
            {
                error = $"Insufficient credits: need {cost}, have {Credits}.";
                return false;
            }

            if (!Graph.TryPlaceNode(x, y, type, out node, out error))
            {
                return false;
            }

            Credits -= cost;
            OnCreditsChanged?.Invoke(Credits);
            return true;
        }

        /// <summary>
        /// Links two nodes up to <see cref="GameData.MaxLinkLength"/> cells apart, atomically
        /// debiting <see cref="GameData.LinkCostPerCell"/> x Manhattan length. See
        /// <see cref="TryPlaceNode"/> for the atomicity guarantee.
        /// </summary>
        /// <example>
        /// <code>
        /// // Gateway at (0,3), server at (3,3): a 3-cell link costing 3 x LinkCostPerCell.
        /// sim.TryAddLink(sim.Graph.GatewayNode.Id, server.Id, out Link link, out string error);
        /// </code>
        /// </example>
        public bool TryAddLink(int nodeAId, int nodeBId, out Link link, out string error)
        {
            link = default;

            if (!Graph.CanAddLink(nodeAId, nodeBId, out int length, out error))
            {
                return false;
            }

            int cost = Data.GetLinkCost(length);
            if (Credits < cost)
            {
                error = $"Insufficient credits: need {cost}, have {Credits}.";
                return false;
            }

            if (!Graph.TryAddLink(nodeAId, nodeBId, out link, out error))
            {
                return false;
            }

            Credits -= cost;
            OnCreditsChanged?.Invoke(Credits);
            return true;
        }

        /// <summary>
        /// Removes an existing link and refunds <see cref="GameData.LinkRefundFraction"/> of what it
        /// cost to build. The counterpart to <see cref="TryAddLink"/>.
        /// </summary>
        /// <param name="refund">Credits actually returned; 0 when the call fails.</param>
        /// <param name="error">Why removal was refused; empty on success.</param>
        /// <remarks>
        /// The refund is derived from the link's stored <see cref="Link.Length"/> through
        /// <see cref="GameData.GetLinkCost"/>, so it tracks the real purchase price rather than a
        /// figure the caller supplies. Rounded down, so removing a link can never return more than
        /// it cost. Like the other build actions this is atomic: the graph is only mutated once the
        /// link is known to exist.
        /// </remarks>
        public bool TryRemoveLink(int nodeAId, int nodeBId, out int refund, out string error)
        {
            refund = 0;

            if (!Graph.TryRemoveLink(nodeAId, nodeBId, out Link removed, out error))
            {
                return false;
            }

            refund = (int)(Data.GetLinkCost(removed.Length) * Data.LinkRefundFraction);
            if (refund > 0)
            {
                Credits += refund;
                OnCreditsChanged?.Invoke(Credits);
            }

            return true;
        }

        /// <summary>Upgrades a security tool to level 2, atomically debiting its <see cref="GameData"/> upgrade cost. See <see cref="TryPlaceNode"/> for the atomicity guarantee.</summary>
        public bool TryUpgrade(int nodeId, out string error)
        {
            Node node = Graph.GetNode(nodeId);
            if (node == null)
            {
                error = "Node does not exist.";
                return false;
            }

            int cost;
            try
            {
                cost = Data.GetUpgradeCost(node.Type);
            }
            catch (ArgumentException ex)
            {
                error = ex.Message;
                return false;
            }

            if (node.Level >= 2)
            {
                error = "Node is already at max level.";
                return false;
            }

            if (Credits < cost)
            {
                error = $"Insufficient credits: need {cost}, have {Credits}.";
                return false;
            }

            if (!Graph.TryUpgrade(nodeId, out error))
            {
                return false;
            }

            Credits -= cost;
            OnCreditsChanged?.Invoke(Credits);
            return true;
        }

        // ------------------------------------------------------------------
        // Night lifecycle
        // ------------------------------------------------------------------

        /// <summary>
        /// Begins a night: expands <see cref="NightData.Waves"/> into the spawn schedule, queues the
        /// night's alien intrusions, resets the per-night clock, cursors and report counters, clears
        /// any leftover packets, and repairs whatever the previous night broke (worm infections and
        /// «DDoS» downtime — the day shift is assumed to have cleaned up).
        /// </summary>
        /// <remarks>
        /// Wave jitter is drawn here, from the injected <see cref="IRandomSource"/>, so the whole
        /// night's timing is fixed the moment the night starts and is reproducible for a given seed.
        /// </remarks>
        public void StartNight(NightData nightData)
        {
            _currentNightData = nightData ?? throw new ArgumentNullException(nameof(nightData));

            _orderedSchedule = new List<PacketSpawnEvent>(nightData.SpawnSchedule);
            AppendWaveSpawns(nightData, _orderedSchedule);
            _orderedSchedule.Sort((a, b) => a.SpawnTime.CompareTo(b.SpawnTime));
            _nextScheduleIndex = 0;

            _orderedAlienSpawns = new List<AlienNodeSpawn>(nightData.AlienNodes);
            _orderedAlienSpawns.Sort((a, b) => a.SpawnTime.CompareTo(b.SpawnTime));
            _nextAlienSpawnIndex = 0;

            NightElapsedTime = 0f;
            IsNightActive = true;
            IsOutOfControl = false;
            IsVictory = false;
            _coreDestroyedFired = false;
            ClearNodeAfflictions();

            _nightCreditsEarned = 0f;
            _nightBlockedCount = 0;
            _nightLeakedCount = 0;
            _nightDissipatedCount = 0;
            _nightDamageTaken = 0;

            foreach (Packet leftover in _activePackets)
            {
                leftover.IsDestroyed = true;
            }
            _activePackets.Clear();
        }

        /// <summary>
        /// Advances the simulation by <paramref name="deltaTime"/> seconds. Safe to call with a
        /// large delta (e.g. to fast-forward in a test or under a debug time-scale) — packet
        /// movement is resolved hop-by-hop within the call, not once per call, so results do not
        /// depend on how the caller chooses to slice time into calls.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (deltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime), "deltaTime must be >= 0.");
            }

            SimulationTime += deltaTime;

            if (!IsNightActive)
            {
                return;
            }

            NightElapsedTime += deltaTime;

            UpdateNodeRecovery();
            SpawnScheduledPackets();
            SpawnScheduledAlienNodes();
            SpawnInfectionPackets();
            UpdateServerIncome(deltaTime);

            for (int i = _activePackets.Count - 1; i >= 0; i--)
            {
                Packet packet = _activePackets[i];
                AdvancePacket(packet, deltaTime);
                if (packet.IsDestroyed)
                {
                    _activePackets.RemoveAt(i);
                }
            }

            if (CoreIntegrity <= 0)
            {
                if (!_coreDestroyedFired)
                {
                    _coreDestroyedFired = true;
                    IsNightActive = false;
                    OnCoreDestroyed?.Invoke();
                }
                return;
            }

            if (NightElapsedTime >= _currentNightData.NightDuration && !IsOutOfControl)
            {
                if (_currentNightData.EndsOutOfControl)
                {
                    EnterOutOfControl();
                }
                else
                {
                    EndNight();
                }
            }
        }

        /// <summary>Spawns a packet of <paramref name="type"/> at the Gateway right now, heading for the Core (bypassing the schedule). Useful for scripted/manual spawns and tests.</summary>
        /// <remarks>
        /// The returned packet may already have <see cref="Packet.IsDestroyed"/> set to true if it
        /// dissipated (no path to Core) immediately on arrival at the Gateway.
        /// </remarks>
        public Packet SpawnPacket(PacketType type) => SpawnPacket(type, WaveTargetKind.Core);

        /// <summary>
        /// Spawns a packet of <paramref name="type"/> at the Gateway aimed at
        /// <paramref name="target"/> — the Core, or a Server picked through the seeded
        /// <see cref="IRandomSource"/> for a «DDoS» wave.
        /// </summary>
        public Packet SpawnPacket(PacketType type, WaveTargetKind target) =>
            SpawnPacketAt(type, Graph.GatewayNode.Id, ResolveDestinationNodeId(target));

        /// <summary>
        /// Spawns a packet at an arbitrary node with an arbitrary destination and runs the arrival
        /// pipeline for its starting node. The general form behind <see cref="SpawnPacket(PacketType)"/>.
        /// </summary>
        /// <param name="type">Attack profile to spawn.</param>
        /// <param name="startNodeId">Node the packet appears at.</param>
        /// <param name="destinationNodeId">Node it travels towards. Re-targeted to the Core if it becomes unreachable.</param>
        public Packet SpawnPacketAt(PacketType type, int startNodeId, int destinationNodeId)
        {
            PacketDefinition definition = Data.GetPacketDefinition(type);
            _nextPacketId++;
            var packet = new Packet(_nextPacketId, type, definition, startNodeId, destinationNodeId);

            bool alive = RunArrivalPipeline(packet);
            if (alive)
            {
                _activePackets.Add(packet);
            }
            return packet;
        }

        /// <summary>
        /// Makes an intruder node appear now, linked into the existing network — the manual form of
        /// a <see cref="NightData.AlienNodes"/> entry (Story 005 acceptance criterion 3).
        /// </summary>
        /// <returns>The node, or null when the grid had no free cell.</returns>
        public Node SpawnAlienNode(AlienNodeSpawn spawn)
        {
            if (spawn == null)
            {
                throw new ArgumentNullException(nameof(spawn));
            }

            if (!Graph.TryCreateAlienNode(spawn.X, spawn.Y, spawn.Type, out Node node))
            {
                return null;
            }

            LinkAlienNode(node, spawn.LinkCount);
            OnAlienNodeAppeared?.Invoke(node);
            return node;
        }

        /// <summary>
        /// The <c>shutdown --all</c> ending: only legal once <see cref="IsOutOfControl"/> is true.
        /// Kills every packet in flight, ends the night, and reports a victory
        /// (<see cref="NightReport.Victory"/>).
        /// </summary>
        /// <returns>False — changing nothing — when the network is not out of control yet.</returns>
        public bool ShutdownAll()
        {
            if (!IsOutOfControl || IsVictory)
            {
                return false;
            }

            IsVictory = true;

            foreach (Packet packet in _activePackets)
            {
                packet.IsDestroyed = true;
            }
            _activePackets.Clear();

            OnVictory?.Invoke();
            EndNight();
            return true;
        }

        /// <summary>
        /// Picks the node a misfiring command hits instead of the one the player named — Story 005
        /// acceptance criterion 4. Rolls <see cref="CommandMisfireChance"/> on the seeded
        /// <see cref="IRandomSource"/>, so the same seed and the same typed commands always misfire
        /// in the same places.
        /// </summary>
        /// <param name="intended">The node the player actually named.</param>
        /// <param name="mustBeIsolatable">
        /// True for <c>isolate</c>: excludes the Gateway and the Core, which cannot be isolated, so a
        /// misfire always still does something rather than silently failing.
        /// </param>
        /// <param name="actual">The node to act on: a different one on a misfire, <paramref name="intended"/> otherwise.</param>
        /// <returns>True when the command misfired.</returns>
        public bool TryMisfireTarget(Node intended, bool mustBeIsolatable, out Node actual)
        {
            actual = intended;

            if (intended == null)
            {
                return false;
            }

            float chance = CommandMisfireChance;
            if (chance <= 0f || Random.NextDouble() >= chance)
            {
                return false;
            }

            var candidates = new List<Node>();
            foreach (Node node in Graph.AllNodes)
            {
                if (node.Id == intended.Id)
                {
                    continue;
                }

                if (mustBeIsolatable && (node.Id == Graph.GatewayNode.Id || node.Id == Graph.CoreNode.Id))
                {
                    continue;
                }

                candidates.Add(node);
            }

            if (candidates.Count == 0)
            {
                return false;
            }

            actual = candidates[Random.NextInt(0, candidates.Count)];
            return true;
        }

        /// <summary>True if a Firewall could currently detect this packet (either never hidden, or revealed — individually or via a temporary global <see cref="Reveal"/>).</summary>
        public bool IsPacketVisible(Packet packet)
        {
            if (!packet.RawStartsHidden) return true;
            if (packet.IndividuallyRevealed) return true;
            return SimulationTime < _globalRevealUntilTime;
        }

        // ------------------------------------------------------------------
        // Story 004 stub API — Isolate/Reveal/Patch.
        // Full command parsing and cooldowns are Story 004's job; these only flip the
        // internal state the terminal commands will eventually drive.
        // ------------------------------------------------------------------

        /// <summary>Cuts every link incident to <paramref name="nodeId"/> for <paramref name="duration"/> seconds. Gateway and Core cannot be isolated.</summary>
        public bool Isolate(int nodeId, float duration)
        {
            Node node = Graph.GetNode(nodeId);
            if (node == null)
            {
                return false;
            }

            if (node.Id == Graph.GatewayNode.Id || node.Id == Graph.CoreNode.Id)
            {
                return false;
            }

            node.IsolatedUntilTime = SimulationTime + duration;
            return true;
        }

        /// <summary>Temporarily reveals every currently-hidden packet for <paramref name="duration"/> seconds (the terminal `scan` command).</summary>
        public void Reveal(float duration)
        {
            _globalRevealUntilTime = SimulationTime + duration;
        }

        /// <summary>
        /// Brings a node back online and clears everything wrong with it: active isolation, «DDoS»
        /// downtime, and any «червь» infection (the terminal <c>patch</c> command). Clearing the
        /// infection is what stops an infected Server spawning further worms — Story 005 acceptance
        /// criterion 2's «до patch».
        /// </summary>
        public bool Patch(int nodeId)
        {
            Node node = Graph.GetNode(nodeId);
            if (node == null)
            {
                return false;
            }

            node.IsOnline = true;
            node.IsolatedUntilTime = float.NegativeInfinity;
            node.DownedUntilTime = float.NegativeInfinity;
            node.IsInfected = false;
            node.InfectionNextSpawnTime = float.NegativeInfinity;
            return true;
        }

        // ------------------------------------------------------------------
        // Internal simulation step helpers
        // ------------------------------------------------------------------

        /// <summary>
        /// Turns each <see cref="NightWave"/> into <see cref="PacketSpawnEvent"/>s at
        /// <c>StartTime + i * IntervalSeconds</c>, jittered through the seeded
        /// <see cref="IRandomSource"/> and clamped into the night.
        /// </summary>
        private void AppendWaveSpawns(NightData nightData, List<PacketSpawnEvent> into)
        {
            foreach (NightWave wave in nightData.Waves)
            {
                if (wave == null || wave.Count <= 0)
                {
                    continue;
                }

                for (int i = 0; i < wave.Count; i++)
                {
                    float spawnTime = wave.StartTime + i * wave.IntervalSeconds;

                    if (wave.JitterSeconds > 0f)
                    {
                        spawnTime += (float)(Random.NextDouble() * 2d - 1d) * wave.JitterSeconds;
                    }

                    if (spawnTime < 0f)
                    {
                        spawnTime = 0f;
                    }
                    else if (spawnTime > nightData.NightDuration)
                    {
                        spawnTime = nightData.NightDuration;
                    }

                    into.Add(new PacketSpawnEvent
                    {
                        SpawnTime = spawnTime,
                        Type = wave.Type,
                        Target = wave.Target,
                    });
                }
            }
        }

        /// <summary>
        /// The concrete node a wave's «цель» resolves to right now: the Core, or one Server chosen
        /// through the seeded <see cref="IRandomSource"/>. The player's own online Servers are
        /// preferred; alien or downed Servers are used only if there is nothing else, and the Core is
        /// the last resort when the network has no Server at all.
        /// </summary>
        private int ResolveDestinationNodeId(WaveTargetKind target)
        {
            if (target != WaveTargetKind.Server)
            {
                return Graph.CoreNode.Id;
            }

            var preferred = new List<Node>();
            var fallback = new List<Node>();

            foreach (Node node in Graph.AllNodes)
            {
                if (node.Type != NodeType.Server)
                {
                    continue;
                }

                if (!node.IsAlien && node.IsOnline)
                {
                    preferred.Add(node);
                }
                else
                {
                    fallback.Add(node);
                }
            }

            List<Node> pool = preferred.Count > 0 ? preferred : fallback;
            if (pool.Count == 0)
            {
                return Graph.CoreNode.Id;
            }

            return pool[Random.NextInt(0, pool.Count)].Id;
        }

        private void SpawnScheduledPackets()
        {
            while (_nextScheduleIndex < _orderedSchedule.Count &&
                   _orderedSchedule[_nextScheduleIndex].SpawnTime <= NightElapsedTime)
            {
                PacketSpawnEvent scheduled = _orderedSchedule[_nextScheduleIndex];
                _nextScheduleIndex++;
                SpawnPacket(scheduled.Type, scheduled.Target);
            }
        }

        private void SpawnScheduledAlienNodes()
        {
            while (_nextAlienSpawnIndex < _orderedAlienSpawns.Count &&
                   _orderedAlienSpawns[_nextAlienSpawnIndex].SpawnTime <= NightElapsedTime)
            {
                AlienNodeSpawn spawn = _orderedAlienSpawns[_nextAlienSpawnIndex];
                _nextAlienSpawnIndex++;
                SpawnAlienNode(spawn);
            }
        }

        /// <summary>
        /// Links a freshly appeared alien node into its nearest existing neighbours — nearest by
        /// Manhattan distance, ties broken by ascending node id, so the topology it creates is
        /// deterministic. The links are free: the network built them, not the player.
        /// </summary>
        private void LinkAlienNode(Node alien, int linkCount)
        {
            int wanted = linkCount > 0 ? linkCount : 1;

            var candidates = new List<Node>();
            foreach (Node other in Graph.AllNodes)
            {
                if (other.Id == alien.Id)
                {
                    continue;
                }

                if (!Graph.CanAddLink(alien.Id, other.Id, out _, out _))
                {
                    continue;
                }

                candidates.Add(other);
            }

            candidates.Sort((a, b) =>
            {
                int distanceA = ManhattanDistance(alien, a);
                int distanceB = ManhattanDistance(alien, b);
                return distanceA != distanceB ? distanceA.CompareTo(distanceB) : a.Id.CompareTo(b.Id);
            });

            int linked = 0;
            for (int i = 0; i < candidates.Count && linked < wanted; i++)
            {
                if (Graph.TryAddLink(alien.Id, candidates[i].Id, out _, out _))
                {
                    linked++;
                }
            }
        }

        private static int ManhattanDistance(Node a, Node b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

        /// <summary>
        /// Emits the next batch of worms from every infected Server. Keeps emitting while the node's
        /// next-spawn time is in the past, so a large <see cref="Tick"/> delta catches up instead of
        /// swallowing beats.
        /// </summary>
        private void SpawnInfectionPackets()
        {
            IReadOnlyList<Node> nodes = Graph.AllNodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                Node node = nodes[i];
                if (!node.IsInfected)
                {
                    continue;
                }

                PacketDefinition definition = Data.GetPacketDefinition(node.InfectionPacketType);
                float interval = definition.InfectionSpawnIntervalSeconds;
                if (interval <= 0f)
                {
                    continue; // Guards against an endless catch-up loop on misconfigured data.
                }

                int beatBudget = 16;
                while (node.IsInfected && SimulationTime >= node.InfectionNextSpawnTime && beatBudget-- > 0)
                {
                    for (int spawn = 0; spawn < Data.WormsPerInfectionSpawn; spawn++)
                    {
                        SpawnInfectionPacketFrom(node, definition);
                    }

                    node.InfectionNextSpawnTime += interval;
                }
            }
        }

        /// <summary>
        /// Sends one worm from an infected node towards a neighbour of its own — an uninfected
        /// Server neighbour if there is one, otherwise any reachable neighbour, chosen through the
        /// seeded <see cref="IRandomSource"/>. The packet does not run the arrival pipeline at its
        /// source: it is leaving that node, not arriving at it.
        /// </summary>
        private Packet SpawnInfectionPacketFrom(Node source, PacketDefinition definition)
        {
            IReadOnlyList<int> neighborIds =
                Graph.GetActiveNeighbors(source.Id, SimulationTime, definition.CanCrossMissingLinks);
            if (neighborIds.Count == 0)
            {
                return null;
            }

            var preferred = new List<int>();
            foreach (int neighborId in neighborIds)
            {
                Node neighbor = Graph.GetNode(neighborId);
                if (neighbor.Type == NodeType.Server && !neighbor.IsInfected)
                {
                    preferred.Add(neighborId);
                }
            }

            IReadOnlyList<int> pool = preferred.Count > 0 ? preferred : neighborIds;
            int firstHopId = pool[Random.NextInt(0, pool.Count)];

            _nextPacketId++;
            var packet = new Packet(_nextPacketId, definition.Type, definition, source.Id, Graph.CoreNode.Id)
            {
                TargetNodeId = firstHopId,
            };

            _activePackets.Add(packet);
            return packet;
        }

        /// <summary>Brings nodes whose «DDoS» downtime has expired back online.</summary>
        private void UpdateNodeRecovery()
        {
            IReadOnlyList<Node> nodes = Graph.AllNodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                Node node = nodes[i];
                if (float.IsNegativeInfinity(node.DownedUntilTime) || node.IsDownedAt(SimulationTime))
                {
                    continue;
                }

                node.DownedUntilTime = float.NegativeInfinity;
                if (!node.IsOnline)
                {
                    node.IsOnline = true;
                    OnServerRecovered?.Invoke(node);
                }
            }
        }

        /// <summary>
        /// Clears worm infections and «DDoS» downtime at the start of a night: the day shift is
        /// assumed to have repaired the hardware, so escalation comes from the night's own data
        /// rather than from accumulated rot the player can no longer reach.
        /// </summary>
        private void ClearNodeAfflictions()
        {
            foreach (Node node in Graph.AllNodes)
            {
                node.IsInfected = false;
                node.InfectionNextSpawnTime = float.NegativeInfinity;

                if (!float.IsNegativeInfinity(node.DownedUntilTime))
                {
                    node.DownedUntilTime = float.NegativeInfinity;
                    node.IsOnline = true;
                }
            }
        }

        private void UpdateServerIncome(float dt)
        {
            bool anyIncome = false;
            foreach (Node node in Graph.AllNodes)
            {
                if (node.Type != NodeType.Server || !node.IsOnline || node.IsAlien)
                {
                    continue;
                }

                if (!Graph.HasPath(node.Id, Graph.GatewayNode.Id, SimulationTime)) continue;
                if (!Graph.HasPath(node.Id, Graph.CoreNode.Id, SimulationTime)) continue;

                float income = Data.ServerIncomePerSecond * dt;
                Credits += income;
                _nightCreditsEarned += income;
                anyIncome = true;
            }

            if (anyIncome)
            {
                OnCreditsChanged?.Invoke(Credits);
            }
        }

        private void AdvancePacket(Packet packet, float dt)
        {
            float remaining = dt;
            int hopBudget = 256; // Guards against pathological infinite loops within a single Tick.

            while (remaining > 0f && !packet.IsDestroyed && hopBudget-- > 0)
            {
                float effectiveSpeed = GetEffectiveSpeed(packet);
                if (effectiveSpeed <= 0f)
                {
                    return; // No progress possible this tick; packet stays parked on its current link.
                }

                // Speed is in cells/second, so crossing a link of length L takes L / speed seconds.
                // LinkProgress stays a 0..1 fraction of the link regardless of its length.
                if (!Graph.TryGetTraversalLength(
                        packet.CurrentNodeId, packet.TargetNodeId, packet.CanCrossMissingLinks, out int traversalLength))
                {
                    throw new InvalidOperationException(
                        $"Packet {packet.Id} is travelling between nodes {packet.CurrentNodeId} and {packet.TargetNodeId}, which are not linked.");
                }

                float linkLength = traversalLength;
                float cellsToTarget = (1f - packet.LinkProgress) * linkLength;
                float timeToArrive = cellsToTarget / effectiveSpeed;

                if (remaining < timeToArrive)
                {
                    packet.LinkProgress += effectiveSpeed * remaining / linkLength;
                    return;
                }

                remaining -= timeToArrive;
                packet.CurrentNodeId = packet.TargetNodeId;
                packet.LinkProgress = 0f;

                if (!RunArrivalPipeline(packet))
                {
                    return; // destroyed: blocked, leaked, or dissipated
                }
            }
        }

        private float GetEffectiveSpeed(Packet packet)
        {
            PacketDefinition definition = Data.GetPacketDefinition(packet.Type);
            float baseSpeed = definition.SpeedCellsPerSecond;

            float multiplier = 1f;
            Node from = Graph.GetNode(packet.CurrentNodeId);
            Node to = Graph.GetNode(packet.TargetNodeId);

            if (from.Type == NodeType.Ids && from.IsOnline)
            {
                multiplier = Math.Min(multiplier, Data.GetIdsSlowMultiplier(from.Level));
            }
            if (to.Type == NodeType.Ids && to.IsOnline)
            {
                multiplier = Math.Min(multiplier, Data.GetIdsSlowMultiplier(to.Level));
            }

            return baseSpeed * multiplier;
        }

        /// <summary>
        /// Applies every per-node effect (IDS reveal, Firewall filter, Honeypot capture, Core
        /// damage) for a packet that has just arrived at <see cref="Packet.CurrentNodeId"/>, then
        /// recomputes its path to the Core. Returns false if the packet was destroyed (blocked,
        /// leaked, or dissipated) — the packet's <see cref="Packet.TargetNodeId"/> is only updated
        /// when this returns true.
        /// </summary>
        private bool RunArrivalPipeline(Packet packet)
        {
            Node node = Graph.GetNode(packet.CurrentNodeId);
            IReadOnlyList<int> neighborIds =
                Graph.GetActiveNeighbors(node.Id, SimulationTime, packet.CanCrossMissingLinks);

            // 1. IDS reveal — applies at the IDS's own node AND at nodes directly linked to it.
            if (packet.RawStartsHidden && !packet.IndividuallyRevealed)
            {
                if (IsOnlineOfType(node, NodeType.Ids) || AnyNeighborOfType(neighborIds, NodeType.Ids))
                {
                    packet.IndividuallyRevealed = true;
                }
            }

            // 2. Firewall — only affects visible packets, passing through the Firewall's own node.
            if (node.Type == NodeType.Firewall && node.IsOnline && IsPacketVisible(packet))
            {
                int filter = Data.GetFirewallFilter(node.Level);
                packet.CurrentHp -= filter;
                if (packet.CurrentHp <= 0)
                {
                    packet.IsDestroyed = true;
                    _nightBlockedCount++;
                    OnPacketBlocked?.Invoke(packet, PacketBlockReason.Firewall);
                    return false;
                }
            }

            // 3. Honeypot — captures at the Honeypot's own node AND at nodes directly linked to it.
            Node honeypot = FindActiveHoneypot(node, neighborIds);
            if (honeypot != null)
            {
                honeypot.HoneypotCapacityRemaining--;
                packet.IsDestroyed = true;
                _nightBlockedCount++;
                OnPacketBlocked?.Invoke(packet, PacketBlockReason.Honeypot);
                return false;
            }

            // 4. «червь» — the first healthy Server it reaches becomes a spawn source until `patch`.
            if (packet.InfectsServer && node.Type == NodeType.Server && !node.IsInfected)
            {
                InfectNode(node, packet);
                packet.IsDestroyed = true;
                _nightLeakedCount++;
                OnPacketLeaked?.Invoke(packet);
                return false;
            }

            // 5. Core — always damages, whatever the packet was nominally aimed at.
            if (node.Id == Graph.CoreNode.Id)
            {
                ApplyCoreDamage(packet);
                packet.IsDestroyed = true;
                _nightLeakedCount++;
                OnPacketLeaked?.Invoke(packet);
                return false;
            }

            // 6. «DDoS» — arriving at the Server it was aimed at takes that Server down.
            if (node.Id == packet.DestinationNodeId)
            {
                if (packet.DisablesTargetServer && node.Type == NodeType.Server)
                {
                    DownNode(node, packet);
                }

                packet.IsDestroyed = true;
                _nightLeakedCount++;
                OnPacketLeaked?.Invoke(packet);
                return false;
            }

            // 7. Recompute shortest-hop path to the destination from here, falling back to the Core
            //    when the destination Server has become unreachable (isolated, or its links cut).
            List<int> path = Graph.FindShortestHopPath(
                node.Id, packet.DestinationNodeId, SimulationTime, packet.CanCrossMissingLinks);

            if (path == null && packet.DestinationNodeId != Graph.CoreNode.Id)
            {
                packet.DestinationNodeId = Graph.CoreNode.Id;
                path = Graph.FindShortestHopPath(
                    node.Id, packet.DestinationNodeId, SimulationTime, packet.CanCrossMissingLinks);
            }

            if (path == null || path.Count < 2)
            {
                packet.IsDestroyed = true;
                _nightDissipatedCount++;
                OnPacketDissipated?.Invoke(packet);
                return false;
            }

            packet.TargetNodeId = path[1];
            packet.LinkProgress = 0f;
            return true;
        }

        private static bool IsOnlineOfType(Node node, NodeType type) => node.Type == type && node.IsOnline;

        private bool AnyNeighborOfType(IReadOnlyList<int> neighborIds, NodeType type)
        {
            foreach (int neighborId in neighborIds)
            {
                if (IsOnlineOfType(Graph.GetNode(neighborId), type))
                {
                    return true;
                }
            }
            return false;
        }

        private Node FindActiveHoneypot(Node arrivalNode, IReadOnlyList<int> neighborIds)
        {
            if (IsActiveHoneypot(arrivalNode))
            {
                return arrivalNode;
            }

            foreach (int neighborId in neighborIds)
            {
                Node neighbor = Graph.GetNode(neighborId);
                if (IsActiveHoneypot(neighbor))
                {
                    return neighbor;
                }
            }

            return null;
        }

        private static bool IsActiveHoneypot(Node node) =>
            node.Type == NodeType.Honeypot && node.IsOnline && node.HoneypotCapacityRemaining > 0;

        /// <summary>Marks a Server as a «червь» spawn source, using the arriving packet's own profile for the spawn beat.</summary>
        private void InfectNode(Node node, Packet packet)
        {
            PacketDefinition definition = Data.GetPacketDefinition(packet.Type);
            node.IsInfected = true;
            node.InfectionPacketType = packet.Type;
            node.InfectionNextSpawnTime = SimulationTime + definition.InfectionSpawnIntervalSeconds;
            OnServerInfected?.Invoke(node);
        }

        /// <summary>Takes a Server offline for the arriving «DDoS» packet's <see cref="PacketDefinition.ServerDownSeconds"/>.</summary>
        private void DownNode(Node node, Packet packet)
        {
            PacketDefinition definition = Data.GetPacketDefinition(packet.Type);
            node.IsOnline = false;
            node.DownedUntilTime = SimulationTime + definition.ServerDownSeconds;
            OnServerDowned?.Invoke(node, definition.ServerDownSeconds);
        }

        private void EnterOutOfControl()
        {
            IsOutOfControl = true;
            OnOutOfControl?.Invoke();
        }

        private void ApplyCoreDamage(Packet packet)
        {
            CoreIntegrity = Math.Max(0, CoreIntegrity - packet.CoreDamage);
            _nightDamageTaken += packet.CoreDamage;
            OnCoreDamaged?.Invoke(packet.CoreDamage, CoreIntegrity);
        }

        private void EndNight()
        {
            IsNightActive = false;

            var report = new NightReport
            {
                NightNumber = _currentNightData.NightNumber,
                CreditsEarned = _nightCreditsEarned,
                PacketsBlocked = _nightBlockedCount,
                PacketsLeaked = _nightLeakedCount,
                PacketsDissipated = _nightDissipatedCount,
                CoreDamageTaken = _nightDamageTaken,
                RemainingCoreIntegrity = CoreIntegrity,
                CoreDestroyed = CoreIntegrity <= 0,
                Victory = IsVictory,
            };

            OnNightEnded?.Invoke(report);
        }
    }
}
