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
