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

        /// <summary>Begins a night: resets the per-night clock, schedule cursor, and report counters, and clears any leftover packets from a previous night.</summary>
        public void StartNight(NightData nightData)
        {
            _currentNightData = nightData ?? throw new ArgumentNullException(nameof(nightData));

            _orderedSchedule = new List<PacketSpawnEvent>(nightData.SpawnSchedule);
            _orderedSchedule.Sort((a, b) => a.SpawnTime.CompareTo(b.SpawnTime));
            _nextScheduleIndex = 0;

            NightElapsedTime = 0f;
            IsNightActive = true;
            _coreDestroyedFired = false;

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

            SpawnScheduledPackets();
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

            if (NightElapsedTime >= _currentNightData.NightDuration)
            {
                EndNight();
            }
        }

        /// <summary>Spawns a packet of <paramref name="type"/> at the Gateway right now (bypassing the schedule). Useful for scripted/manual spawns and tests.</summary>
        /// <remarks>
        /// The returned packet may already have <see cref="Packet.IsDestroyed"/> set to true if it
        /// dissipated (no path to Core) immediately on arrival at the Gateway.
        /// </remarks>
        public Packet SpawnPacket(PacketType type)
        {
            PacketDefinition definition = Data.GetPacketDefinition(type);
            _nextPacketId++;
            var packet = new Packet(_nextPacketId, type, definition, Graph.GatewayNode.Id);

            bool alive = RunArrivalPipeline(packet);
            if (alive)
            {
                _activePackets.Add(packet);
            }
            return packet;
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

        /// <summary>Brings a node back online and clears any active isolation on it (the terminal `patch` command).</summary>
        public bool Patch(int nodeId)
        {
            Node node = Graph.GetNode(nodeId);
            if (node == null)
            {
                return false;
            }

            node.IsOnline = true;
            node.IsolatedUntilTime = float.NegativeInfinity;
            return true;
        }

        // ------------------------------------------------------------------
        // Internal simulation step helpers
        // ------------------------------------------------------------------

        private void SpawnScheduledPackets()
        {
            while (_nextScheduleIndex < _orderedSchedule.Count &&
                   _orderedSchedule[_nextScheduleIndex].SpawnTime <= NightElapsedTime)
            {
                PacketSpawnEvent scheduled = _orderedSchedule[_nextScheduleIndex];
                _nextScheduleIndex++;
                SpawnPacket(scheduled.Type);
            }
        }

        private void UpdateServerIncome(float dt)
        {
            bool anyIncome = false;
            foreach (Node node in Graph.AllNodes)
            {
                if (node.Type != NodeType.Server || !node.IsOnline)
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
                if (!Graph.TryGetLink(packet.CurrentNodeId, packet.TargetNodeId, out Link link))
                {
                    throw new InvalidOperationException(
                        $"Packet {packet.Id} is travelling between nodes {packet.CurrentNodeId} and {packet.TargetNodeId}, which are not linked.");
                }

                float linkLength = link.Length;
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
            IReadOnlyList<int> neighborIds = Graph.GetActiveNeighbors(node.Id, SimulationTime);

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

            // 4. Core.
            if (node.Id == Graph.CoreNode.Id)
            {
                ApplyCoreDamage(packet);
                packet.IsDestroyed = true;
                _nightLeakedCount++;
                OnPacketLeaked?.Invoke(packet);
                return false;
            }

            // 5. Recompute shortest-hop path to Core from here.
            List<int> path = Graph.FindShortestHopPath(node.Id, Graph.CoreNode.Id, SimulationTime);
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
            };

            OnNightEnded?.Invoke(report);
        }
    }
}
