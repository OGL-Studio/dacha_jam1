using System;
using System.Collections.Generic;
using NightShift.Core.Data;
using NUnit.Framework;

namespace NightShift.Core.Tests
{
    /// <summary>
    /// Validation suite for Story 005 (Пять ночей и нарастание «Out of Control»), one or more
    /// <c>test_[scenario]_[expected_outcome]</c> methods per Acceptance Criterion; see the "AC"
    /// comment above each region for the mapping.
    /// </summary>
    /// <remarks>
    /// Every simulation here is built with a fixed constant seed through the injected
    /// <see cref="IRandomSource"/> and advanced only through <see cref="NetworkSimulation.Tick"/>, so
    /// nothing depends on wall-clock time or on ambient randomness. The campaign's own seed
    /// (<see cref="NightLibrary.SimulationSeed"/>) is used for the winnability test, because that is
    /// the seed the shipped game runs on.
    /// </remarks>
    [TestFixture]
    public class FiveNightsEscalationTests
    {
        private const int FixedSeed = 12345;

        /// <summary>Fixed step used to advance a night. Chosen once here rather than per test so no test hides a step-size dependency.</summary>
        private const float TickSeconds = 1f / 60f;

        // ------------------------------------------------------------------
        // Fixtures
        // ------------------------------------------------------------------

        /// <summary>The shipped campaign: authored tuning, the fixed starting network, the campaign seed.</summary>
        private static NetworkSimulation CreateCampaignSimulation()
        {
            var simulation = new NetworkSimulation(
                NightLibrary.CreateGameData(),
                new SystemRandomSource(NightLibrary.SimulationSeed),
                NightLibrary.StartingCredits);

            Assert.IsTrue(StartingNetwork.TryBuild(simulation, out string error), error);
            return simulation;
        }

        /// <summary>A bare simulation on a custom grid, for the single-behaviour attack-type tests.</summary>
        private static NetworkSimulation CreateBareSimulation(int width, int height = 1, int seed = FixedSeed)
        {
            GameData data = NightLibrary.CreateGameData();
            data.GridWidth = width;
            data.GridHeight = height;
            return new NetworkSimulation(data, new SystemRandomSource(seed), 1000f);
        }

        private static Node PlaceOrFail(NetworkSimulation simulation, int x, int y, NodeType type)
        {
            Assert.IsTrue(simulation.TryPlaceNode(x, y, type, out Node node, out string error), error);
            return node;
        }

        private static void LinkOrFail(NetworkSimulation simulation, int nodeAId, int nodeBId)
        {
            Assert.IsTrue(simulation.TryAddLink(nodeAId, nodeBId, out _, out string error), error);
        }

        /// <summary>A night with no waves at all, used as a clock for the behaviour tests.</summary>
        private static NightData EmptyNight(float duration) => new NightData { NightNumber = 1, NightDuration = duration };

        /// <summary>Runs a whole night at a fixed step and returns its report. Fails the test if the night never ends.</summary>
        private static NightReport RunNight(NetworkSimulation simulation, NightData night)
        {
            NightReport captured = null;
            Action<NightReport> handler = report => captured = report;

            simulation.OnNightEnded += handler;
            simulation.StartNight(night);

            int tickBudget = (int)(night.NightDuration / TickSeconds) + 600;
            while (simulation.IsNightActive && tickBudget-- > 0)
            {
                simulation.Tick(TickSeconds);
            }

            simulation.OnNightEnded -= handler;

            Assert.IsNotNull(captured, "Night " + night.NightNumber + " never produced a report.");
            return captured;
        }

        /// <summary>Advances an already-started night by <paramref name="seconds"/> at the fixed step.</summary>
        private static void Advance(NetworkSimulation simulation, float seconds)
        {
            int ticks = (int)(seconds / TickSeconds);
            for (int i = 0; i < ticks; i++)
            {
                simulation.Tick(TickSeconds);
            }
        }

        // ------------------------------------------------------------------
        // AC1 — five nights described as data: 300-420s, waves of
        // (type, count, interval, target); no balance number in night logic.
        // ------------------------------------------------------------------

