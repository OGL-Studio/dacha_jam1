namespace NightShift.Core
{
    /// <summary>
    /// Seeded randomness injection point. The simulation never constructs <c>System.Random</c>
    /// itself and never reads the wall clock — inject an implementation of this (typically
    /// <see cref="SystemRandomSource"/> with a fixed seed) so that identical seed + identical
    /// action sequence always produces an identical result.
    /// </summary>
    /// <example>
    /// <code>
    /// IRandomSource rng = new SystemRandomSource(seed: 12345);
    /// var sim = new NetworkSimulation(new GameData(), rng);
    /// </code>
    /// </example>
    public interface IRandomSource
    {
        /// <summary>Returns an integer in [minInclusive, maxExclusive).</summary>
        int NextInt(int minInclusive, int maxExclusive);

        /// <summary>Returns a double in [0, 1).</summary>
        double NextDouble();
    }
}
