namespace TavernIdler.Adapters.Random;

/// <summary>
/// SplitMix64 (Steele, Lea &amp; Flood) — used solely to expand a 64-bit seed into the 256-bit
/// xoshiro256** state, as recommended by that generator's authors. Its avalanche is what stops
/// adjacent seeds (1, 2, 3…) or similar stream names from producing correlated streams.
/// </summary>
internal static class SplitMix64
{
    private const ulong Golden = 0x9E3779B97F4A7C15UL;

    /// <summary>Advance <paramref name="state"/> by one step and return that step's output.</summary>
    internal static ulong Next(ref ulong state)
    {
        state += Golden;
        return Finalize(state);
    }

    /// <summary>Avalanche <paramref name="z"/> without advancing a counter (seed derivation).</summary>
    internal static ulong Mix(ulong z) => Finalize(z);

    private static ulong Finalize(ulong z)
    {
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
