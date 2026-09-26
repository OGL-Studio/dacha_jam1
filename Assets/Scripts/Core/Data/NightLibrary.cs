using System;

namespace NightShift.Core.Data
{
    /// <summary>
    /// The five authored nights — Story 005 acceptance criterion 1 of
    /// `production/epics/night-shift/story-005-five-nights-escalation.md`: «5 ночей описаны данными
    /// (длительность 300-420 с, волны: тип, количество, интервал, цель); код ночей не содержит чисел
    /// баланса».
    /// </summary>
    /// <remarks>
    /// <para><b>This class is the data file.</b> Every balance number of the campaign lives here as a
    /// field of a <see cref="NightData"/> / <see cref="NightWave"/> / <see cref="AlienNodeSpawn"/>
    /// initialiser, and nothing else in the game contains a night number: the simulation reads these
    /// objects, and the Unity layer's <c>NightContent</c> only forwards to
    /// <see cref="CreateNight"/>. Per-type attack stats live in <see cref="GameData"/> for the same
    /// reason. Re-balance the campaign by editing this one file.</para>
    ///
    /// <para><b>The escalation, night by night.</b></para>
    /// <list type="bullet">
    ///   <item><description><b>1</b> (300s) — the Story 002 night, unchanged: Standard and Stealth only.</description></item>
    ///   <item><description><b>2</b> (320s) — «скан» arrives (fast, weak) and two «брутфорс» packets that a level 1 Firewall pair cannot kill.</description></item>
    ///   <item><description><b>3</b> (350s) — «DDoS» aimed at a Server, and the first two nodes the network grows by itself.</description></item>
    ///   <item><description><b>4</b> (390s) — «червь» and «аномалия» join in, two more intruders appear, and the terminal starts missing (<see cref="NightData.CommandMisfireChance"/>).</description></item>
    ///   <item><description><b>5</b> (420s) — everything at once, the terminal is properly unreliable, and the night does not report: it ends in «сеть вне контроля» (<see cref="NightData.EndsOutOfControl"/>), where <c>shutdown --all</c> is the victory ending.</description></item>
    /// </list>
    ///
    /// <para><b>Determinism.</b> Nothing here draws randomness. Wave jitter is drawn inside
    /// <see cref="NetworkSimulation.StartNight"/> from the simulation's injected
    /// <see cref="IRandomSource"/>, so a given seed plus a given night number always produces the
    /// same schedule.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var sim = new NetworkSimulation(NightLibrary.CreateGameData(),
    ///                                 new SystemRandomSource(NightLibrary.SimulationSeed),
    ///                                 NightLibrary.StartingCredits);
    /// StartingNetwork.TryBuild(sim, out _);
    /// sim.StartNight(NightLibrary.CreateNight(1));
    /// </code>
    /// </example>
    public static class NightLibrary
    {
        /// <summary>How many nights the campaign has. Night <see cref="NightCount"/> is the one that ends out of control.</summary>
        public const int NightCount = 5;

        /// <summary>Seed for the simulation's randomness stream (wave jitter, DDoS target choice, worm spread, command misfires).</summary>
        public const int SimulationSeed = 20260926;

        /// <summary>
        /// Credits the player owns when day 1 begins. Enough for two or three tools plus their links
        /// at the default <see cref="GameData"/> costs, not enough to fortify the whole grid.
        /// </summary>
        public const float StartingCredits = 200f;

        /// <summary>Jitter applied to every wave so a night does not read as a metronome, in seconds.</summary>
        private const float StandardJitter = 2f;

        /// <summary>Tight jitter for the fast «скан» waves, whose intervals are short.</summary>
        private const float ShortJitter = 1f;

        /// <summary>The tuning set for the campaign: <see cref="GameData"/> at its authored defaults.</summary>
        public static GameData CreateGameData() => new GameData();

