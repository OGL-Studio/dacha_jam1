namespace NightShift.Core
{
    /// <summary>
    /// Identifies the behavioural profile of a packet (speed/HP/damage/stealth), looked up in
    /// <see cref="GameData.PacketDefinitions"/>. Story 001 ships the two baseline profiles
    /// (<see cref="Standard"/>, <see cref="Stealth"/>) needed to exercise every security tool.
    /// Story 005 extends this enum with the escalating night 2-5 attack types (scan,
    /// brute-force, DDoS, worm, anomaly). <see cref="Packet"/> and <see cref="NetworkSimulation"/>
    /// only ever read numeric fields off <see cref="PacketDefinition"/>, so adding new enum
    /// members plus a matching <see cref="GameData.PacketDefinitions"/> entry is enough — no
    /// change to the movement/effect pipeline is required.
    /// </summary>
    public enum PacketType
    {
        /// <summary>Baseline visible attack packet.</summary>
        Standard = 0,

        /// <summary>Baseline hidden attack packet — invisible to Firewalls until an IDS reveals it.</summary>
        Stealth = 1,
    }
}