        [Test]
        public void test_AuthoredNights_AreFiveAndLastBetween300And420Seconds()
        {
            Assert.AreEqual(5, NightLibrary.NightCount);

            for (int nightNumber = 1; nightNumber <= NightLibrary.NightCount; nightNumber++)
            {
                NightData night = NightLibrary.CreateNight(nightNumber);

                Assert.AreEqual(nightNumber, night.NightNumber);
                Assert.GreaterOrEqual(night.NightDuration, 300f, "Night " + nightNumber + " is too short.");
                Assert.LessOrEqual(night.NightDuration, 420f, "Night " + nightNumber + " is too long.");
            }
        }

        [Test]
        public void test_AuthoredNights_DescribeEveryWaveWithTypeCountIntervalAndTarget()
        {
            for (int nightNumber = 1; nightNumber <= NightLibrary.NightCount; nightNumber++)
            {
                NightData night = NightLibrary.CreateNight(nightNumber);

                Assert.IsNotEmpty(night.Waves, "Night " + nightNumber + " has no waves.");

                foreach (NightWave wave in night.Waves)
                {
                    Assert.Greater(wave.Count, 0, "Night " + nightNumber + " has an empty wave.");
                    Assert.Greater(wave.IntervalSeconds, 0f, "Night " + nightNumber + " has a zero-interval wave.");
                    Assert.GreaterOrEqual(wave.StartTime, 0f);
                    Assert.LessOrEqual(wave.StartTime, night.NightDuration);
                }
            }
        }

        [Test]
        public void test_AuthoredNights_EscalateInTotalPacketCount()
        {
            int previous = 0;
            for (int nightNumber = 1; nightNumber <= NightLibrary.NightCount; nightNumber++)
            {
                int total = 0;
                foreach (NightWave wave in NightLibrary.CreateNight(nightNumber).Waves)
                {
                    total += wave.Count;
                }

                Assert.Greater(total, previous, "Night " + nightNumber + " is not denser than the night before.");
                previous = total;
            }
        }

        [Test]
        public void test_StartNight_ExpandsWavesIntoTimedSpawns()
        {
            NetworkSimulation simulation = CreateBareSimulation(4);
            Node relay = PlaceOrFail(simulation, 2, 0, NodeType.Server);
            LinkOrFail(simulation, simulation.Graph.GatewayNode.Id, relay.Id);
            LinkOrFail(simulation, relay.Id, simulation.Graph.CoreNode.Id);

            var night = EmptyNight(120f);
            night.Waves.Add(new NightWave
            {
                Type = PacketType.Standard,
                Count = 3,
                StartTime = 10f,
                IntervalSeconds = 10f,
            });

            var spawned = new List<int>();
            simulation.OnPacketLeaked += packet => spawned.Add(packet.Id);

            simulation.StartNight(night);

            Advance(simulation, 5f);
            Assert.IsEmpty(simulation.ActivePackets, "Nothing should spawn before the wave's StartTime.");

            Advance(simulation, 100f);
            Assert.AreEqual(3, spawned.Count, "All three wave packets should have resolved by now.");
        }

        [Test]
        public void test_SameSeedAndSameNight_ProduceIdenticalReports()
        {
            NightReport first = RunNight(CreateCampaignSimulation(), NightLibrary.CreateNight(2));
            NightReport second = RunNight(CreateCampaignSimulation(), NightLibrary.CreateNight(2));

            Assert.AreEqual(first.PacketsBlocked, second.PacketsBlocked);
            Assert.AreEqual(first.PacketsLeaked, second.PacketsLeaked);
            Assert.AreEqual(first.PacketsDissipated, second.PacketsDissipated);
            Assert.AreEqual(first.CoreDamageTaken, second.CoreDamageTaken);
            Assert.AreEqual(first.RemainingCoreIntegrity, second.RemainingCoreIntegrity);
        }

        // ------------------------------------------------------------------
        // AC2 — the five attack types.
        // ------------------------------------------------------------------

