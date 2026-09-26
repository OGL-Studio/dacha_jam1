using System.Collections.Generic;
using NUnit.Framework;

namespace NightShift.Core.Tests
{
    /// <summary>
    /// Validation suite for Story 001 (Ядро симуляции сети). One or more
    /// <c>test_[scenario]_[expected_outcome]</c> methods per Acceptance Criteria bullet; see the
    /// "AC" comment above each region for the mapping. Every simulation under test is built with
    /// a fixed constant seed (<see cref="FixedSeed"/>) via the injected <see cref="IRandomSource"/>
    /// and advanced only via <see cref="NetworkSimulation.Tick"/> — no <c>System.Random</c> is
    /// created ad hoc anywhere in this file, and no assertion depends on wall-clock time.
    /// </summary>
    [TestFixture]
    public class SimulationCoreTests
    {
        private const int FixedSeed = 12345;

        private static GameData SmallGameData(int width, int height = 1)
        {
            var data = new GameData();
            data.GridWidth = width;
            data.GridHeight = height;
            return data;
        }

        private static NetworkSimulation CreateSimulation(GameData data = null, int seed = FixedSeed, float startingCredits = 1000f)
        {
            data ??= new GameData();
            return new NetworkSimulation(data, new SystemRandomSource(seed), startingCredits);
        }

        private static Node PlaceOrFail(NetworkSimulation sim, int x, int y, NodeType type)
        {
            bool ok = sim.TryPlaceNode(x, y, type, out Node node, out string error);
            Assert.IsTrue(ok, $"Expected placement of {type} at ({x},{y}) to succeed: {error}");
            return node;
        }

        private static void LinkOrFail(NetworkSimulation sim, int nodeAId, int nodeBId)
        {
            bool ok = sim.TryAddLink(nodeAId, nodeBId, out _, out string error);
            Assert.IsTrue(ok, $"Expected link {nodeAId}-{nodeBId} to succeed: {error}");
        }

        // ------------------------------------------------------------------
        // AC1 — 12x7 grid, exactly one fixed non-sellable Gateway (left edge) and
        // Core (right edge); node only into an empty cell; no duplicate links.
        // ------------------------------------------------------------------

        [Test]
        public void test_DefaultGrid_HasFixedGatewayAndCoreAtEdges()
        {
            NetworkSimulation sim = CreateSimulation();

            Assert.AreEqual(12, sim.Graph.Width);
            Assert.AreEqual(7, sim.Graph.Height);

            Assert.AreEqual(NodeType.Gateway, sim.Graph.GatewayNode.Type);
            Assert.AreEqual(0, sim.Graph.GatewayNode.X);
            Assert.AreEqual(3, sim.Graph.GatewayNode.Y);

            Assert.AreEqual(NodeType.Core, sim.Graph.CoreNode.Type);
            Assert.AreEqual(11, sim.Graph.CoreNode.X);
            Assert.AreEqual(3, sim.Graph.CoreNode.Y);

            Assert.AreNotEqual(sim.Graph.GatewayNode.Id, sim.Graph.CoreNode.Id);
        }

        [Test]
        public void test_TryPlaceNode_EmptyCell_Succeeds()
        {
            NetworkSimulation sim = CreateSimulation();

            bool ok = sim.TryPlaceNode(1, 3, NodeType.Server, out Node node, out string error);

            Assert.IsTrue(ok, error);
            Assert.IsNotNull(node);
            Assert.AreEqual(NodeType.Server, sim.Graph.GetNodeAt(1, 3).Type);
        }

        [Test]
        public void test_TryPlaceNode_OccupiedCell_Rejected()
        {
            NetworkSimulation sim = CreateSimulation();

            // The Gateway's own cell is occupied from the start.
            bool onGateway = sim.TryPlaceNode(0, 3, NodeType.Server, out _, out string gatewayError);
            Assert.IsFalse(onGateway);
            Assert.IsNotNull(gatewayError);

            // A cell occupied by a node the player just placed is likewise rejected.
            PlaceOrFail(sim, 1, 3, NodeType.Server);
            float creditsAfterFirstPlacement = sim.Credits;

            bool onOccupied = sim.TryPlaceNode(1, 3, NodeType.Firewall, out _, out string occupiedError);

            Assert.IsFalse(onOccupied);
            Assert.IsNotNull(occupiedError);
            Assert.AreEqual(creditsAfterFirstPlacement, sim.Credits, "A rejected placement must not spend credits.");
        }

        [Test]
        public void test_TryPlaceNode_OutOfBounds_Rejected()
        {
            NetworkSimulation sim = CreateSimulation();

            bool ok = sim.TryPlaceNode(12, 3, NodeType.Server, out _, out string error);

            Assert.IsFalse(ok);
            Assert.IsNotNull(error);
        }

        [Test]
        public void test_TryPlaceNode_GatewayOrCoreType_Rejected()
        {
            NetworkSimulation sim = CreateSimulation();

            Assert.IsFalse(sim.TryPlaceNode(5, 5, NodeType.Gateway, out _, out _));
            Assert.IsFalse(sim.TryPlaceNode(5, 5, NodeType.Core, out _, out _));
        }

