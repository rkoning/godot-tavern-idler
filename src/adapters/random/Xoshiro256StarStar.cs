using System.Numerics;
using TavernIdler.Kernel;

namespace TavernIdler.Adapters.Random;

/// <summary>
/// xoshiro256** (Blackman &amp; Vigna, 2018) — an explicit, fully specified PRNG, chosen over
/// <c>System.Random</c> because CON-015 requires sequences that are stable across process runs
/// and platforms. 256-bit state, 2^256−1 period, passes BigCrush.
/// </summary>
internal sealed class Xoshiro256StarStar : IRandom
{
    private ulong _s0, _s1, _s2, _s3;

    internal Xoshiro256StarStar(ulong seed) => Reseed(seed);

    /// <summary>Replace the state with one expanded from <paramref name="seed"/> via SplitMix64.</summary>
    internal void Reseed(ulong seed)
    {
        var state = seed;
        _s0 = SplitMix64.Next(ref state);
        _s1 = SplitMix64.Next(ref state);
        _s2 = SplitMix64.Next(ref state);
        _s3 = SplitMix64.Next(ref state);

        // The all-zero state is xoshiro's single fixed point (it would emit zeros forever).
        // SplitMix64 makes this astronomically unlikely, but the generator is only correct if
        // it cannot happen at all.
        if ((_s0 | _s1 | _s2 | _s3) == 0)
            _s0 = _s1 = _s2 = _s3 = 0x9E3779B97F4A7C15UL;
    }

    /// <inheritdoc />
    public double NextDouble()
    {
        // Top 53 bits → the exactly-representable integers in [0, 2^53), scaled into [0.0, 1.0).
        // Never reaches 1.0: the largest value is (2^53 − 1) / 2^53.
        return (NextUInt64() >> 11) * (1.0 / (1UL << 53));
    }

    /// <inheritdoc />
    public int NextInt(int maxExclusive)
    {
        if (maxExclusive < 1)
            throw new ArgumentOutOfRangeException(
                nameof(maxExclusive), maxExclusive, "maxExclusive must be >= 1.");

        // Lemire's multiply-and-shift with rejection: unbiased, and rejects on well under 1 in
        // 2^32 draws for any bound. Plain `% maxExclusive` would skew low values.
        var bound = (ulong)(uint)maxExclusive;
        var high = Math.BigMul(NextUInt64(), bound, out var low);
        if (low < bound)
        {
            var threshold = (0UL - bound) % bound; // 2^64 mod bound
            while (low < threshold)
                high = Math.BigMul(NextUInt64(), bound, out low);
        }

        return (int)high;
    }

    private ulong NextUInt64()
    {
        var result = BitOperations.RotateLeft(_s1 * 5UL, 7) * 9UL;
        var t = _s1 << 17;

        _s2 ^= _s0;
        _s3 ^= _s1;
        _s1 ^= _s2;
        _s0 ^= _s3;
        _s2 ^= t;
        _s3 = BitOperations.RotateLeft(_s3, 45);

        return result;
    }
}
