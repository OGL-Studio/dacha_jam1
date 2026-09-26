using NightShift.Core;
using NightShift.Core.Data;
using UnityEngine;

namespace NightShift.Game
{
    /// <summary>
    /// The Unity layer's thin door onto the game's content: the five authored nights and the fixed
    /// starting network. Every number behind it now lives in <c>NightShift.Core.Data</c>
    /// (<see cref="NightLibrary"/>, <see cref="StartingNetwork"/>).
    /// </summary>
    /// <remarks>
    /// <para><b>Why it is now a forwarder.</b> Up to Story 004 this class authored night 1 and the
    /// starting topology itself, in the Unity assembly. Story 005 needs five nights and a headless
    /// test that a reasonable defence survives nights 1-2 (acceptance criterion 6), and the test
    /// cannot see the Unity assembly — so the content moved into Core and this class kept only the
    /// Unity-facing surface <see cref="GameBootstrap"/> calls, plus the editor-log error reporting
    /// that Core (which has no <c>Debug</c>) cannot do.</para>
    ///
    /// <para><b>Removed with Story 005:</b> <c>ScheduleSeed</c> and <c>CreateNight1(IRandomSource)</c>.
    /// Schedule jitter is no longer drawn at authoring time — it is drawn inside
    /// <see cref="NetworkSimulation.StartNight"/> from the simulation's own seeded
    /// <see cref="IRandomSource"/>, so there is one random stream instead of two and the night is
    /// still identical for a given seed.</para>
    /// </remarks>
    public static class NightContent
    {
        /// <summary>Seed for the simulation's randomness stream. See <see cref="NightLibrary.SimulationSeed"/>.</summary>
        public const int SimulationSeed = NightLibrary.SimulationSeed;

        /// <summary>Credits the player owns when day 1 begins. See <see cref="NightLibrary.StartingCredits"/>.</summary>
        public const float StartingCredits = NightLibrary.StartingCredits;

        /// <summary>How many nights the campaign has. See <see cref="NightLibrary.NightCount"/>.</summary>
        public const int NightCount = NightLibrary.NightCount;

        /// <summary>The tuning set for the campaign.</summary>
        public static GameData CreateGameData() => NightLibrary.CreateGameData();

        /// <summary>
        /// Night factory for <see cref="GameRunner.Initialize"/>: the <see cref="NightData"/> for a
        /// 1-based night number. Nights past the fifth repeat the final out-of-control night.
        /// </summary>
        public static NightData CreateNight(int nightNumber) => NightLibrary.CreateNight(nightNumber);

        /// <summary>Places the fixed starting topology, logging to the console if the grid refuses it.</summary>
        public static void BuildStartingNetwork(NetworkSimulation simulation)
        {
            if (!StartingNetwork.TryBuild(simulation, out string error))
            {
                Debug.LogError("[NightShift] Starting network: " + error);
            }
        }
    }
}