        [Test]
        public void test_TryAddLink_DuplicateLink_Rejected()
        {
            NetworkSimulation sim = CreateSimulation();
            Node server = PlaceOrFail(sim, 1, 3, NodeType.Server);

            LinkOrFail(sim, sim.Graph.GatewayNode.Id, server.Id);

            bool sameOrder = sim.TryAddLink(sim.Graph.GatewayNode.Id, server.Id, out _, out string error1);
            bool reversedOrder = sim.TryAddLink(server.Id, sim.Graph.GatewayNode.Id, out _, out string error2);

            Assert.IsFalse(sameOrder);
            Assert.IsFalse(reversedOrder);
            Assert.IsNotNull(error1);
            Assert.IsNotNull(error2);
            Assert.AreEqual(1, sim.Graph.AllLinks.Count);
        }

        [Test]
        public void test_TryAddLink_LongerThanMaxLength_Rejected()
        {
            var data = new GameData();
            NetworkSimulation sim = CreateSimulation(data);
            // Gateway sits at (0,3); this cell is MaxLinkLength + 1 cells away along the row.
            Node farServer = PlaceOrFail(sim, data.MaxLinkLength + 1, 3, NodeType.Server);
            float creditsBefore = sim.Credits;

            bool ok = sim.TryAddLink(sim.Graph.GatewayNode.Id, farServer.Id, out _, out string error);

            Assert.IsFalse(ok);
            Assert.IsNotNull(error);
            Assert.AreEqual(creditsBefore, sim.Credits, "A rejected link must not spend credits.");
            Assert.AreEqual(0, sim.Graph.AllLinks.Count);
        }

        [Test]
        public void test_TryAddLink_ExactlyMaxLength_SucceedsWithThatLength()
        {
            var data = new GameData();
            NetworkSimulation sim = CreateSimulation(data);
            // Non-adjacent on purpose: links are not limited to neighbouring cells.
            Node server = PlaceOrFail(sim, data.MaxLinkLength, 3, NodeType.Server);

            bool ok = sim.TryAddLink(sim.Graph.GatewayNode.Id, server.Id, out Link link, out string error);

            Assert.IsTrue(ok, error);
            Assert.AreEqual(data.MaxLinkLength, link.Length);
        }

        // ------------------------------------------------------------------
        // AC2 — purchase of node / link / upgrade debits GameData price; insufficient
        // credits rejects the action atomically (balance unchanged, no partial spend).
        // ------------------------------------------------------------------

        [Test]
        public void test_TryPlaceNode_SufficientCredits_DeductsExactCost()
        {
            var data = new GameData();
            NetworkSimulation sim = CreateSimulation(data, startingCredits: 1000f);

            bool ok = sim.TryPlaceNode(1, 3, NodeType.Server, out _, out string error);

            Assert.IsTrue(ok, error);
            Assert.AreEqual(1000f - data.ServerCost, sim.Credits);
        }

        [Test]
        public void test_TryPlaceNode_InsufficientCredits_RejectedAndBalanceUnchanged()
        {
            var data = new GameData();
            NetworkSimulation sim = CreateSimulation(data, startingCredits: data.ServerCost - 1);

            bool ok = sim.TryPlaceNode(1, 3, NodeType.Server, out _, out string error);

            Assert.IsFalse(ok);
            Assert.IsNotNull(error);
            Assert.AreEqual(data.ServerCost - 1, sim.Credits);
            Assert.IsNull(sim.Graph.GetNodeAt(1, 3));
        }

        [Test]
        public void test_TryAddLink_InsufficientCredits_RejectedAndBalanceUnchanged()
        {
            var data = new GameData();
            // Exactly enough for the node, nothing left over for the link.
            NetworkSimulation sim = CreateSimulation(data, startingCredits: data.ServerCost);
            Node server = PlaceOrFail(sim, 1, 3, NodeType.Server);
            float creditsAfterPlacement = sim.Credits;

            bool ok = sim.TryAddLink(sim.Graph.GatewayNode.Id, server.Id, out _, out string error);

            Assert.IsFalse(ok);
            Assert.IsNotNull(error);
            Assert.AreEqual(creditsAfterPlacement, sim.Credits);
            Assert.AreEqual(0, sim.Graph.AllLinks.Count);
        }

        [Test]
        public void test_TryAddLink_LongerLink_CostsProportionallyMore()
        {
            var data = new GameData();
            NetworkSimulation sim = CreateSimulation(data, startingCredits: 1000f);
            // Gateway sits at (0,3).
            Node near = PlaceOrFail(sim, 1, 3, NodeType.Server); // 1 cell away
            Node far = PlaceOrFail(sim, 0, 6, NodeType.Server);  // 3 cells away

            float beforeShort = sim.Credits;
            LinkOrFail(sim, sim.Graph.GatewayNode.Id, near.Id);
            float shortCost = beforeShort - sim.Credits;

            float beforeLong = sim.Credits;
            LinkOrFail(sim, sim.Graph.GatewayNode.Id, far.Id);
            float longCost = beforeLong - sim.Credits;

            Assert.AreEqual(data.LinkCostPerCell * 1, shortCost);
            Assert.AreEqual(data.LinkCostPerCell * 3, longCost);
            Assert.AreEqual(3f * shortCost, longCost);
        }

