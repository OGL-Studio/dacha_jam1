namespace NightShift.Core
{
    /// <summary>
    /// End-of-night summary, raised once via <see cref="NetworkSimulation.OnNightEnded"/>.
    /// </summary>
    public sealed class NightReport
    {
        public int NightNumber { get; set; }

        /// <summary>Credits earned from server income during this night ("заработано").</summary>
        public float CreditsEarned { get; set; }

        /// <summary>Packets destroyed by a defense (Firewall or Honeypot) before reaching the Core ("заблокировано").</summary>
        public int PacketsBlocked { get; set; }

        /// <summary>Packets that reached the Core ("пропущено").</summary>
        public int PacketsLeaked { get; set; }

        /// <summary>Packets that dissipated with no damage because no path to the Core existed. Neither blocked nor leaked.</summary>
        public int PacketsDissipated { get; set; }

        /// <summary>Total Core integrity lost during this night ("урон").</summary>
        public int CoreDamageTaken { get; set; }

        /// <summary>Core integrity remaining at the moment the night ended.</summary>
        public int RemainingCoreIntegrity { get; set; }

        /// <summary>True if Core integrity had already reached 0 when the night ended.</summary>
        public bool CoreDestroyed { get; set; }
    }
}
