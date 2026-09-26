using System;

namespace NightShift.Core
{
    /// <summary>Default <see cref="IRandomSource"/>, backed by a seeded <see cref="System.Random"/>.</summary>
    /// <example>
    /// <code>
    /// var sim = new NetworkSimulation(new GameData(), new SystemRandomSource(12345));
    /// </code>
    /// </example>
    public sealed class SystemRandomSource : IRandomSource
    {
        private readonly Random _random;

        public SystemRandomSource(int seed)
        {
            _random = new Random(seed);
        }

        public int NextInt(int minInclusive, int maxExclusive) => _random.Next(minInclusive, maxExclusive);

        public double NextDouble() => _random.NextDouble();
    }
}