        /// <summary>
        /// The <see cref="NightData"/> for a 1-based night number. Numbers below 1 are treated as
        /// night 1; numbers above <see cref="NightCount"/> repeat the final out-of-control night, so a
        /// caller that keeps counting days can never fall off the end of the campaign.
        /// </summary>
        /// <remarks>A fresh instance every call — the caller may mutate it without affecting later nights.</remarks>
        public static NightData CreateNight(int nightNumber)
        {
            int clamped = nightNumber < 1 ? 1 : (nightNumber > NightCount ? NightCount : nightNumber);

            switch (clamped)
            {
                case 1: return CreateNight1();
                case 2: return CreateNight2();
                case 3: return CreateNight3();
                case 4: return CreateNight4();
                case 5: return CreateNight5();
                default: throw new InvalidOperationException("Unreachable: night " + clamped + " is not authored.");
            }
        }

        /// <summary>
        /// Night 1 — the tutorial night: Standard traffic with a Stealth packet in every third slot,
        /// exactly the schedule Story 002 shipped, now expressed as waves.
        /// </summary>
        private static NightData CreateNight1()
        {
            var night = new NightData
            {
                NightNumber = 1,
                NightDuration = 300f,
            };

            // Three interleaved waves reproduce the original "every third spawn is Stealth" pattern
            // on a 25s beat: slots 0,3,6,9 / 1,4,7,10 / 2,5,8,11.
            night.Waves.Add(new NightWave { Type = PacketType.Standard, Count = 4, StartTime = 8f, IntervalSeconds = 75f, JitterSeconds = StandardJitter });
            night.Waves.Add(new NightWave { Type = PacketType.Standard, Count = 4, StartTime = 33f, IntervalSeconds = 75f, JitterSeconds = StandardJitter });
            night.Waves.Add(new NightWave { Type = PacketType.Stealth, Count = 4, StartTime = 58f, IntervalSeconds = 75f, JitterSeconds = StandardJitter });

            return night;
        }

        /// <summary>Night 2 — «скан» and «брутфорс» arrive: speed the player cannot react to, and HP a level 1 Firewall pair cannot chew through.</summary>
        private static NightData CreateNight2()
        {
            var night = new NightData
            {
                NightNumber = 2,
                NightDuration = 320f,
            };

            night.Waves.Add(new NightWave { Type = PacketType.Scan, Count = 8, StartTime = 10f, IntervalSeconds = 12f, JitterSeconds = ShortJitter });
            night.Waves.Add(new NightWave { Type = PacketType.Standard, Count = 8, StartTime = 20f, IntervalSeconds = 35f, JitterSeconds = StandardJitter });
            night.Waves.Add(new NightWave { Type = PacketType.Stealth, Count = 3, StartTime = 60f, IntervalSeconds = 80f, JitterSeconds = StandardJitter });
            night.Waves.Add(new NightWave { Type = PacketType.BruteForce, Count = 2, StartTime = 150f, IntervalSeconds = 90f, JitterSeconds = StandardJitter });

            return night;
        }

        /// <summary>Night 3 — «DDoS» aims at a Server instead of the Core, and the network starts growing nodes of its own.</summary>
        private static NightData CreateNight3()
        {
            var night = new NightData
            {
                NightNumber = 3,
                NightDuration = 350f,
            };

            night.Waves.Add(new NightWave { Type = PacketType.Scan, Count = 10, StartTime = 8f, IntervalSeconds = 11f, JitterSeconds = ShortJitter });
            night.Waves.Add(new NightWave { Type = PacketType.Standard, Count = 10, StartTime = 15f, IntervalSeconds = 30f, JitterSeconds = StandardJitter });
            night.Waves.Add(new NightWave { Type = PacketType.Stealth, Count = 4, StartTime = 55f, IntervalSeconds = 70f, JitterSeconds = StandardJitter });
            night.Waves.Add(new NightWave { Type = PacketType.BruteForce, Count = 3, StartTime = 120f, IntervalSeconds = 75f, JitterSeconds = StandardJitter });
            night.Waves.Add(new NightWave
            {
                Type = PacketType.Ddos,
                Count = 6,
                StartTime = 190f,
                IntervalSeconds = 5f,
                JitterSeconds = ShortJitter,
                Target = WaveTargetKind.Server,
            });

            night.AlienNodes.Add(new AlienNodeSpawn { SpawnTime = 130f, X = 5, Y = 5, Type = NodeType.Server, LinkCount = 1 });
            night.AlienNodes.Add(new AlienNodeSpawn { SpawnTime = 260f, X = 9, Y = 1, Type = NodeType.Server, LinkCount = 2 });

            return night;
        }

