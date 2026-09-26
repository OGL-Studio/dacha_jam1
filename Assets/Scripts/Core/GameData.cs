using System;
using System.Collections.Generic;

namespace NightShift.Core
{
    /// <summary>
    /// Every tuning number the simulation reads: build costs, security tool stats, server
    /// income, Core integrity, packet profiles, grid size. Nothing in <c>NightShift.Core</c>
    /// hardcodes a gameplay number outside of this class — balance a night entirely by editing
    /// an instance of it.
    /// </summary>
    /// <remarks>
    /// This is a plain C# class, not a <c>ScriptableObject</c> — <c>NightShift.Core</c> has zero
    /// <c>UnityEngine</c> references. A Unity-facing asset wrapper that exposes these same fields
    /// in the Inspector belongs to the Unity layer (Story 002+), not here.
    /// </remarks>
    /// <example>
    /// <code>
    /// var data = new GameData();
    /// data.ServerCost = 75; // re-balance without touching simulation code
    /// var sim = new NetworkSimulation(data, new SystemRandomSource(12345));
    /// </code>
    /// </example>
    public sealed class GameData
    {
        // --- Grid ---
        public int GridWidth = 12;
        public int GridHeight = 7;

        // --- Build costs (credits) ---
        public int ServerCost = 50;
        public int FirewallCost = 40;
        public int IdsCost = 60;
        public int HoneypotCost = 45;

        // --- Links ---
        /// <summary>Credits per grid cell of Manhattan link length. A 3-cell link costs 15 at the default of 5.</summary>
        public int LinkCostPerCell = 5;

        /// <summary>Longest allowed link, in cells of Manhattan distance between its two endpoint nodes.</summary>
        public int MaxLinkLength = 6;

        /// <summary>
        /// Share of a link's purchase price returned when the player removes it, rounded down.
        /// Below 1.0 so that building and re-routing costs something rather than being free.
        /// </summary>
        public float LinkRefundFraction = 0.5f;

        // --- Upgrade costs, level 1 -> 2 (credits) ---
        public int FirewallUpgradeCost = 80;
        public int IdsUpgradeCost = 100;
        public int HoneypotUpgradeCost = 90;

        // --- Server income ---
        public float ServerIncomePerSecond = 5f;

        // --- Firewall: HP removed from a passing, visible packet ---
        public int FirewallFilterLevel1 = 20;
        public int FirewallFilterLevel2 = 40;

        // --- IDS: speed multiplier applied to packets on its own links (< 1 = slower) ---
        public float IdsSlowMultiplierLevel1 = 0.5f;
        public float IdsSlowMultiplierLevel2 = 0.3f;

        // --- Honeypot: capture capacity per level ---
        public int HoneypotCapacityLevel1 = 3;
        public int HoneypotCapacityLevel2 = 6;

        // --- Core ---
        public int CoreStartingIntegrity = 100;

        // --- Terminal commands (Story 004) ---
        // Effect durations and cooldowns for the player's night-time interventions. Read by
        // TerminalCommandProcessor at construction; nothing in the terminal hardcodes a number.
        // Cooldowns are measured on NetworkSimulation.SimulationTime, so they respect the debug
        // time scale and freeze between nights.

        /// <summary>Seconds a node stays cut off from the network after a successful <c>isolate</c>.</summary>
        public float IsolateDuration = 6f;

        /// <summary>Seconds before <c>isolate</c> may be used again, counted from the moment it applied.</summary>
        public float IsolateCooldown = 14f;

        /// <summary>Seconds every hidden packet stays revealed after a successful <c>scan</c>.</summary>
        public float ScanDuration = 5f;

        /// <summary>Seconds before <c>scan</c> may be used again.</summary>
        public float ScanCooldown = 18f;

        /// <summary>Seconds before <c>patch</c> may be used again.</summary>
        public float PatchCooldown = 10f;

        /// <summary>Cooldown of <c>help</c>. Zero by default: reading the command list is never rationed.</summary>
        public float HelpCooldown = 0f;

        /// <summary>
        /// Cooldown of <c>shutdown --all</c> (Story 005). Zero: the command exists only in the
        /// «сеть вне контроля» end state and is meant to work the first time it is typed.
        /// </summary>
        public float ShutdownCooldown = 0f;

        // --- Story 005 escalation ---

        /// <summary>
        /// How many packets an infected Server sends per spawn beat. One keeps the worm a slow
        /// bleed the player can out-run with <c>patch</c>; raising it makes an unpatched infection
        /// snowball.
        /// </summary>
        public int WormsPerInfectionSpawn = 1;