        [Test]
        public void test_AttackProfiles_ScanIsFastAndWeak_BruteForceIsTough()
        {
            GameData data = NightLibrary.CreateGameData();

            PacketDefinition standard = data.GetPacketDefinition(PacketType.Standard);
            PacketDefinition scan = data.GetPacketDefinition(PacketType.Scan);
            PacketDefinition bruteForce = data.GetPacketDefinition(PacketType.BruteForce);

            Assert.Greater(scan.SpeedCellsPerSecond, standard.SpeedCellsPerSecond, "«скан» must be the fast one.");
            Assert.Less(scan.MaxHp, standard.MaxHp, "«скан» must be the weak one.");

            Assert.Greater(bruteForce.MaxHp, standard.MaxHp, "«брутфорс» must be the tough one.");
            Assert.Greater(
                bruteForce.MaxHp,
                2 * data.GetFirewallFilter(1),
                "«брутфорс» must survive a pair of level 1 Firewalls, otherwise it is not a threat.");
        }

        [Test]
        public void test_AnomalyProfile_IsHiddenAndCrossesMissingLinks()
        {
            PacketDefinition anomaly = NightLibrary.CreateGameData().GetPacketDefinition(PacketType.Anomaly);

            Assert.IsTrue(anomaly.StartsHidden, "«аномалия» must start hidden.");
            Assert.IsTrue(anomaly.CanCrossMissingLinks, "«аномалия» must be able to cross a missing link.");
        }

        [Test]
        public void test_DdosPacket_TakesTargetServerOfflineWithoutDamagingCore()
        {
            NetworkSimulation simulation = CreateBareSimulation(5);
            Node server = PlaceOrFail(simulation, 2, 0, NodeType.Server);
            LinkOrFail(simulation, simulation.Graph.GatewayNode.Id, server.Id);
            LinkOrFail(simulation, server.Id, simulation.Graph.CoreNode.Id);

            Node downed = null;
            simulation.OnServerDowned += (node, seconds) => downed = node;

            simulation.StartNight(EmptyNight(300f));
            simulation.SpawnPacket(PacketType.Ddos, WaveTargetKind.Server);

            Advance(simulation, 5f);

            Assert.AreSame(server, downed, "The DDoS packet should have downed the Server it was aimed at.");
            Assert.IsFalse(server.IsOnline);
            Assert.IsTrue(server.IsDownedAt(simulation.SimulationTime));
            Assert.AreEqual(100, simulation.CoreIntegrity, "A DDoS hit on a Server must not damage the Core.");
        }

        [Test]
        public void test_DownedServer_ComesBackOnlineAfterItsDowntime()
        {
            NetworkSimulation simulation = CreateBareSimulation(5);
            Node server = PlaceOrFail(simulation, 2, 0, NodeType.Server);
            LinkOrFail(simulation, simulation.Graph.GatewayNode.Id, server.Id);
            LinkOrFail(simulation, server.Id, simulation.Graph.CoreNode.Id);

            float downSeconds = simulation.Data.GetPacketDefinition(PacketType.Ddos).ServerDownSeconds;

            Node recovered = null;
            simulation.OnServerRecovered += node => recovered = node;

            simulation.StartNight(EmptyNight(300f));
            simulation.SpawnPacket(PacketType.Ddos, WaveTargetKind.Server);
            Advance(simulation, 5f);
            Assert.IsFalse(server.IsOnline);

            Advance(simulation, downSeconds + 1f);

            Assert.AreSame(server, recovered);
            Assert.IsTrue(server.IsOnline);
        }