        /// <summary>Night 4 — «червь» and «аномалия», two more intruders, and a terminal that starts hitting the wrong node.</summary>
        private static NightData CreateNight4()
        {
            var night = new NightData
            {
                NightNumber = 4,
                NightDuration = 390f,
                CommandMisfireChance = 0.12f,
            };

            night.Waves.Add(new NightWave { Type = PacketType.Scan, Count = 12, StartTime = 6f, IntervalSeconds = 10f, JitterSeconds = ShortJitter });
            night.Waves.Add(new NightWave { Type = PacketType.Standard, Count = 10, StartTime = 12f, IntervalSeconds = 28f, JitterSeconds = StandardJitter });
            night.Waves.Add(new NightWave { Type = PacketType.Stealth, Count = 5, StartTime = 45f, IntervalSeconds = 60f, JitterSeconds = StandardJitter });
            night.Waves.Add(new NightWave { Type = PacketType.BruteForce, Count = 3, StartTime = 90f, IntervalSeconds = 80f, JitterSeconds = StandardJitter });
            night.Waves.Add(new NightWave { Type = PacketType.Worm, Count = 2, StartTime = 140f, IntervalSeconds = 110f, JitterSeconds = StandardJitter });
            night.Waves.Add(new NightWave { Type = PacketType.Anomaly, Count = 3, StartTime = 200f, IntervalSeconds = 60f, JitterSeconds = StandardJitter });
            night.Waves.Add(new NightWave
            {
                Type = PacketType.Ddos,
                Count = 8,
                StartTime = 250f,
                IntervalSeconds = 4f,
                JitterSeconds = ShortJitter,
                Target = WaveTargetKind.Server,
            });

            night.AlienNodes.Add(new AlienNodeSpawn { SpawnTime = 70f, X = 2, Y = 1, Type = NodeType.Server, LinkCount = 2 });
            night.AlienNodes.Add(new AlienNodeSpawn { SpawnTime = 230f, X = 7, Y = 5, Type = NodeType.Server, LinkCount = 2 });

            return night;
        }

        /// <summary>
        /// Night 5 — everything at once, and the night the shift never reports: when the timer runs
        /// out the network is «вне контроля» and only <c>shutdown --all</c> is left.
        /// </summary>
        private static NightData CreateNight5()
        {
            var night = new NightData
            {
                NightNumber = 5,
                NightDuration = 420f,
                CommandMisfireChance = 0.2f,
                EndsOutOfControl = true,
            };

            night.Waves.Add(new NightWave { Type = PacketType.Scan, Count = 16, StartTime = 5f, IntervalSeconds = 9f, JitterSeconds = ShortJitter });
            night.Waves.Add(new NightWave { Type = PacketType.Standard, Count = 12, StartTime = 10f, IntervalSeconds = 26f, JitterSeconds = StandardJitter });
            night.Waves.Add(new NightWave { Type = PacketType.Stealth, Count = 6, StartTime = 40f, IntervalSeconds = 55f, JitterSeconds = StandardJitter });
            night.Waves.Add(new NightWave { Type = PacketType.BruteForce, Count = 4, StartTime = 70f, IntervalSeconds = 70f, JitterSeconds = StandardJitter });
            night.Waves.Add(new NightWave { Type = PacketType.Worm, Count = 3, StartTime = 110f, IntervalSeconds = 90f, JitterSeconds = StandardJitter });
            night.Waves.Add(new NightWave { Type = PacketType.Anomaly, Count = 5, StartTime = 150f, IntervalSeconds = 45f, JitterSeconds = StandardJitter });
            night.Waves.Add(new NightWave
            {
                Type = PacketType.Ddos,
                Count = 10,
                StartTime = 230f,
                IntervalSeconds = 4f,
                JitterSeconds = ShortJitter,
                Target = WaveTargetKind.Server,
            });
            night.Waves.Add(new NightWave
            {
                Type = PacketType.Ddos,
                Count = 10,
                StartTime = 330f,
                IntervalSeconds = 3f,
                JitterSeconds = ShortJitter,
                Target = WaveTargetKind.Server,
            });

            return night;
        }
    }
}
