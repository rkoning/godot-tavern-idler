using System;
using System.Linq;
using TavernIdler.Adapters.Random;
using TavernIdler.Kernel;

namespace TavernIdler.Tests.Adapters.Random;

/// <summary>
/// Adapter-specific unit tests for <see cref="DeterministicRandomSource"/> (TKT-017).
///
/// The CON-015 conformance suite proves the port's semantics; these tests pin the things the
/// port cannot see: the exact algorithm (xoshiro256** over SplitMix64-derived state, with
/// FNV-1a-64 stream-name hashing) and the out-of-band <see cref="DeterministicRandomSource.BeginNight"/>
/// reseeding hook the contract requires implementers to expose.
///
/// The golden vectors below were produced by an independent reference implementation of
/// xoshiro256**/SplitMix64/FNV-1a-64 (Python), NOT by this C# code. They are what makes the
/// "stable across process runs and platforms" clause of CON-015 testable: any future refactor
/// that silently changes the byte stream fails here.
/// </summary>
public class DeterministicRandomSourceTests
{
    private const long Seed = 0x5EEDF00DL;

    // ── Golden vectors (independent reference implementation) ────────────

    [Fact]
    public void Guests_stream_night_1_matches_reference_vector()
    {
        var r = new DeterministicRandomSource(Seed, 1).GetStream("guests");

        Assert.Equal(0.9400573841239814, r.NextDouble());
        Assert.Equal(0.9750726873914078, r.NextDouble());
        Assert.Equal(0.3705185686629312, r.NextDouble());
    }

    [Fact]
    public void Guests_stream_night_2_matches_reference_vector()
    {
        var r = new DeterministicRandomSource(Seed, 2).GetStream("guests");

        Assert.Equal(0.592269579582559, r.NextDouble());
        Assert.Equal(0.5937454917059637, r.NextDouble());
        Assert.Equal(0.5985034743705926, r.NextDouble());
    }

    [Fact]
    public void Traits_stream_night_1_matches_reference_vector()
    {
        var r = new DeterministicRandomSource(Seed, 1).GetStream("traits");

        Assert.Equal(0.08002189575308882, r.NextDouble());
        Assert.Equal(0.627415095391601, r.NextDouble());
        Assert.Equal(0.7290173475458315, r.NextDouble());
    }

    // ── BeginNight: the reseeding hook CON-015 requires ──────────────────

    [Fact]
    public void BeginNight_reseeds_streams_to_the_new_nights_sequence()
    {
        var expectedNight2 = Draw(new DeterministicRandomSource(Seed, 2).GetStream("guests"), 20);

        var src = new DeterministicRandomSource(Seed, 1);
        var guests = src.GetStream("guests");
        Draw(guests, 137); // burn an arbitrary amount of night 1
        src.BeginNight(2);

        Assert.Equal(expectedNight2, Draw(guests, 20));
    }

    [Fact]
    public void BeginNight_preserves_stream_instance_identity()
    {
        var src = new DeterministicRandomSource(Seed, 1);
        var before = src.GetStream("guests");

        src.BeginNight(2);

        Assert.Same(before, src.GetStream("guests"));
    }

    [Fact]
    public void BeginNight_reseeds_streams_created_before_and_after_the_call_alike()
    {
        var src = new DeterministicRandomSource(Seed, 1);
        var early = src.GetStream("guests"); // exists across the reseed
        Draw(early, 10);

        src.BeginNight(4);
        var late = src.GetStream("traits"); // first requested after the reseed

        Assert.Equal(Draw(new DeterministicRandomSource(Seed, 4).GetStream("guests"), 10), Draw(early, 10));
        Assert.Equal(Draw(new DeterministicRandomSource(Seed, 4).GetStream("traits"), 10), Draw(late, 10));
    }

    [Fact]
    public void NightNumber_reports_the_current_night()
    {
        var src = new DeterministicRandomSource(Seed, 3);
        Assert.Equal(3, src.NightNumber);

        src.BeginNight(9);
        Assert.Equal(9, src.NightNumber);
    }

    // ── Seed-space sensitivity ───────────────────────────────────────────

    [Fact]
    public void Adjacent_seeds_yield_unrelated_sequences()
    {
        var a = Draw(new DeterministicRandomSource(1, 1).GetStream("guests"), 50);
        var b = Draw(new DeterministicRandomSource(2, 1).GetStream("guests"), 50);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Negative_seeds_are_supported()
    {
        var a = Draw(new DeterministicRandomSource(-1L, 1).GetStream("guests"), 50);
        var b = Draw(new DeterministicRandomSource(-1L, 1).GetStream("guests"), 50);

        Assert.Equal(a, b);
        Assert.Equal(-1L, new DeterministicRandomSource(-1L, 1).Seed);
    }

    [Fact]
    public void Stream_names_are_case_sensitive_and_ordinal()
    {
        var lower = Draw(new DeterministicRandomSource(Seed, 1).GetStream("guests"), 20);
        var upper = Draw(new DeterministicRandomSource(Seed, 1).GetStream("Guests"), 20);

        Assert.NotEqual(lower, upper);
    }

    // ── Guards ───────────────────────────────────────────────────────────

    [Fact]
    public void GetStream_rejects_null_name()
    {
        var src = new DeterministicRandomSource(Seed, 1);
        Assert.Throws<ArgumentNullException>(() => src.GetStream(null!));
    }

    [Fact]
    public void NextInt_covers_the_whole_range_including_the_endpoints()
    {
        var r = new DeterministicRandomSource(Seed, 1).GetStream("guests");
        var seen = new bool[4];
        for (var i = 0; i < 1_000; i++) seen[r.NextInt(4)] = true;

        Assert.All(seen, hit => Assert.True(hit));
    }

    [Fact]
    public void NextInt_handles_the_full_int_range()
    {
        var r = new DeterministicRandomSource(Seed, 1).GetStream("guests");
        for (var i = 0; i < 1_000; i++) Assert.InRange(r.NextInt(int.MaxValue), 0, int.MaxValue - 1);
    }

    private static double[] Draw(IRandom r, int count)
        => Enumerable.Range(0, count).Select(_ => r.NextDouble()).ToArray();
}