        [Test]
        public void test_WormPacket_InfectsServerWhichThenKeepsSpawningUntilPatched()
        {
            // One lone Server, so every worm it emits travels on to the Core and dies there instead
            // of re-infecting anything: what this test measures is the spawning, not the spread.
            NetworkSimulation simulation = CreateBareSimulation(8, 7);
            Node server = PlaceOrFail(simulation, 3, 3, NodeType.Server);
            LinkOrFail(simulation, simulation.Graph.GatewayNode.Id, server.Id);
            LinkOrFail(simulation, server.Id, simulation.Graph.CoreNode.Id);

            int resolvedPackets = 0;
            simulation.OnPacketLeaked += _ => resolvedPackets++;
            simulation.OnPacketBlocked += (_, __) => resolvedPackets++;
            simulation.OnPacketDissipated += _ => resolvedPackets++;

            float interval = simulation.Data.GetPacketDefinition(PacketType.Worm).InfectionSpawnIntervalSeconds;

            simulation.StartNight(EmptyNight(900f));
            simulation.SpawnPacket(PacketType.Worm);

            Advance(simulation, 10f);
            Assert.IsTrue(server.IsInfected, "The worm should have infected the first Server it reached.");
            Assert.AreEqual(PacketType.Worm, server.InfectionPacketType);

            int afterInfection = resolvedPackets;
            Advance(simulation, interval * 3f);
            Assert.Greater(resolvedPackets, afterInfection, "An infected Server must keep sending worms of its own.");

            // `patch` is the cure. Worms already in flight still have to land, so settle first, then
            // measure: nothing new may appear after that.
            Assert.IsTrue(simulation.Patch(server.Id));
            Assert.IsFalse(server.IsInfected);

            Advance(simulation, interval * 2f);
            int afterPatchSettled = resolvedPackets;

            Advance(simulation, interval * 3f);

            Assert.AreEqual(afterPatchSettled, resolvedPackets, "A patched Server must stop producing worms.");
            Assert.IsEmpty(simulation.ActivePackets, "Nothing should be left in flight once the infection is cured.");
        }

        [Test]
        public void test_InfectedServer_SpreadsTheWormToANeighbouringServer()
        {
            NetworkSimulation simulation = CreateBareSimulation(12, 7);
            Node firstServer = PlaceOrFail(simulation, 3, 3, NodeType.Server);
            Node secondServer = PlaceOrFail(simulation, 7, 3, NodeType.Server);
            LinkOrFail(simulation, simulation.Graph.GatewayNode.Id, firstServer.Id);
            LinkOrFail(simulation, firstServer.Id, secondServer.Id);
            LinkOrFail(simulation, secondServer.Id, simulation.Graph.CoreNode.Id);

            var infected = new List<Node>();
            simulation.OnServerInfected += node => infected.Add(node);

            float interval = simulation.Data.GetPacketDefinition(PacketType.Worm).InfectionSpawnIntervalSeconds;

            simulation.StartNight(EmptyNight(600f));
            simulation.SpawnPacket(PacketType.Worm);

            Advance(simulation, 10f);
            Assert.IsTrue(firstServer.IsInfected);
            Assert.IsFalse(secondServer.IsInfected, "Only the Server the worm reached should be infected at first.");

            // The infected Server is a spawn source aimed at its own neighbours, which is how the
            // infection crosses the network with nothing arriving from the Gateway.
            Advance(simulation, interval * 3f);

            Assert.IsTrue(secondServer.IsInfected, "The infection should have spread to the neighbouring Server.");
            Assert.AreEqual(2, infected.Count);
        }

        [Test]
        public void test_AnomalyPacket_CrossesGridAdjacentCellsWhereNoLinkExists()
        {
            // gw(0,0) - a(1,0) - b(2,0) ... core(3,0): b and core are grid-adjacent but NOT linked.
            NetworkSimulation simulation = CreateBareSimulation(4);
            Node a = PlaceOrFail(simulation, 1, 0, NodeType.Server);
            Node b = PlaceOrFail(simulation, 2, 0, NodeType.Server);
            LinkOrFail(simulation, simulation.Graph.GatewayNode.Id, a.Id);
            LinkOrFail(simulation, a.Id, b.Id);
            Assert.IsFalse(simulation.Graph.TryGetLink(b.Id, simulation.Graph.CoreNode.Id, out _));

            var dissipated = new List<Packet>();
            var leaked = new List<Packet>();
            simulation.OnPacketDissipated += packet => dissipated.Add(packet);
            simulation.OnPacketLeaked += packet => leaked.Add(packet);

            simulation.StartNight(EmptyNight(300f));

            // An ordinary packet has no route to the Core and dies harmlessly.
            simulation.SpawnPacket(PacketType.Standard);
            Advance(simulation, 20f);
            Assert.AreEqual(1, dissipated.Count, "A Standard packet must not be able to cross the gap.");
            Assert.AreEqual(100, simulation.CoreIntegrity);

            // The anomaly steps across the missing link and reaches the Core.
            simulation.SpawnPacket(PacketType.Anomaly);
            Advance(simulation, 30f);

            Assert.AreEqual(1, leaked.Count, "«аномалия» should have reached the Core across the missing link.");
            Assert.Less(simulation.CoreIntegrity, 100);
        }

