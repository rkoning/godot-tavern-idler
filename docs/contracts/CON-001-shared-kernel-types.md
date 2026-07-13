# CON-001: Shared Kernel Types v1.0

> Status: FROZEN (Gate 4 PASSED 2026-07-13)
> Kind: shared type
> Provider: shared kernel (`src/domains/kernel/`)
> Consumers: DOM-001..007, app orchestrator, all adapters
> Conformance tests: `tests/contracts/kernel/`

## Purpose

The minimal shared kernel approved at stage 3 (user, 2026-07-13): identifier value types, `Money`, `Tick`, the domain-event marker, and the `Outcome` result pattern (error-model decision, user 2026-07-13). No behavior beyond arithmetic/equality. Traces: all REQs indirectly; specifically REQ-004 (`Money` exactness).

## Interface definition

```csharp
namespace TavernIdler.Kernel;

// ── Identifiers ─────────────────────────────────────────────
// Runtime entities: sequential ints, unique per save, never reused.
public readonly record struct RoomId(int Value);
public readonly record struct GuestId(int Value);
public readonly record struct EmployeeId(int Value);

// Content-defined types: non-empty, case-sensitive string keys from content JSON.
public readonly record struct RoomTypeId(string Value);
public readonly record struct GuestTypeId(string Value);
public readonly record struct RoleId(string Value);
public readonly record struct NamedHireId(string Value);
public readonly record struct MenuItemId(string Value);
public readonly record struct TraitId(string Value);
public readonly record struct RuleId(string Value);
public readonly record struct MilestoneId(string Value);
public readonly record struct ShopItemId(string Value);   // perks, special rooms, named employees
public readonly record struct AbilityId(string Value);
public readonly record struct VenueId(string Value);

// ── Money ───────────────────────────────────────────────────
// Exact integer gold (decision: long + defined rounding, user 2026-07-13).
public readonly record struct Money(long Amount) : IComparable<Money>
{
    public static readonly Money Zero = new(0);
    public static Money operator +(Money a, Money b) => new(checked(a.Amount + b.Amount));
    public static Money operator -(Money a, Money b) => new(checked(a.Amount - b.Amount));
    public static bool operator >(Money a, Money b) => a.Amount > b.Amount;
    public static bool operator <(Money a, Money b) => a.Amount < b.Amount;
    public static bool operator >=(Money a, Money b) => a.Amount >= b.Amount;
    public static bool operator <=(Money a, Money b) => a.Amount <= b.Amount;
    public int CompareTo(Money other) => Amount.CompareTo(other.Amount);

    /// Multiplies by a double factor and rounds half-away-from-zero. The ONLY
    /// sanctioned Money×double operation; all price/multiplier math uses it.
    public Money MultiplyRounded(double factor) =>
        new(checked((long)Math.Round(Amount * factor, MidpointRounding.AwayFromZero)));
}

// ── Time ────────────────────────────────────────────────────
// Absolute simulation time in ticks since run start. Durations are plain `int` tick counts.
public readonly record struct Tick(long Value) : IComparable<Tick>
{
    public static Tick operator +(Tick t, int ticks) => new(t.Value + ticks);
    public int CompareTo(Tick other) => Value.CompareTo(other.Value);
}

// ── Grid primitives ─────────────────────────────────────────
// X: column, 0-based from lot left. Y: row, 0 = ground level, increases upward.
public readonly record struct CellCoord(int X, int Y);

// Origin = bottom-left cell. Width/Height ≥ 1. Cells covered: [X, X+Width) × [Y, Y+Height).
public readonly record struct GridRect(int X, int Y, int Width, int Height)
{
    public int Area => Width * Height;
    public bool Contains(CellCoord c) =>
        c.X >= X && c.X < X + Width && c.Y >= Y && c.Y < Y + Height;
}

// ── Events & outcomes ───────────────────────────────────────
public interface IDomainEvent { }

/// Result of a mutating command. Failure mutates nothing and carries no events.
public abstract record Outcome<TError>
{
    public sealed record Success(IReadOnlyList<IDomainEvent> Events) : Outcome<TError>;
    public sealed record Failure(TError Error) : Outcome<TError>;
    private Outcome() { }
}
```

## Semantics

- **Ids:** `int`-backed ids are allocated by the owning domain, start at 1, monotonically increase, never recycle within a save. String-backed ids must be non-empty; comparison is ordinal case-sensitive. `default` (0 / null-string) id values are invalid arguments → `ArgumentException` (contract violation).
- **Money:** `Amount` may be negative only inside arithmetic intermediates; no port may return a negative balance (ledger floors at 0 per SYS004-Q1 resolution, see CON-007). Overflow throws (`checked`) — treated as a bug, not a game state.
- **Tick:** starts at 0 per run, reset only by prestige (`StartRun`). Never negative.
- **Outcome:** `Failure` guarantees the aggregate is unchanged and no events were raised — every command contract inherits this invariant. Events lists are immutable and ordered by occurrence.
- **Threading:** all kernel types are immutable and thread-safe by construction; domain access itself is single-threaded per CON-016.
- **Exceptions vs errors (global rule):** expected, player-reachable failures are `TError` variants; programmer errors (invalid ids, null args, out-of-range durations) throw standard .NET exceptions and are excluded from conformance-tested behavior except where noted.

## Conformance tests

`tests/contracts/kernel/`:

- `Money.MultiplyRounded` rounding table: ×1.0 identity; ×0.5 of odd amounts rounds away from zero; negative factors; overflow throws.
- `Money`/`Tick` arithmetic and comparison operators; `checked` overflow throws.
- `GridRect.Contains` boundary cases (edges inclusive-exclusive); `Area`.
- `Outcome.Failure` pattern-match exhaustiveness (compile-time usage sample).
- Id record equality/inequality semantics; ordinal case-sensitivity of string ids.

## Change history

| Version | Date | Change | Approved by | Affected tickets |
|---|---|---|---|---|
| 1.0 | 2026-07-13 | initial | user | — |
