namespace NightShift.Core
{
    /// <summary>
    /// Identifies the behavioural profile of a packet (speed/HP/damage/stealth plus the Story 005
    /// special behaviours), looked up in <see cref="GameData.PacketDefinitions"/>. Story 001 ships
    /// the two baseline profiles (<see cref="Standard"/>, <see cref="Stealth"/>) needed to exercise
    /// every security tool; Story 005 adds the five escalating attack types named by acceptance
    /// criterion 2 of `production/epics/night-shift/story-005-five-nights-escalation.md`.
    /// </summary>
    /// <remarks>
    /// Every number and every behaviour switch lives on <see cref="PacketDefinition"/>, never on
    /// this enum: <see cref="Packet"/> and <see cref="NetworkSimulation"/> branch on definition
    /// flags (<see cref="PacketDefinition.DisablesTargetServer"/>,
    /// <see cref="PacketDefinition.InfectsServer"/>,
    /// <see cref="PacketDefinition.CanCrossMissingLinks"/>), so a designer can re-skin which type
    /// does what purely as data.
    /// </remarks>
    public enum PacketType
    {
        /// <summary>Baseline visible attack packet.</summary>
        Standard = 0,

        /// <summary>Baseline hidden attack packet — invisible to Firewalls until an IDS reveals it.</summary>
        Stealth = 1,

        /// <summary>«скан» — fast and weak: arrives long before the player can react, but dies to any Firewall.</summary>
        Scan = 2,

        /// <summary>«брутфорс» — slow and tough: survives level 1 Firewalls, so it has to be upgraded or trapped away.</summary>
        BruteForce = 3,

        /// <summary>«DDoS» — many weak packets aimed at one Server; arrival takes that Server offline (see <see cref="PacketDefinition.DisablesTargetServer"/>).</summary>
        Ddos = 4,

        /// <summary>«червь» — infects the first Server it reaches; the infected Server then spawns more worms towards its neighbours until <c>patch</c> (see <see cref="PacketDefinition.InfectsServer"/>).</summary>
        Worm = 5,

        /// <summary>«аномалия» — hidden, and able to step between grid-adjacent cells where no link exists (see <see cref="PacketDefinition.CanCrossMissingLinks"/>).</summary>
        Anomaly = 6,
    }
}