        [Test]
        public void test_IsolatedNode_StillStopsAnAnomaly()
        {
            NetworkSimulation simulation = CreateBareSimulation(4);
            Node a = PlaceOrFail(simulation, 1, 0, NodeType.Server);
            Node b = PlaceOrFail(simulation, 2, 0, NodeType.Server);
            LinkOrFail(simulation, simulation.Graph.GatewayNode.Id, a.Id);
            LinkOrFail(simulation, a.Id, b.Id);

            simulation.StartNight(EmptyNight(300f));
            Assert.IsTrue(simulation.Isolate(b.Id, 120f));

            var dissipated = new List<Packet>();
            simulation.OnPacketDissipated += packet => dissipated.Add(packet);

            simulation.SpawnPacket(PacketType.Anomaly);
            Advance(simulation, 30f);

            Assert.AreEqual(1, dissipated.Count, "Isolation must remain an answer to an anomaly.");
            Assert.AreEqual(100, simulation.CoreIntegrity);
        }

        // ------------------------------------------------------------------
        // AC3 — alien nodes appear on nights 3-4, linked in, unsellable,
        // isolatable.
        // ------------------------------------------------------------------

        [Test]
        public void test_AlienNodes_AreAuthoredOnNights3And4Only()
        {
            Assert.IsEmpty(NightLibrary.CreateNight(1).AlienNodes);
            Assert.IsEmpty(NightLibrary.CreateNight(2).AlienNodes);
            Assert.IsNotEmpty(NightLibrary.CreateNight(3).AlienNodes);
            Assert.IsNotEmpty(NightLibrary.CreateNight(4).AlienNodes);
        }

        [Test]
        public void test_AlienNodeSpawn_AppearsLinkedAndCannotBeSoldButCanBeIsolated()
        {
            NetworkSimulation simulation = CreateCampaignSimulation();
            int nodesBefore = simulation.Graph.AllNodes.Count;

            var night = EmptyNight(300f);
            night.AlienNodes.Add(new AlienNodeSpawn { SpawnTime = 1f, X = 5, Y = 5, Type = NodeType.Server, LinkCount = 2 });

            Node appeared = null;
            simulation.OnAlienNodeAppeared += node => appeared = node;

            simulation.StartNight(night);
            Advance(simulation, 3f);

            Assert.IsNotNull(appeared, "The night's alien node should have appeared on its own.");
            Assert.AreEqual(nodesBefore + 1, simulation.Graph.AllNodes.Count);
            Assert.IsTrue(appeared.IsAlien);
            Assert.IsFalse(appeared.CanBeRemovedByPlayer, "«их нельзя продать».");
            Assert.IsNotEmpty(
                simulation.Graph.GetActiveNeighbors(appeared.Id, simulation.SimulationTime),
                "An alien node must be linked into the existing network.");
            Assert.IsTrue(simulation.Isolate(appeared.Id, 5f), "«только изолировать».");
            Assert.IsTrue(NodeNaming.GetName(simulation.Graph, appeared)
                .StartsWith(NodeNaming.AlienPrefix, StringComparison.Ordinal));
        }

        [Test]
        public void test_AlienServer_EarnsThePlayerNothing()
        {
            NetworkSimulation simulation = CreateBareSimulation(6);
            Node alien = simulation.SpawnAlienNode(new AlienNodeSpawn { SpawnTime = 0f, X = 2, Y = 0, Type = NodeType.Server, LinkCount = 2 });
            Assert.IsNotNull(alien);

            float creditsBefore = simulation.Credits;
            simulation.StartNight(EmptyNight(300f));
            Advance(simulation, 30f);

            Assert.AreEqual(creditsBefore, simulation.Credits, "An alien Server must not pay the player.");
        }

        // ------------------------------------------------------------------
        // AC4 — on nights 4-5 a command may hit the wrong node, and the log
        // says so.
        // ------------------------------------------------------------------

