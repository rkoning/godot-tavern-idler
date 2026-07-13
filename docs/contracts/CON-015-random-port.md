# CON-015: Random Port v1.0

> Status: FROZEN (Gate 4 PASSED 2026-07-13)
> Kind: port interface
> Provider: shared (interface in kernel namespace); implementer: RNG adapter
> Consumers: DOM-003 Guests (arrival draws, VIP rolls), DOM-006 Traits (behavior rolls)
> Conformance tests: `tests/contracts/random/`

## Purpose

Deterministic, seedable randomness behind a port so domain logic stays engine-free and replayable (Decisions A/B). Traces: REQ-024, REQ-050, REQ-102, REQ-110.

## Interface definition

```csharp
namespace TavernIdler.Kernel;

public interface IRandomSource
{
    /// Named, independent deterministic streams. Same (seed, name) ⇒ same sequence,
    /// regardless of what other streams consumed.
    IRandom GetStream(string name);
    long Seed { get; }
}

public interface IRandom
{
    double NextDouble();               // uniform [0.0, 1.0)
    int NextInt(int maxExclusive);     // uniform [0, maxExclusive); maxExclusive ≥ 1
}
```

Reserved stream names: `"guests"` (DOM-003), `"traits"` (DOM-006). New streams require a contract change.

## Semantics

- **Determinism:** for a fixed `Seed`, each named stream's sequence is fully determined and independent — consuming from `"guests"` never shifts `"traits"`. (Reference implementation: per-stream PRNG seeded by `hash(Seed, name)`; algorithm choice is the adapter's, but sequences must be stable across process runs and platforms — no `System.Random` platform drift; use an explicit algorithm, e.g. xoshiro256**.)
- `GetStream` returns the same instance per name (idempotent). Stream state is part of the run, **not** serialized: saves happen at phase boundaries (Decision C) and each night re-derives its seed as `hash(Seed, NightNumber)` per stream — implementers must reseed streams at `BeginService`/`BeginNight` with the night number so replays of a night are deterministic without persisting PRNG state.
- `NextInt(maxExclusive < 1)` → `ArgumentOutOfRangeException`. `NextDouble` never returns 1.0.
- Not thread-safe by contract; single-threaded use per CON-016.

## Conformance tests

`tests/contracts/random/`:

- Same seed + name ⇒ identical 10k-draw sequences across two adapter instances (and documented to hold cross-platform).
- Stream independence: interleaved consumption doesn't alter either sequence.
- Night reseeding: sequences for night N identical whether or not night N−1 consumed draws.
- `NextDouble` bounds over 100k draws; `NextInt` uniformity smoke test (chi-squared, loose bound) and bounds.
- `GetStream` idempotence.

## Change history

| Version | Date | Change | Approved by | Affected tickets |
|---|---|---|---|---|
| 1.0 | 2026-07-13 | initial | user | — |