        [Test]
        public void test_TryUpgrade_InsufficientCredits_RejectedAndBalanceUnchanged()
        {
            var data = new GameData();
            NetworkSimulation sim = CreateSimulation(data, startingCredits: data.FirewallCost);
            Node firewall = PlaceOrFail(sim, 1, 3, NodeType.Firewall);
            float creditsAfterPlacement = sim.Credits;

            bool ok = sim.TryUpgrade(firewall.Id, out string error);

            Assert.IsFalse(ok);
            Assert.IsNotNull(error);
            Assert.AreEqual(creditsAfterPlacement, sim.Credits);
            Assert.AreEqual(1, firewall.Level);
        }

        [Test]
        public void test_TryUpgrade_SufficientCredits_DeductsExactCostAndRaisesLevel()
        {
            var data = new GameData();
            NetworkSimulation sim = CreateSimulation(data, startingCredits: 1000f);
            Node firewall = PlaceOrFail(sim, 1, 3, NodeType.Firewall);
            float creditsBeforeUpgrade = sim.Credits;

            bool ok = sim.TryUpgrade(firewall.Id, out string error);

            Assert.IsTrue(ok, error);
            Assert.AreEqual(2, firewall.Level);
            Assert.AreEqual(creditsBeforeUpgrade - data.FirewallUpgradeCost, sim.Credits);
        }

        // ------------------------------------------------------------------
        // AC3 — Server income ticks only while online AND connected to both Gateway and Core.
        // ------------------------------------------------------------------

        [Test]
        public void test_Tick_ServerConnectedToGatewayAndCore_EarnsIncome()
        {
            GameData data = SmallGameData(width: 3);
            NetworkSimulation sim = CreateSimulation(data, startingCredits: 1000f);
            Node server = PlaceOrFail(sim, 1, 0, NodeType.Server);
            LinkOrFail(sim, sim.Graph.GatewayNode.Id, server.Id);
            LinkOrFail(sim, server.Id, sim.Graph.CoreNode.Id);
            sim.StartNight(new NightData { NightDuration = 100f });

            float creditsBeforeTick = sim.Credits;
            sim.Tick(2f);

            Assert.That(sim.Credits, Is.EqualTo(creditsBeforeTick + data.ServerIncomePerSecond * 2f).Within(0.0001f));
        }

        [Test]
        public void test_Tick_ServerMissingPathToCore_NoIncome()
        {
            GameData data = SmallGameData(width: 3);
            NetworkSimulation sim = CreateSimulation(data, startingCredits: 1000f);
            Node server = PlaceOrFail(sim, 1, 0, NodeType.Server);
            LinkOrFail(sim, sim.Graph.GatewayNode.Id, server.Id);
            // Deliberately no link from server to Core.
            sim.StartNight(new NightData { NightDuration = 100f });

            float creditsBeforeTick = sim.Credits;
            sim.Tick(2f);

            Assert.AreEqual(creditsBeforeTick, sim.Credits);
        }

        [Test]
        public void test_Tick_ServerOffline_NoIncome()
        {
            GameData data = SmallGameData(width: 3);
            NetworkSimulation sim = CreateSimulation(data, startingCredits: 1000f);
            Node server = PlaceOrFail(sim, 1, 0, NodeType.Server);
            LinkOrFail(sim, sim.Graph.GatewayNode.Id, server.Id);
            LinkOrFail(sim, server.Id, sim.Graph.CoreNode.Id);
            server.IsOnline = false;
            sim.StartNight(new NightData { NightDuration = 100f });

            float creditsBeforeTick = sim.Credits;
            sim.Tick(2f);

            Assert.AreEqual(creditsBeforeTick, sim.Credits);
        }

        // ------------------------------------------------------------------
        // AC4 — packet spawns at Gateway, moves at its type's speed, recomputes shortest
        // hop path at every node; dissipates with no damage if no path exists.
        // ------------------------------------------------------------------

        [Test]
        public void test_SpawnPacket_NoPathFromGateway_DissipatesWithoutDamage()
        {
            GameData data = SmallGameData(width: 3);
            NetworkSimulation sim = CreateSimulation(data, startingCredits: 1000f);
            int dissipatedCount = 0;
            sim.OnPacketDissipated += _ => dissipatedCount++;

            Packet packet = sim.SpawnPacket(PacketType.Standard);

            Assert.IsTrue(packet.IsDestroyed);
            Assert.AreEqual(1, dissipatedCount);
            Assert.AreEqual(data.CoreStartingIntegrity, sim.CoreIntegrity);
        }

        [Test]
        public void test_Tick_PacketMovesAlongLinkAtItsTypeSpeed()
        {
            GameData data = SmallGameData(width: 4);
            NetworkSimulation sim = CreateSimulation(data, startingCredits: 1000f);
            Node a = PlaceOrFail(sim, 1, 0, NodeType.Server);
            Node b = PlaceOrFail(sim, 2, 0, NodeType.Server);
            LinkOrFail(sim, sim.Graph.GatewayNode.Id, a.Id);
            LinkOrFail(sim, a.Id, b.Id);
            LinkOrFail(sim, b.Id, sim.Graph.CoreNode.Id);
            sim.StartNight(new NightData { NightDuration = 100f });

            Packet packet = sim.SpawnPacket(PacketType.Standard);
            PacketDefinition definition = data.GetPacketDefinition(PacketType.Standard);
            sim.Tick(0.25f);

            Assert.AreEqual(sim.Graph.GatewayNode.Id, packet.CurrentNodeId);
            Assert.AreEqual(a.Id, packet.TargetNodeId);
            Assert.That(packet.LinkProgress, Is.EqualTo(definition.SpeedCellsPerSecond * 0.25f).Within(0.0001f));
        }

