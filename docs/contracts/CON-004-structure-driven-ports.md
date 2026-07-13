# CON-004: Structure Driven Ports v1.0

> Status: FROZEN (Gate 4 PASSED 2026-07-13)
> Kind: port interface + data schema
> Provider: DOM-001 Structure (interface owner); implementers: economy bridge (over CON-007), venue bridge (over CON-013), room content adapter
> Consumers: DOM-001 domain code (caller); bridge/content adapter tickets (implementers)
> Conformance tests: `tests/contracts/structure/driven/`

## Purpose

What Structure needs from outside: charging/refunding gold, the venue lot, and the room-type catalog — plus the room-sheet JSON schema. Traces: REQ-066, REQ-073, REQ-082–084, REQ-087, REQ-099, REQ-100, REQ-104.

## Interface definition

```csharp
namespace TavernIdler.Domains.Structure;
using TavernIdler.Kernel;

// ── Charging (implemented over CON-007) ─────────────────────
public enum BuildCostKind { Room, Upgrade, Circulation }

public abstract record ChargeResult
{
    public sealed record Charged(Money AmountCharged) : ChargeResult;   // post-multiplier
    public sealed record InsufficientGold(Money Required, Money Available) : ChargeResult;
    private ChargeResult() { }
}

public interface IBuildLedger
{
    /// Applies the venue build-cost multiplier (REQ-087) to baseCost, then debits.
    ChargeResult TryCharge(Money baseCost, BuildCostKind kind);
    /// Credits the ledger. amount ≥ 0.
    void Refund(Money amount);
}

// ── Venue lot (implemented over CON-013 IVenueData) ─────────
public interface ILotConstraints
{
    GridRect Lot { get; }                              // REQ-082: full rectangle, origin (0,0)
    CellCoord Entrance { get; }                        // REQ-084: Lot ground cell (Y == 0)
    IReadOnlyList<TerrainFeature> Terrain { get; }     // REQ-083
}

public sealed record TerrainFeature(CellCoord Cell, TerrainEffect Effect);

public abstract record TerrainEffect
{
    public sealed record EnablesRoomType(RoomTypeId Room) : TerrainEffect;   // REQ-083(a)
    public sealed record ModifiesRoom(RoomStatModifier Modifier) : TerrainEffect; // REQ-083(b)
    private TerrainEffect() { }
}

public sealed record RoomStatModifier(
    double CapacityMultiplier,      // ≥ 0, default 1.0
    double ServiceSpeedMultiplier,  // ≥ 0, default 1.0
    double UpkeepMultiplier);       // ≥ 0, default 1.0

// ── Room content (implemented by content adapter) ───────────
public interface IRoomContent
{
    /// Full catalog from content JSON, filtered to currently available types
    /// (base set + Progression unlocks + venue exclusives; adapter composes
    /// CON-013 unlock/venue state with the JSON catalog).
    IReadOnlyList<RoomTypeSheet> AvailableRoomTypes();
}

public sealed record RoomTypeSheet(
    RoomTypeId Id,
    string DisplayName,
    int MinArea, int MaxArea,                 // REQ-069 footprint range (cells)
    int OptimumArea,                          // efficiency curve (CON-003 formula)
    double EfficiencyFalloffPerCell,          // ≥ 0
    double MinEfficiency,                     // (0, 1]
    double CapacityPerCell,                   // ≥ 0
    Money BuildCost,                          // base, pre-multiplier
    Money NightlyUpkeep,
    IReadOnlyList<TierSpec> Tiers,            // index 0 = tier 1 (base); REQ-071
    IReadOnlyList<ServiceOffering> Services,  // CON-003 type; REQ-066/104
    StaffRequirements Staffing,               // CON-003 type; REQ-057
    IReadOnlyList<TraitId> Traits,            // REQ-095
    bool Broadcaster,                         // REQ-047
    RoomTypeId? RequiresTerrainFeature);      // non-null → REQ-083(a) placement rule

public sealed record TierSpec(
    Money UpgradeCost,                        // tier 1: Money.Zero
    double CapacityMultiplier,                // vs base, ≥ 1.0
    double ServiceSpeedMultiplier,            // ≥ 1.0
    IReadOnlyList<RoleRequirement> StaffingMaxOverrides); // empty = inherit
```

### Room sheet JSON schema (content file `content/rooms.json`)

```json
{ "rooms": [ {
  "id": "taproom", "displayName": "Tap Room",
  "minArea": 6, "maxArea": 24, "optimumArea": 12,
  "efficiencyFalloffPerCell": 0.04, "minEfficiency": 0.4,
  "capacityPerCell": 1.5,
  "buildCost": 200, "nightlyUpkeep": 10,
  "tiers": [
    { "upgradeCost": 0,   "capacityMultiplier": 1.0, "serviceSpeedMultiplier": 1.0, "staffingMaxOverrides": [] },
    { "upgradeCost": 500, "capacityMultiplier": 1.5, "serviceSpeedMultiplier": 1.2,
      "staffingMaxOverrides": [ { "role": "barmaid", "min": 1, "max": 5 } ] } ],
  "services": [ { "serviceId": "drink", "kind": "MenuConsumption", "baseDurationTicks": 40, "entryFee": null } ],
  "staffing": [ { "role": "bartender", "min": 1, "max": 1 }, { "role": "barmaid", "min": 1, "max": 3 } ],
  "traits": [ "rowdy-friendly" ], "broadcaster": false,
  "requiresTerrainFeature": null
} ] }
```

## Semantics

- **`IBuildLedger.TryCharge`:** atomic — either the full post-multiplier amount is debited or nothing is. Multiplier applied: `charged = baseCost.MultiplyRounded(buildMult)` (CON-001 rounding). `Refund` never fails; negative amount → `ArgumentOutOfRangeException`.
- **`ILotConstraints`:** immutable for the whole run (REQ-090); reads never change between prestiges. `Entrance.Y == 0` guaranteed. Terrain cells lie within `Lot`.
- **`IRoomContent.AvailableRoomTypes()`:** result may change between prep phases (new unlocks) but is stable within one phase; Structure re-reads it lazily per command, never caches across nights.
- **Schema validation (adapter's duty):** ids unique; `MinArea ≤ OptimumArea ≤ MaxArea`; tier 1 `upgradeCost == 0`; every `staffingMaxOverrides.role` exists in base staffing; `kind` ∈ ServiceKind names; unknown JSON fields rejected (fail-fast at load with file/line context).
- All money fields are integer gold (CON-001). Durations are tick counts ≥ 1.

## Conformance tests

`tests/contracts/structure/driven/`:

- `IBuildLedger` (run against the real economy bridge + a reference stub): charge applies `MultiplyRounded` with the active multiplier; insufficient funds leaves balance unchanged; refund credits exactly.
- `ILotConstraints` invariants: entrance on ground row inside lot; terrain within lot; values identical across repeated reads within a run.
- Content adapter: sample `rooms.json` loads to expected `RoomTypeSheet` records (golden-file test); each schema-validation rule rejects a crafted bad file with a diagnostic naming the offending field.
- Unlock filtering: catalog excludes locked special rooms until CON-013 unlock state includes them (stubbed).

## Change history

| Version | Date | Change | Approved by | Affected tickets |
|---|---|---|---|---|
| 1.0 | 2026-07-13 | initial | user | — |