        [Test]
        public void test_MisfireChance_IsAuthoredOnNights4And5Only()
        {
            Assert.AreEqual(0f, NightLibrary.CreateNight(1).CommandMisfireChance);
            Assert.AreEqual(0f, NightLibrary.CreateNight(2).CommandMisfireChance);
            Assert.AreEqual(0f, NightLibrary.CreateNight(3).CommandMisfireChance);
            Assert.Greater(NightLibrary.CreateNight(4).CommandMisfireChance, 0f);
            Assert.Greater(NightLibrary.CreateNight(5).CommandMisfireChance, 0f);
        }

        [Test]
        public void test_CommandWithoutMisfireChance_AlwaysHitsTheNamedNode()
        {
            NetworkSimulation simulation = CreateCampaignSimulation();
            var terminal = new TerminalCommandProcessor(simulation);

            var night = EmptyNight(300f); // CommandMisfireChance defaults to 0.
            simulation.StartNight(night);

            for (int i = 0; i < 20; i++)
            {
                TerminalCommandResult result = terminal.Execute("patch srv-1");
                Assert.IsTrue(result.Success, result.Diagnostic);
                Assert.IsFalse(result.Misfired);
                Assert.AreEqual("srv-1", result.NodeName);
                Advance(simulation, simulation.Data.PatchCooldown + 1f);
            }
        }

        [Test]
        public void test_CertainMisfire_AppliesToAnotherNodeAndReportsBothNames()
        {
            NetworkSimulation simulation = CreateCampaignSimulation();
            var terminal = new TerminalCommandProcessor(simulation);

            var night = EmptyNight(300f);
            night.CommandMisfireChance = 1f;
            simulation.StartNight(night);

            TerminalCommandResult result = terminal.Execute("isolate srv-1");

            Assert.IsTrue(result.Success, result.Diagnostic);
            Assert.IsTrue(result.Misfired, "A chance of 1 must always misfire.");
            Assert.AreEqual("srv-1", result.IntendedNodeName);
            Assert.AreNotEqual(result.IntendedNodeName, result.NodeName);
            Assert.IsNotEmpty(result.NodeName);

            // The wrong node is the one that actually got isolated.
            Assert.IsTrue(NodeNaming.TryResolve(simulation.Graph, result.NodeName, out Node hit));
            Assert.IsTrue(hit.IsIsolatedAt(simulation.SimulationTime));

            Assert.IsTrue(NodeNaming.TryResolve(simulation.Graph, "srv-1", out Node intended));
            Assert.IsFalse(intended.IsIsolatedAt(simulation.SimulationTime));
        }

        [Test]
        public void test_MisfireTargets_AreIdenticalForTheSameSeedAndCommands()
        {
            string[] firstRun = RunMisfireSequence();
            string[] secondRun = RunMisfireSequence();

            Assert.AreEqual(firstRun, secondRun, "Misfires must be reproducible for a given seed.");
        }

        /// <summary>Types the same three commands under a certain-misfire night and returns which nodes were hit.</summary>
        private static string[] RunMisfireSequence()
        {
            NetworkSimulation simulation = CreateCampaignSimulation();
            var terminal = new TerminalCommandProcessor(simulation);

            var night = EmptyNight(300f);
            night.CommandMisfireChance = 1f;
            simulation.StartNight(night);

            var hits = new List<string>();
            for (int i = 0; i < 3; i++)
            {
                hits.Add(terminal.Execute("patch fw-1").NodeName);
                Advance(simulation, simulation.Data.PatchCooldown + 1f);
            }

            return hits.ToArray();
        }

        [Test]
        public void test_MisfiringIsolate_NeverPicksATargetThatCannotBeIsolated()
        {
            NetworkSimulation simulation = CreateCampaignSimulation();
            var terminal = new TerminalCommandProcessor(simulation);

            var night = EmptyNight(600f);
            night.CommandMisfireChance = 1f;
            simulation.StartNight(night);

            for (int i = 0; i < 12; i++)
            {
                TerminalCommandResult result = terminal.Execute("isolate srv-1");
                Assert.IsTrue(result.Success, result.Diagnostic);
                Assert.AreNotEqual(NodeNaming.GatewayName, result.NodeName);
                Assert.AreNotEqual(NodeNaming.CoreName, result.NodeName);
                Advance(simulation, simulation.Data.IsolateCooldown + 1f);
            }
        }