        [Test]
        public void test_Tick_TravelTimeScalesWithLinkLength()
        {
            GameData data = SmallGameData(width: 5);
            NetworkSimulation sim = CreateSimulation(data, startingCredits: 1000f);
            // Gateway (0,0) and Core (4,0) joined by one 4-cell link.
            LinkOrFail(sim, sim.Graph.GatewayNode.Id, sim.Graph.CoreNode.Id);
            sim.StartNight(new NightData { NightDuration = 100f });

            PacketDefinition definition = data.GetPacketDefinition(PacketType.Standard);
            const float linkLength = 4f;
            float crossingTime = linkLength / definition.SpeedCellsPerSecond; // 2s at speed 2

            Packet packet = sim.SpawnPacket(PacketType.Standard);
            sim.Tick(crossingTime * 0.5f);

            Assert.IsFalse(packet.IsDestroyed, "Halfway across a 4-cell link the packet must still be in transit.");
            Assert.That(packet.LinkProgress, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.AreEqual(data.CoreStartingIntegrity, sim.CoreIntegrity);

            sim.Tick(crossingTime * 0.5f + 0.01f);

            Assert.IsTrue(packet.IsDestroyed);
            Assert.AreEqual(data.CoreStartingIntegrity - definition.CoreDamage, sim.CoreIntegrity);
        }

        [Test]
        public void test_PacketArrival_RecomputesPathAtEveryNodeAndReachesCore()
        {
            GameData data = SmallGameData(width: 4);
            NetworkSimulation sim = CreateSimulation(data, startingCredits: 1000f);
            Node a = PlaceOrFail(sim, 1, 0, NodeType.Server);
            Node b = PlaceOrFail(sim, 2, 0, NodeType.Server);
            LinkOrFail(sim, sim.Graph.GatewayNode.Id, a.Id);
            LinkOrFail(sim, a.Id, b.Id);
            LinkOrFail(sim, b.Id, sim.Graph.CoreNode.Id);
            sim.StartNight(new NightData { NightDuration = 100f });

            int leakedCount = 0;
            sim.OnPacketLeaked += _ => leakedCount++;
            sim.SpawnPacket(PacketType.Standard);
            sim.Tick(10f); // Comfortably more than the ~1.5s needed for 3 hops at speed 2.

            Assert.AreEqual(1, leakedCount);
            Assert.AreEqual(0, sim.ActivePackets.Count);
            Assert.AreEqual(data.CoreStartingIntegrity - data.GetPacketDefinition(PacketType.Standard).CoreDamage, sim.CoreIntegrity);
        }

        // ------------------------------------------------------------------
        // AC5 — Firewall removes filter HP from a passing, visible packet (level 1/2 from
        // GameData); destroyed at HP <= 0. Hidden packets are unaffected until revealed.
        // ------------------------------------------------------------------

        [Test]
        public void test_Firewall_VisiblePacket_DestroyedWhenFilterExceedsRemainingHp()
        {
            var data = SmallGameData(width: 3);
            data.FirewallFilterLevel1 = 40; // >= Standard's MaxHp(30): guarantees destruction in a single pass.
            NetworkSimulation sim = CreateSimulation(data, startingCredits: 1000f);
            Node firewall = PlaceOrFail(sim, 1, 0, NodeType.Firewall);
            LinkOrFail(sim, sim.Graph.GatewayNode.Id, firewall.Id);
            LinkOrFail(sim, firewall.Id, sim.Graph.CoreNode.Id);
            sim.StartNight(new NightData { NightDuration = 100f });

            PacketBlockReason? reason = null;
            sim.OnPacketBlocked += (_, r) => reason = r;
            Packet packet = sim.SpawnPacket(PacketType.Standard);
            sim.Tick(1f);

            Assert.IsTrue(packet.IsDestroyed);
            Assert.AreEqual(PacketBlockReason.Firewall, reason);
            Assert.AreEqual(data.CoreStartingIntegrity, sim.CoreIntegrity, "Packet must not have reached the Core.");
        }

        [Test]
        public void test_Firewall_HiddenPacket_UnaffectedUntilRevealed()
        {
            GameData data = SmallGameData(width: 3);
            NetworkSimulation sim = CreateSimulation(data, startingCredits: 1000f);
            Node firewall = PlaceOrFail(sim, 1, 0, NodeType.Firewall);
            LinkOrFail(sim, sim.Graph.GatewayNode.Id, firewall.Id);
            LinkOrFail(sim, firewall.Id, sim.Graph.CoreNode.Id);
            sim.StartNight(new NightData { NightDuration = 100f });

            Packet packet = sim.SpawnPacket(PacketType.Stealth); // Stealth starts hidden; no IDS present to reveal it.
            sim.Tick(5f); // Enough time to cross both hops (~1.33s at speed 1.5).

            PacketDefinition stealthDefinition = data.GetPacketDefinition(PacketType.Stealth);
            Assert.AreEqual(stealthDefinition.MaxHp, packet.CurrentHp, "Firewall must not reduce HP of a still-hidden packet.");
            Assert.IsTrue(packet.IsDestroyed, "Packet should have reached and damaged the Core instead.");
            Assert.AreEqual(data.CoreStartingIntegrity - stealthDefinition.CoreDamage, sim.CoreIntegrity);
        }

        [Test]
        public void test_Firewall_Level2_AppliesLevel2FilterValue()
        {
            GameData data = SmallGameData(width: 3);
            data.FirewallFilterLevel2 = 25; // Distinct from level 1 (20) and non-lethal to Standard (30 HP).
            NetworkSimulation sim = CreateSimulation(data, startingCredits: 1000f);
            Node firewall = PlaceOrFail(sim, 1, 0, NodeType.Firewall);
            LinkOrFail(sim, sim.Graph.GatewayNode.Id, firewall.Id);
            LinkOrFail(sim, firewall.Id, sim.Graph.CoreNode.Id);
            Assert.IsTrue(sim.TryUpgrade(firewall.Id, out string error), error);
            sim.StartNight(new NightData { NightDuration = 100f });

            Packet packet = sim.SpawnPacket(PacketType.Standard);
            sim.Tick(0.75f); // Past the Firewall (0.5s) but not yet at the Core (1.0s).

            Assert.IsFalse(packet.IsDestroyed);
            Assert.AreEqual(firewall.Id, packet.CurrentNodeId);
            Assert.AreEqual(packet.MaxHp - data.FirewallFilterLevel2, packet.CurrentHp);
        }

        // ------------------------------------------------------------------
        // AC6 — IDS reveals hidden packets at its own node and adjacent nodes; slows
        // packets on its own links by a GameData multiplier.
        // ------------------------------------------------------------------

        [Test]
        public void test_Ids_RevealsHiddenPacketOnAdjacentNode()
        {
            GameData data = SmallGameData(width: 3, height: 2);
            NetworkSimulation sim = CreateSimulation(data, startingCredits: 1000f);
            Node relay = PlaceOrFail(sim, 1, 1, NodeType.Server);
            Node ids = PlaceOrFail(sim, 1, 0, NodeType.Ids);
            LinkOrFail(sim, sim.Graph.GatewayNode.Id, relay.Id);
            LinkOrFail(sim, relay.Id, sim.Graph.CoreNode.Id);
            LinkOrFail(sim, relay.Id, ids.Id);
            sim.StartNight(new NightData { NightDuration = 100f });

            Packet packet = sim.SpawnPacket(PacketType.Stealth);
            Assert.IsFalse(sim.IsPacketVisible(packet), "Should still be hidden at the Gateway (not adjacent to the IDS).");

            sim.Tick(1f); // Enough to reach the relay node, which is adjacent to the IDS.

            Assert.IsTrue(packet.IndividuallyRevealed);
            Assert.IsTrue(sim.IsPacketVisible(packet));
        }

        [Test]
        public void test_Ids_RevealsHiddenPacketOnItsOwnNode()
        {
            GameData data = SmallGameData(width: 3);
            NetworkSimulation sim = CreateSimulation(data, startingCredits: 1000f);
            Node ids = PlaceOrFail(sim, 1, 0, NodeType.Ids);
            LinkOrFail(sim, sim.Graph.GatewayNode.Id, ids.Id);
            LinkOrFail(sim, ids.Id, sim.Graph.CoreNode.Id);
            sim.StartNight(new NightData { NightDuration = 100f });

            Packet packet = sim.SpawnPacket(PacketType.Stealth);
            sim.Tick(1f); // Enough to arrive directly at the IDS node itself.

            Assert.IsTrue(packet.IndividuallyRevealed, "IDS must reveal a hidden packet arriving at its own node, not just at neighbours.");
        }

        [Test]
        public void test_Ids_SlowsPacketOnItsOwnLink()
        {
            GameData data = SmallGameData(width: 3);
            NetworkSimulation sim = CreateSimulation(data, startingCredits: 1000f);
            Node ids = PlaceOrFail(sim, 1, 0, NodeType.Ids);
            LinkOrFail(sim, sim.Graph.GatewayNode.Id, ids.Id);
            LinkOrFail(sim, ids.Id, sim.Graph.CoreNode.Id);
            sim.StartNight(new NightData { NightDuration = 100f });

            Packet packet = sim.SpawnPacket(PacketType.Standard);
            float expectedEffectiveSpeed = data.GetPacketDefinition(PacketType.Standard).SpeedCellsPerSecond * data.IdsSlowMultiplierLevel1;
            sim.Tick(0.5f);

            Assert.AreEqual(sim.Graph.GatewayNode.Id, packet.CurrentNodeId, "Must not have arrived yet if correctly slowed.");
            Assert.That(packet.LinkProgress, Is.EqualTo(expectedEffectiveSpeed * 0.5f).Within(0.0001f));
        }

        [Test]
        public void test_Ids_Level2_AppliesLevel2SlowMultiplier()
        {
            GameData data = SmallGameData(width: 3);
            NetworkSimulation sim = CreateSimulation(data, startingCredits: 1000f);
            Node ids = PlaceOrFail(sim, 1, 0, NodeType.Ids);
            LinkOrFail(sim, sim.Graph.GatewayNode.Id, ids.Id);
            LinkOrFail(sim, ids.Id, sim.Graph.CoreNode.Id);
            Assert.IsTrue(sim.TryUpgrade(ids.Id, out string error), error);
            sim.StartNight(new NightData { NightDuration = 100f });

            Packet packet = sim.SpawnPacket(PacketType.Standard);
            float expectedEffectiveSpeed = data.GetPacketDefinition(PacketType.Standard).SpeedCellsPerSecond * data.IdsSlowMultiplierLevel2;
            sim.Tick(0.5f);

            Assert.That(packet.LinkProgress, Is.EqualTo(expectedEffectiveSpeed * 0.5f).Within(0.0001f));
        }

        // ------------------------------------------------------------------
        // AC7 — a packet arriving at a Honeypot's own node, or a node adjacent to it, is
        // diverted and destroyed while capacity remains; capacity decrements by 1.
        // ------------------------------------------------------------------

        [Test]
        public void test_Honeypot_CapturesPacketArrivingAtAdjacentNode()
        {
            GameData data = SmallGameData(width: 3, height: 2);
            NetworkSimulation sim = CreateSimulation(data, startingCredits: 1000f);
            Node relay = PlaceOrFail(sim, 1, 1, NodeType.Server);
            Node honeypot = PlaceOrFail(sim, 1, 0, NodeType.Honeypot);
            LinkOrFail(sim, sim.Graph.GatewayNode.Id, relay.Id);
            LinkOrFail(sim, relay.Id, sim.Graph.CoreNode.Id);
            LinkOrFail(sim, relay.Id, honeypot.Id);
            sim.StartNight(new NightData { NightDuration = 100f });

            PacketBlockReason? reason = null;
            sim.OnPacketBlocked += (_, r) => reason = r;
            Packet packet = sim.SpawnPacket(PacketType.Standard);
            sim.Tick(1f);

            Assert.IsTrue(packet.IsDestroyed);
            Assert.AreEqual(PacketBlockReason.Honeypot, reason);
            Assert.AreEqual(data.HoneypotCapacityLevel1 - 1, honeypot.HoneypotCapacityRemaining);
        }

        [Test]
        public void test_Honeypot_CapturesPacketArrivingAtItsOwnNode()
        {
            GameData data = SmallGameData(width: 3);
            NetworkSimulation sim = CreateSimulation(data, startingCredits: 1000f);
            Node honeypot = PlaceOrFail(sim, 1, 0, NodeType.Honeypot);
            LinkOrFail(sim, sim.Graph.GatewayNode.Id, honeypot.Id);
            LinkOrFail(sim, honeypot.Id, sim.Graph.CoreNode.Id);
            sim.StartNight(new NightData { NightDuration = 100f });

            Packet packet = sim.SpawnPacket(PacketType.Standard);
            sim.Tick(1f);

            Assert.IsTrue(packet.IsDestroyed, "Honeypot must capture a packet arriving at its own node, not just at neighbours.");
            Assert.AreEqual(data.HoneypotCapacityLevel1 - 1, honeypot.HoneypotCapacityRemaining);
        }

        [Test]
        public void test_Honeypot_Level2_RaisesCapacityToLevel2Value()
        {
            var data = new GameData();
            NetworkSimulation sim = CreateSimulation(data, startingCredits: 1000f);
            Node honeypot = PlaceOrFail(sim, 1, 3, NodeType.Honeypot);
            Assert.AreEqual(data.HoneypotCapacityLevel1, honeypot.HoneypotCapacityRemaining);

            bool ok = sim.TryUpgrade(honeypot.Id, out string error);

            Assert.IsTrue(ok, error);
            Assert.AreEqual(data.HoneypotCapacityLevel2, honeypot.HoneypotCapacityRemaining);
        }

        [Test]
        public void test_Honeypot_NoCapacityRemaining_PacketContinuesPastIt()
        {
            GameData data = SmallGameData(width: 3, height: 2);
            NetworkSimulation sim = CreateSimulation(data, startingCredits: 1000f);
            Node relay = PlaceOrFail(sim, 1, 1, NodeType.Server);
            Node honeypot = PlaceOrFail(sim, 1, 0, NodeType.Honeypot);
            LinkOrFail(sim, sim.Graph.GatewayNode.Id, relay.Id);
            LinkOrFail(sim, relay.Id, sim.Graph.CoreNode.Id);
            LinkOrFail(sim, relay.Id, honeypot.Id);
            sim.StartNight(new NightData { NightDuration = 1000f });

            // Exhaust the level-1 capacity.
            for (int i = 0; i < data.HoneypotCapacityLevel1; i++)
            {
                sim.SpawnPacket(PacketType.Standard);
                sim.Tick(1f);
            }
            Assert.AreEqual(0, honeypot.HoneypotCapacityRemaining);

            Packet overflowPacket = sim.SpawnPacket(PacketType.Standard);
            sim.Tick(5f); // Enough to fully cross the remaining hop to the Core.

            Assert.AreEqual(0, honeypot.HoneypotCapacityRemaining, "Capacity must not go negative.");
            Assert.AreEqual(
                data.CoreStartingIntegrity - data.GetPacketDefinition(PacketType.Standard).CoreDamage,
                sim.CoreIntegrity,
                "The packet that found no free capacity must have continued on to damage the Core.");
        }

        // ------------------------------------------------------------------
        // AC8 — a packet reaching the Core subtracts its damage from Core integrity
        // (starts at 100); the simulation reports defeat at 0.
        // ------------------------------------------------------------------

        [Test]
        public void test_PacketReachesCore_DealsDamageToIntegrity()
        {
            GameData data = SmallGameData(width: 2);
            NetworkSimulation sim = CreateSimulation(data, startingCredits: 1000f);
            LinkOrFail(sim, sim.Graph.GatewayNode.Id, sim.Graph.CoreNode.Id);
            sim.StartNight(new NightData { NightDuration = 100f });

            (int damage, int remaining)? damagedEvent = null;
            sim.OnCoreDamaged += (d, r) => damagedEvent = (d, r);
            sim.SpawnPacket(PacketType.Standard);
            sim.Tick(1f);

            int expectedDamage = data.GetPacketDefinition(PacketType.Standard).CoreDamage;
            Assert.AreEqual(data.CoreStartingIntegrity - expectedDamage, sim.CoreIntegrity);
            Assert.IsNotNull(damagedEvent);
            Assert.AreEqual(expectedDamage, damagedEvent.Value.damage);
            Assert.AreEqual(sim.CoreIntegrity, damagedEvent.Value.remaining);
        }

        [Test]
        public void test_CoreIntegrityReachesZero_FiresDefeatEvent()
        {
            GameData data = SmallGameData(width: 2);
            NetworkSimulation sim = CreateSimulation(data, startingCredits: 1000f);
            LinkOrFail(sim, sim.Graph.GatewayNode.Id, sim.Graph.CoreNode.Id);
            sim.StartNight(new NightData { NightDuration = 1000f });

            int defeatCount = 0;
            sim.OnCoreDestroyed += () => defeatCount++;

            int hitsNeeded = data.CoreStartingIntegrity / data.GetPacketDefinition(PacketType.Standard).CoreDamage;
            for (int i = 0; i < hitsNeeded; i++)
            {
                sim.SpawnPacket(PacketType.Standard);
                sim.Tick(1f);
            }

            Assert.AreEqual(0, sim.CoreIntegrity);
            Assert.AreEqual(1, defeatCount);
            Assert.IsFalse(sim.IsNightActive);
        }

        // ------------------------------------------------------------------
        // AC9 — a night runs for nightDuration and reports end-of-night with a summary
        // (earned, blocked, leaked, damage).
        // ------------------------------------------------------------------

        [Test]
        public void test_Night_RunsForConfiguredDurationThenFiresNightEndedWithSummary()
        {
            GameData data = SmallGameData(width: 3);
            NetworkSimulation sim = CreateSimulation(data, startingCredits: 1000f);
            Node server = PlaceOrFail(sim, 1, 0, NodeType.Server);
            LinkOrFail(sim, sim.Graph.GatewayNode.Id, server.Id);
            LinkOrFail(sim, server.Id, sim.Graph.CoreNode.Id);

            var nightData = new NightData
            {
                NightNumber = 7,
                NightDuration = 2f,
                SpawnSchedule = new List<PacketSpawnEvent>
                {
                    new PacketSpawnEvent { SpawnTime = 0f, Type = PacketType.Standard },
                    new PacketSpawnEvent { SpawnTime = 0.2f, Type = PacketType.Standard },
                },
            };

            NightReport report = null;
            sim.OnNightEnded += r => report = r;
            sim.StartNight(nightData);
            sim.Tick(2.5f); // Covers the whole 2s night (and both scheduled spawns) in one call.

            Assert.IsFalse(sim.IsNightActive);
            Assert.IsNotNull(report);
            Assert.AreEqual(7, report.NightNumber);
            Assert.That(report.CreditsEarned, Is.EqualTo(data.ServerIncomePerSecond * 2.5f).Within(0.0001f));
            Assert.AreEqual(2, report.PacketsLeaked);
            Assert.AreEqual(0, report.PacketsBlocked);
            Assert.AreEqual(0, report.PacketsDissipated);
            Assert.AreEqual(2 * data.GetPacketDefinition(PacketType.Standard).CoreDamage, report.CoreDamageTaken);
            Assert.AreEqual(sim.CoreIntegrity, report.RemainingCoreIntegrity);
            Assert.IsFalse(report.CoreDestroyed);
        }

        // ------------------------------------------------------------------
        // AC10 — determinism: identical seed + identical action sequence => identical results.
        // ------------------------------------------------------------------

        private struct ScenarioResult
        {
            public float Credits;
            public int CoreIntegrity;
            public NightReport Report;
            public int HoneypotCapacityRemaining;
        }

        private static ScenarioResult RunDeterminismScenario()
        {
            GameData data = SmallGameData(width: 5);
            NetworkSimulation sim = CreateSimulation(data, seed: 999, startingCredits: 1000f);

            Node firewall = PlaceOrFail(sim, 1, 0, NodeType.Firewall);
            Node ids = PlaceOrFail(sim, 2, 0, NodeType.Ids);
            Node honeypot = PlaceOrFail(sim, 3, 0, NodeType.Honeypot);
            LinkOrFail(sim, sim.Graph.GatewayNode.Id, firewall.Id);
            LinkOrFail(sim, firewall.Id, ids.Id);
            LinkOrFail(sim, ids.Id, honeypot.Id);
            LinkOrFail(sim, honeypot.Id, sim.Graph.CoreNode.Id);

            bool upgraded = sim.TryUpgrade(firewall.Id, out string upgradeError);
            Assert.IsTrue(upgraded, upgradeError);

            var nightData = new NightData
            {
                NightNumber = 1,
                NightDuration = 5f,
                SpawnSchedule = new List<PacketSpawnEvent>
                {
                    new PacketSpawnEvent { SpawnTime = 0f, Type = PacketType.Standard },
                    new PacketSpawnEvent { SpawnTime = 0.3f, Type = PacketType.Stealth },
                    new PacketSpawnEvent { SpawnTime = 1f, Type = PacketType.Standard },
                },
            };

            NightReport report = null;
            sim.OnNightEnded += r => report = r;
            sim.StartNight(nightData);
            sim.Tick(6f); // Covers the whole 5s night in one call.

            return new ScenarioResult
            {
                Credits = sim.Credits,
                CoreIntegrity = sim.CoreIntegrity,
                Report = report,
                HoneypotCapacityRemaining = honeypot.HoneypotCapacityRemaining,
            };
        }

        [Test]
        public void test_IdenticalSeedAndActions_ProduceIdenticalResults()
        {
            ScenarioResult first = RunDeterminismScenario();
            ScenarioResult second = RunDeterminismScenario();

            Assert.AreEqual(first.Credits, second.Credits);
            Assert.AreEqual(first.CoreIntegrity, second.CoreIntegrity);
            Assert.AreEqual(first.HoneypotCapacityRemaining, second.HoneypotCapacityRemaining);

            Assert.IsNotNull(first.Report);
            Assert.IsNotNull(second.Report);
            Assert.AreEqual(first.Report.CreditsEarned, second.Report.CreditsEarned);
            Assert.AreEqual(first.Report.PacketsBlocked, second.Report.PacketsBlocked);
            Assert.AreEqual(first.Report.PacketsLeaked, second.Report.PacketsLeaked);
            Assert.AreEqual(first.Report.PacketsDissipated, second.Report.PacketsDissipated);
            Assert.AreEqual(first.Report.CoreDamageTaken, second.Report.CoreDamageTaken);

            // Sanity: this particular gauntlet (upgraded Firewall + IDS + Honeypot in series)
            // is expected to block every packet before it ever reaches the Core.
            Assert.AreEqual(3, first.Report.PacketsBlocked);
            Assert.AreEqual(0, first.Report.PacketsLeaked);
        }

        // ------------------------------------------------------------------
        // Story 004 stub API — Isolate / Reveal / Patch flip the right internal state.
        // Full command parsing and cooldowns belong to Story 004, not tested here.
        // ------------------------------------------------------------------

        [Test]
        public void test_Isolate_CutsNodeLinksForDuration()
        {
            GameData data = SmallGameData(width: 3);
            NetworkSimulation sim = CreateSimulation(data, startingCredits: 1000f);
            Node relay = PlaceOrFail(sim, 1, 0, NodeType.Server);
            LinkOrFail(sim, sim.Graph.GatewayNode.Id, relay.Id);
            LinkOrFail(sim, relay.Id, sim.Graph.CoreNode.Id);

            Assert.IsTrue(sim.Graph.HasPath(sim.Graph.GatewayNode.Id, sim.Graph.CoreNode.Id, sim.SimulationTime));

            bool isolated = sim.Isolate(relay.Id, 5f);
            Assert.IsTrue(isolated);
            Assert.IsFalse(sim.Graph.HasPath(sim.Graph.GatewayNode.Id, sim.Graph.CoreNode.Id, sim.SimulationTime));

            sim.Tick(6f); // Advances the simulation clock past the isolation window even with no night running.

            Assert.IsTrue(sim.Graph.HasPath(sim.Graph.GatewayNode.Id, sim.Graph.CoreNode.Id, sim.SimulationTime));
        }

        [Test]
        public void test_Isolate_GatewayOrCore_Rejected()
        {
            NetworkSimulation sim = CreateSimulation();

            Assert.IsFalse(sim.Isolate(sim.Graph.GatewayNode.Id, 5f));
            Assert.IsFalse(sim.Isolate(sim.Graph.CoreNode.Id, 5f));
        }

        [Test]
        public void test_Reveal_TemporarilyRevealsHiddenPackets()
        {
            GameData data = SmallGameData(width: 3);
            NetworkSimulation sim = CreateSimulation(data, startingCredits: 1000f);
            Node relay = PlaceOrFail(sim, 1, 0, NodeType.Server);
            LinkOrFail(sim, sim.Graph.GatewayNode.Id, relay.Id);
            LinkOrFail(sim, relay.Id, sim.Graph.CoreNode.Id);

            Packet packet = sim.SpawnPacket(PacketType.Stealth);
            Assert.IsFalse(sim.IsPacketVisible(packet));

            sim.Reveal(3f);
            Assert.IsTrue(sim.IsPacketVisible(packet));

            sim.Tick(4f); // Advances the clock past the reveal window; no night running, so the packet itself does not move.

            Assert.IsFalse(sim.IsPacketVisible(packet));
        }

        [Test]
        public void test_Patch_BringsNodeBackOnlineAndClearsIsolation()
        {
            GameData data = SmallGameData(width: 3);
            NetworkSimulation sim = CreateSimulation(data, startingCredits: 1000f);
            Node server = PlaceOrFail(sim, 1, 0, NodeType.Server);
            server.IsOnline = false;
            sim.Isolate(server.Id, 10f);
            Assert.IsTrue(server.IsIsolatedAt(sim.SimulationTime));

            bool patched = sim.Patch(server.Id);

            Assert.IsTrue(patched);
            Assert.IsTrue(server.IsOnline);
            Assert.IsFalse(server.IsIsolatedAt(sim.SimulationTime));
            Assert.IsFalse(sim.Patch(999999));
        }
    }
}
