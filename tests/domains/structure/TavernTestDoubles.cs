using System;
using System.Collections.Generic;
using System.Linq;
using TavernIdler.Domains.Cycle;
using TavernIdler.Domains.Structure;
using TavernIdler.Kernel;

namespace TavernIdler.Tests.Domains.Structure;

/// <summary>In-memory <see cref="ICycleQueries"/>: only the phase matters to Structure.</summary>
public sealed class FakeCycleQueries : ICycleQueries
{
    public Phase Phase { get; set; } = Phase.Prep;
    public bool IsDraining => false;
    public int NightNumber => 1;
    public Tick Now => new(0);
    public int ElapsedServiceTicks => 0;
    public int RemainingServiceTicks => 0;
    public bool RunModeActive => false;
}

/// <summary>
/// Reference <see cref="IBuildLedger"/> (CON-004): holds gold and applies the venue build-cost
/// multiplier via <see cref="Money.MultiplyRounded"/>. Stands in for the economy bridge (TKT-019).
/// </summary>
public sealed class InMemoryBuildLedger : IBuildLedger
{
    private readonly double _buildMultiplier;

    public InMemoryBuildLedger(Money startingGold, double buildMultiplier = 1.0)
    {
        Balance = startingGold;
        _buildMultiplier = buildMultiplier;
    }

    public Money Balance { get; private set; }

    public ChargeResult TryCharge(Money baseCost, BuildCostKind kind)
    {
        var required = baseCost.MultiplyRounded(_buildMultiplier);
        if (required > Balance) return new ChargeResult.InsufficientGold(required, Balance);
        Balance -= required;
        return new ChargeResult.Charged(required);
    }

    public void Refund(Money amount)
    {
        if (amount < Money.Zero) throw new ArgumentOutOfRangeException(nameof(amount));
        Balance += amount;
    }
}

/// <summary>Fixed venue lot (CON-004 <see cref="ILotConstraints"/>).</summary>
public sealed class FixedLot : ILotConstraints
{
    public FixedLot(GridRect lot, CellCoord entrance, IReadOnlyList<TerrainFeature>? terrain = null)
    {
        Lot = lot;
        Entrance = entrance;
        Terrain = terrain ?? new List<TerrainFeature>();
    }

    public GridRect Lot { get; }
    public CellCoord Entrance { get; }
    public IReadOnlyList<TerrainFeature> Terrain { get; }
}

/// <summary>
/// Unlock-filtered room catalog (CON-004 <see cref="IRoomContent"/>). <see cref="Available"/> is
/// mutable so tests can prove the aggregate re-reads it per command rather than caching.
/// </summary>
public sealed class FakeRoomContent : IRoomContent
{
    private readonly IReadOnlyList<RoomTypeSheet> _fullCatalog;

    public FakeRoomContent(IReadOnlyList<RoomTypeSheet> fullCatalog, IReadOnlyList<RoomTypeId>? available)
    {
        _fullCatalog = fullCatalog;
        Available = available;
    }

    /// null = every catalog type is currently unlocked.
    public IReadOnlyList<RoomTypeId>? Available { get; set; }

    public int ReadCount { get; private set; }

    public IReadOnlyList<RoomTypeSheet> AvailableRoomTypes()
    {
        ReadCount++;
        if (Available is null) return _fullCatalog;
        var allowed = Available.ToHashSet();
        return _fullCatalog.Where(s => allowed.Contains(s.Id)).ToList();
    }
}