        // ------------------------------------------------------------------
        // AC5 — night 5 ends in «сеть вне контроля», where only
        // `shutdown --all` is left and typing it is the victory ending.
        // ------------------------------------------------------------------

        [Test]
        public void test_OnlyTheFinalNight_IsAuthoredToEndOutOfControl()
        {
            for (int nightNumber = 1; nightNumber < NightLibrary.NightCount; nightNumber++)
            {
                Assert.IsFalse(NightLibrary.CreateNight(nightNumber).EndsOutOfControl, "Night " + nightNumber);
            }

            Assert.IsTrue(NightLibrary.CreateNight(NightLibrary.NightCount).EndsOutOfControl);
        }

        [Test]
        public void test_ShutdownAll_IsHiddenAndRefusedBeforeTheNetworkIsOutOfControl()
        {
            NetworkSimulation simulation = CreateCampaignSimulation();
            var terminal = new TerminalCommandProcessor(simulation);
            simulation.StartNight(EmptyNight(300f));

            TerminalCommandResult result = terminal.Execute("shutdown --all");

            Assert.IsFalse(result.Success);
            Assert.AreEqual(TerminalResultCode.ShutdownUnavailable, result.Code);
            Assert.IsFalse(simulation.IsVictory);

            foreach (TerminalCommandInfo info in terminal.AvailableCommands)
            {
                Assert.AreNotEqual(TerminalCommandProcessor.ShutdownCommand, info.Name,
                    "`shutdown` must not be listed before the end state.");
            }
        }

        [Test]
        public void test_FinalNightTimerExpiring_EntersOutOfControlWithoutEndingTheNight()
        {
            NetworkSimulation simulation = CreateCampaignSimulation();

            var night = EmptyNight(20f);
            night.EndsOutOfControl = true;

            int outOfControlEvents = 0;
            NightReport report = null;
            simulation.OnOutOfControl += () => outOfControlEvents++;
            simulation.OnNightEnded += captured => report = captured;

            simulation.StartNight(night);
            Advance(simulation, 40f);

            Assert.IsTrue(simulation.IsOutOfControl, "The final night must end in «сеть вне контроля».");
            Assert.IsTrue(simulation.IsNightActive, "The out-of-control state is not the end of the night.");
            Assert.IsNull(report, "No shift report is raised until the player shuts the network down.");
            Assert.AreEqual(1, outOfControlEvents, "OnOutOfControl must fire exactly once.");
        }

        [Test]
        public void test_OutOfControl_LeavesOnlyShutdownAllAvailable()
        {
            NetworkSimulation simulation = CreateCampaignSimulation();
            var terminal = new TerminalCommandProcessor(simulation);

            var night = EmptyNight(20f);
            night.EndsOutOfControl = true;
            simulation.StartNight(night);
            Advance(simulation, 40f);

            Assert.AreEqual(TerminalResultCode.CommandLockedOutOfControl, terminal.Execute("isolate srv-1").Code);
            Assert.AreEqual(TerminalResultCode.CommandLockedOutOfControl, terminal.Execute("scan").Code);
            Assert.AreEqual(TerminalResultCode.CommandLockedOutOfControl, terminal.Execute("patch srv-1").Code);

            // `help` survives so the one remaining command is discoverable, and it lists it.
            TerminalCommandResult help = terminal.Execute("help");
            Assert.IsTrue(help.Success);

            var listed = new List<string>();
            foreach (TerminalCommandInfo info in help.Commands)
            {
                listed.Add(info.Name);
            }

            Assert.Contains(TerminalCommandProcessor.ShutdownCommand, listed);
            Assert.IsFalse(listed.Contains(TerminalCommandProcessor.IsolateCommand));
        }