        /// <summary>
        /// Per-type packet profiles. Story 005's five attack types are entries here and nothing
        /// else: the simulation branches on the definition's behaviour flags, never on the enum.
        /// </summary>
        public Dictionary<PacketType, PacketDefinition> PacketDefinitions = new Dictionary<PacketType, PacketDefinition>
        {
            [PacketType.Standard] = new PacketDefinition
            {
                Type = PacketType.Standard,
                SpeedCellsPerSecond = 2f,
                MaxHp = 30,
                CoreDamage = 10,
                StartsHidden = false,
            },
            [PacketType.Stealth] = new PacketDefinition
            {
                Type = PacketType.Stealth,
                SpeedCellsPerSecond = 1.5f,
                MaxHp = 20,
                CoreDamage = 8,
                StartsHidden = true,
            },

            // «скан» — fast and weak. Twice a Standard packet's speed, so it is past an IDS-less
            // stretch before the player can type anything, but 10 HP dies to any level 1 Firewall.
            [PacketType.Scan] = new PacketDefinition
            {
                Type = PacketType.Scan,
                SpeedCellsPerSecond = 4f,
                MaxHp = 10,
                CoreDamage = 3,
                StartsHidden = false,
            },

            // «брутфорс» — tough. 90 HP survives two level 1 Firewalls (2 x 20) and even one
            // level 2 (40), so the answer is an upgraded pair, a Honeypot, or isolation.
            [PacketType.BruteForce] = new PacketDefinition
            {
                Type = PacketType.BruteForce,
                SpeedCellsPerSecond = 1.2f,
                MaxHp = 90,
                CoreDamage = 18,
                StartsHidden = false,
            },

            // «DDoS» — many weak packets aimed at one Server, which they take offline rather than
            // damaging the Core. The cost of ignoring them is the lost income, not integrity.
            [PacketType.Ddos] = new PacketDefinition
            {
                Type = PacketType.Ddos,
                SpeedCellsPerSecond = 3f,
                MaxHp = 8,
                CoreDamage = 3,
                StartsHidden = false,
                DisablesTargetServer = true,
                ServerDownSeconds = 20f,
            },

            // «червь» — infects the first healthy Server it reaches; that Server then sends worms
            // of its own to its neighbours every InfectionSpawnIntervalSeconds until `patch`.
            [PacketType.Worm] = new PacketDefinition
            {
                Type = PacketType.Worm,
                SpeedCellsPerSecond = 1.5f,
                MaxHp = 40,
                CoreDamage = 6,
                StartsHidden = false,
                InfectsServer = true,
                InfectionSpawnIntervalSeconds = 14f,
            },

            // «аномалия» — hidden like a Stealth packet and able to step between grid-adjacent
            // cells with no link at all, so a topology that looks airtight is not.
            [PacketType.Anomaly] = new PacketDefinition
            {
                Type = PacketType.Anomaly,
                SpeedCellsPerSecond = 1.8f,
                MaxHp = 25,
                CoreDamage = 12,
                StartsHidden = true,
                CanCrossMissingLinks = true,
            },
        };

        /// <summary>Purchase cost for a placeable node type. Throws for Gateway/Core (not purchasable).</summary>
        public int GetNodeCost(NodeType type)
        {
            switch (type)
            {
                case NodeType.Server: return ServerCost;
                case NodeType.Firewall: return FirewallCost;
                case NodeType.Ids: return IdsCost;
                case NodeType.Honeypot: return HoneypotCost;
                default:
                    throw new ArgumentException($"{type} is not a purchasable node type.", nameof(type));
            }
        }

        /// <summary>Upgrade cost (level 1 -> 2) for a security tool type. Throws for non-upgradable types.</summary>
        public int GetUpgradeCost(NodeType type)
        {
            switch (type)
            {
                case NodeType.Firewall: return FirewallUpgradeCost;
                case NodeType.Ids: return IdsUpgradeCost;
                case NodeType.Honeypot: return HoneypotUpgradeCost;
                default:
                    throw new ArgumentException($"{type} is not an upgradable node type.", nameof(type));
            }
        }

        /// <summary>Purchase cost of a link of the given Manhattan <paramref name="length"/> in cells.</summary>
        public int GetLinkCost(int length) => LinkCostPerCell * length;

        public int GetHoneypotCapacity(int level) => level >= 2 ? HoneypotCapacityLevel2 : HoneypotCapacityLevel1;

        public int GetFirewallFilter(int level) => level >= 2 ? FirewallFilterLevel2 : FirewallFilterLevel1;

        public float GetIdsSlowMultiplier(int level) => level >= 2 ? IdsSlowMultiplierLevel2 : IdsSlowMultiplierLevel1;

        public PacketDefinition GetPacketDefinition(PacketType type)
        {
            if (PacketDefinitions.TryGetValue(type, out PacketDefinition definition))
            {
                return definition;
            }
            throw new ArgumentException($"No PacketDefinition registered for {type}.", nameof(type));
        }
    }
}
