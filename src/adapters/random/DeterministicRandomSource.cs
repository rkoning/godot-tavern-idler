using TavernIdler.Kernel;

namespace TavernIdler.Adapters.Random;

/// <summary>
/// CON-015 <see cref="IRandomSource"/> adapter: deterministic, seedable, replayable.
///
/// <para><b>Algorithm.</b> Each named stream is an independent <b>xoshiro256**</b> generator
/// (Blackman &amp; Vigna) whose 256-bit state is expanded from a single 64-bit stream seed by
/// <b>SplitMix64</b>. The stream seed is derived as:</para>
///
/// <code>
///   z = SplitMix64Mix( (ulong)Seed        ^ Fnv1a64(name) )
///   z = SplitMix64Mix( z                  ^ (ulong)nightNumber )
/// </code>
///
/// <para><b>System.Random is deliberately not used</b>: its algorithm is unspecified and has
/// changed between .NET versions, so it cannot satisfy CON-015's "stable across process runs
/// and platforms" requirement. Everything here is fixed-width unsigned integer arithmetic
/// (<c>ulong</c> wraps identically on every platform .NET targets) and the string hash is an
/// explicit FNV-1a-64 over UTF-8 bytes — never <see cref="string.GetHashCode()"/>, which is
/// randomised per process. The byte stream is pinned by golden vectors in
/// <c>tests/adapters/random/</c>, generated from an independent reference implementation.</para>
///
/// <para><b>Night reseeding.</b> PRNG state is not serialized (CON-015 / Decision C). The
/// orchestrator calls <see cref="BeginNight"/> at BeginService/BeginNight; every stream —
/// including ones handed out earlier — is reseeded in place from <c>(Seed, name, nightNumber)</c>,
/// so replaying a night reproduces it exactly without persisting PRNG state.</para>
///
/// <para>Not thread-safe, per contract (single-threaded use, CON-016).</para>
/// </summary>
public sealed class DeterministicRandomSource : IRandomSource
{
    private readonly Dictionary<string, Xoshiro256StarStar> _streams = new(StringComparer.Ordinal);

    public DeterministicRandomSource(long seed, int nightNumber)
    {
        Seed = seed;
        NightNumber = nightNumber;
    }

    /// <inheritdoc />
    public long Seed { get; }

    /// <summary>The night whose sequences the streams are currently positioned on.</summary>
    public int NightNumber { get; private set; }

    /// <inheritdoc />
    public IRandom GetStream(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (!_streams.TryGetValue(name, out var stream))
        {
            stream = new Xoshiro256StarStar(DeriveStreamSeed(Seed, name, NightNumber));
            _streams.Add(name, stream);
        }

        return stream;
    }

    /// <summary>
    /// Re-derive every stream's state for <paramref name="nightNumber"/>. Stream instances are
    /// preserved (<see cref="GetStream"/> idempotence holds across the call); only their state
    /// is replaced.
    /// </summary>
    public void BeginNight(int nightNumber)
    {
        NightNumber = nightNumber;
        foreach (var (name, stream) in _streams)
            stream.Reseed(DeriveStreamSeed(Seed, name, nightNumber));
    }

    private static ulong DeriveStreamSeed(long seed, string name, int nightNumber)
    {
        var z = SplitMix64.Mix((ulong)seed ^ Fnv1a64(name));
        return SplitMix64.Mix(z ^ (ulong)(long)nightNumber);
    }

    /// <summary>FNV-1a 64-bit over the UTF-8 bytes of <paramref name="name"/>. Ordinal and stable.</summary>
    private static ulong Fnv1a64(string name)
    {
        const ulong offsetBasis = 0xCBF29CE484222325UL;
        const ulong prime = 0x100000001B3UL;

        var hash = offsetBasis;
        foreach (var b in System.Text.Encoding.UTF8.GetBytes(name))
        {
            hash ^= b;
            hash *= prime;
        }

        return hash;
    }
}