        [Test]
        public void test_ShutdownAllWhileOutOfControl_IsTheVictoryEnding()
        {
            NetworkSimulation simulation = CreateCampaignSimulation();
            var terminal = new TerminalCommandProcessor(simulation);

            var night = EmptyNight(20f);
            night.EndsOutOfControl = true;

            int victories = 0;
            NightReport report = null;
            simulation.OnVictory += () => victories++;
            simulation.OnNightEnded += captured => report = captured;

            simulation.StartNight(night);
            Advance(simulation, 40f);

            TerminalCommandResult result = terminal.Execute("SHUTDOWN --ALL"); // case-insensitive, as every command is

            Assert.IsTrue(result.Success, result.Diagnostic);
            Assert.AreEqual(TerminalResultCode.ShutdownApplied, result.Code);
            Assert.AreEqual(1, victories);
            Assert.IsTrue(simulation.IsVictory);
            Assert.IsFalse(simulation.IsNightActive);
            Assert.IsNotNull(report);
            Assert.IsTrue(report.Victory, "The final report must be flagged as the victory ending.");
            Assert.IsEmpty(simulation.ActivePackets);
        }

        [Test]
        public void test_ShutdownWithoutTheAllFlag_IsRejected()
        {
            NetworkSimulation simulation = CreateCampaignSimulation();
            var terminal = new TerminalCommandProcessor(simulation);

            var night = EmptyNight(20f);
            night.EndsOutOfControl = true;
            simulation.StartNight(night);
            Advance(simulation, 40f);

            TerminalCommandResult result = terminal.Execute("shutdown srv-1");

            Assert.IsFalse(result.Success);
            Assert.AreEqual(TerminalResultCode.InvalidArgument, result.Code);
            Assert.IsFalse(simulation.IsVictory);
        }

        // ------------------------------------------------------------------
        // AC6 — the campaign is winnable: a reasonable starting defence on the
        // shipped seed survives nights 1-2 without losing.
        // ------------------------------------------------------------------

        /// <summary>
        /// The «разумная стартовая оборона»: the given starting network plus the one purchase the
        /// opening 200 credits comfortably affords — an IDS wired to the first Firewall, which reveals
        /// hidden packets in time for that Firewall to filter them.
        /// </summary>
        private static NetworkSimulation CreateReasonablyDefendedSimulation()
        {
            NetworkSimulation simulation = CreateCampaignSimulation();

            Node firewallA = simulation.Graph.GetNodeAt(3, 3);
            Assert.IsNotNull(firewallA, "Expected the starting network's first Firewall at (3,3).");

            Node ids = PlaceOrFail(simulation, 3, 1, NodeType.Ids);
            LinkOrFail(simulation, ids.Id, firewallA.Id);

            Assert.GreaterOrEqual(simulation.Credits, 0f, "The opening budget must cover this defence.");
            return simulation;
        }

        [Test]
        public void test_ReasonableStartingDefence_SurvivesNights1And2OnTheShippedSeed()
        {
            NetworkSimulation simulation = CreateReasonablyDefendedSimulation();

            NightReport first = RunNight(simulation, NightLibrary.CreateNight(1));

            Assert.IsFalse(first.CoreDestroyed, "Night 1 must be survivable with a reasonable defence.");
            Assert.Greater(first.RemainingCoreIntegrity, 0);
            Assert.Greater(first.PacketsBlocked, 0, "The defence should actually be stopping traffic.");

            NightReport second = RunNight(simulation, NightLibrary.CreateNight(2));

            Assert.IsFalse(second.CoreDestroyed, "Night 2 must be survivable with a reasonable defence.");
            Assert.Greater(second.RemainingCoreIntegrity, 0);
            Assert.Greater(simulation.Credits, 0f, "Surviving should leave the player something to build with.");
        }

        [Test]
        public void test_ReasonableStartingDefence_DoesNotSurviveNight2ForFree()
        {
            NetworkSimulation simulation = CreateReasonablyDefendedSimulation();

            RunNight(simulation, NightLibrary.CreateNight(1));
            NightReport second = RunNight(simulation, NightLibrary.CreateNight(2));

            Assert.Greater(
                second.CoreDamageTaken,
                0,
                "Night 2 introduces «брутфорс», which a level 1 Firewall pair cannot kill — it must cost the player something.");
        }
    }
}
