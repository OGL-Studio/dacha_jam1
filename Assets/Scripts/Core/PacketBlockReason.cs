namespace NightShift.Core
{
    /// <summary>Why a packet was destroyed by a defense before reaching the Core.</summary>
    public enum PacketBlockReason
    {
        Firewall,
        Honeypot,
    }
}
