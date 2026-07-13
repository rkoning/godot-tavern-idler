using System;
using System.Collections.Generic;
using System.Linq;
using TavernIdler.Kernel;

namespace TavernIdler.Tests.Contracts.Random;

/// <summary>
/// CON-015 abstract conformance suite for <see cref="IRandomSource"/> / <see cref="IRandom"/>.
///
/// This class is ABSTRACT and defines no runnable tests on its own — xUnit does not
/// instantiate abstract classes. The RNG adapter ticket (TKT-017) provides a sealed
/// subclass implementing <see cref="CreateSut"/>, at which point every test below runs
/// against the real adapter. This ticket (TKT-001) only defines the suite.
///
/// The frozen port (CON-015) exposes only <c>GetStream(name)</c> and <c>Seed</c>; per-night
/// reseeding (seed re-derived as hash(Seed, NightNumber)) is applied out-of-band by the
/// implementer at BeginService/BeginNight. The suite therefore parameterises the factory by
/// both <paramref name="seed"/> and <paramref name="nightNumber"/> so it can exercise
/// determinism, stream independence, per-night reseeding, and bounds through the narrow port.
/// </summary>
public abstract class RandomSourceConformanceTests
{
    /// <summary>
    /// Produce a fresh <see cref="IRandomSource"/> for the given base seed positioned at the
    /// given night number. Two calls with equal arguments MUST yield sources that emit
    /// identical sequences per named stream. <c>Seed</c> MUST return <paramref name="seed"/>.
    /// </summary>
    protected abstract IRandomSource CreateSut(long seed, int nightNumber);

    private const long Seed = 0x5EEDF00DL;

    private static double[] DrawDoubles(IRandom r, int count)
    {
        var xs = new double[count];
        for (var i = 0; i < count; i++) xs[i] = r.NextDouble();
        return xs;
    }

    // ── Seed property ───────────────────────────────────────────
    [Fact]
    public void Seed_property_echoes_base_seed()
    {
        Assert.Equal(Seed, CreateSut(Seed, 1).Seed);
    }

    // ── GetStream idempotence ───────────────────────────────────
    [Fact]
    public void GetStream_returns_same_instance_per_name()
    {
        var src = CreateSut(Seed, 1);
        Assert.Same(src.GetStream("guests"), src.GetStream("guests"));
        Assert.NotSame(src.GetStream("guests"), src.GetStream("traits"));
    }

    // ── Determinism: same (seed, name) ⇒ identical 10k-draw sequences ──
    [Fact]
    public void Same_seed_and_name_yield_identical_sequences_across_instances()
    {
        var a = DrawDoubles(CreateSut(Seed, 3).GetStream("guests"), 10_000);
        var b = DrawDoubles(CreateSut(Seed, 3).GetStream("guests"), 10_000);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Different_stream_names_yield_different_sequences()
    {
        var g = DrawDoubles(CreateSut(Seed, 3).GetStream("guests"), 1_000);
        var t = DrawDoubles(CreateSut(Seed, 3).GetStream("traits"), 1_000);
        Assert.NotEqual(g, t);
    }

    // ── Stream independence: interleaving must not perturb either sequence ──
    [Fact]
    public void Interleaved_consumption_does_not_alter_either_stream()
    {
        const int n = 1_000;
        var guestsSolo = DrawDoubles(CreateSut(Seed, 5).GetStream("guests"), n);
        var traitsSolo = DrawDoubles(CreateSut(Seed, 5).GetStream("traits"), n);

        var src = CreateSut(Seed, 5);
        var guests = src.GetStream("guests");
        var traits = src.GetStream("traits");
        var guestsInter = new double[n];
        var traitsInter = new double[n];
        for (var i = 0; i < n; i++)
        {
            guestsInter[i] = guests.NextDouble();
            traitsInter[i] = traits.NextDouble();
        }

        Assert.Equal(guestsSolo, guestsInter);
        Assert.Equal(traitsSolo, traitsInter);
    }

    // ── Night reseeding: night N is identical regardless of night N−1 activity ──
    [Fact]
    public void Night_sequence_is_independent_of_previous_night_consumption()
    {
        var nightN = DrawDoubles(CreateSut(Seed, 7).GetStream("guests"), 100);

        // Simulate a heavily-consumed previous night, then re-derive night N.
        DrawDoubles(CreateSut(Seed, 6).GetStream("guests"), 500);
        var nightNAgain = DrawDoubles(CreateSut(Seed, 7).GetStream("guests"), 100);

        Assert.Equal(nightN, nightNAgain);
    }

    [Fact]
    public void Different_nights_yield_different_sequences()
    {
        var night1 = DrawDoubles(CreateSut(Seed, 1).GetStream("guests"), 500);
        var night2 = DrawDoubles(CreateSut(Seed, 2).GetStream("guests"), 500);
        Assert.NotEqual(night1, night2);
    }

    // ── Bounds ──────────────────────────────────────────────────
    [Fact]
    public void NextDouble_stays_in_unit_interval_over_100k_draws()
    {
        var r = CreateSut(Seed, 1).GetStream("guests");
        for (var i = 0; i < 100_000; i++)
        {
            var d = r.NextDouble();
            Assert.InRange(d, 0.0, 1.0);
            Assert.NotEqual(1.0, d); // half-open: never returns 1.0
        }
    }

    [Fact]
    public void NextInt_stays_within_bounds()
    {
        var r = CreateSut(Seed, 1).GetStream("traits");
        for (var i = 0; i < 100_000; i++)
        {
            var v = r.NextInt(6);
            Assert.InRange(v, 0, 5);
        }
    }

    [Fact]
    public void NextInt_one_is_always_zero()
    {
        var r = CreateSut(Seed, 1).GetStream("traits");
        for (var i = 0; i < 1_000; i++) Assert.Equal(0, r.NextInt(1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void NextInt_below_one_throws(int maxExclusive)
    {
        var r = CreateSut(Seed, 1).GetStream("traits");
        Assert.Throws<ArgumentOutOfRangeException>(() => r.NextInt(maxExclusive));
    }

    // ── Uniformity smoke test (loose chi-squared, deterministic) ──
    [Fact]
    public void NextInt_is_roughly_uniform()
    {
        const int buckets = 10;
        const int draws = 100_000;
        var counts = new int[buckets];
        var r = CreateSut(Seed, 1).GetStream("guests");
        for (var i = 0; i < draws; i++) counts[r.NextInt(buckets)]++;

        double expected = (double)draws / buckets;
        double chiSquared = counts.Sum(c => Math.Pow(c - expected, 2) / expected);

        // 9 dof; critical value ≈ 27.9 at p=0.001. Loose bound catches gross non-uniformity
        // without depending on the adapter's exact algorithm.
        Assert.True(chiSquared < 50.0, $"chi-squared {chiSquared:F2} too high; distribution not uniform");
        Assert.All(counts, c => Assert.True(c > 0, "every bucket must be hit"));
    }
}
