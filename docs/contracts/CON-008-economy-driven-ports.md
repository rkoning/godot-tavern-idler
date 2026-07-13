# CON-008: Economy Driven Ports v1.0

> Status: FROZEN (Gate 4 PASSED 2026-07-13)
> Kind: port interface + data schema
> Provider: DOM-004 Economy (interface owner); implementers: menu content adapter, venue-modifier bridge (over CON-013)
> Consumers: DOM-004 domain code (caller); content/bridge tickets (implementers)
> Conformance tests: `tests/contracts/economy/driven/`

## Purpose

Menu catalog input and venue cost multipliers. Traces: REQ-087, REQ-105.

## Interface definition

```csharp
namespace TavernIdler.Domains.Economy;
using TavernIdler.Kernel;

public interface IMenuContent
{
    /// Catalog filtered to currently available items (base + Progression unlocks
    /// + venue exclusives; adapter composes CON-013 state with content JSON).
    IReadOnlyList<MenuItemSheet> AvailableItems();
}

public sealed record MenuItemSheet(       // REQ-105
    MenuItemId Id,
    string DisplayName,
    MenuCategory Category,
    Money SalePrice,                      // fixed; player never sets prices
    Money RestockCost,                    // base, pre-multiplier
    IReadOnlyList<TraitId> Traits);       // REQ-095

public enum MenuCategory { Food, Drink }  // REQ-011

public interface IRunCostModifiers        // REQ-087; constant per run (REQ-090)
{
    double BuildCostMultiplier { get; }   // > 0
    double RestockCostMultiplier { get; } // > 0
}
```

### Menu JSON schema (content file `content/menu.json`)

```json
{ "menuItems": [
  { "id": "ale",            "displayName": "Ale",            "category": "Drink",
    "salePrice": 4,  "restockCost": 1, "traits": [ "humble", "alcoholic" ] },
  { "id": "roast-pheasant", "displayName": "Roast Pheasant", "category": "Food",
    "salePrice": 25, "restockCost": 10, "traits": [ "refined" ] }
] }
```

## Semantics

- **`AvailableItems()`:** stable within a phase; re-read by DOM-004 at each Prep start. Items removed by a venue change never invalidate held stock (stock lines persist; unavailable items simply can't be restocked).
- **`IRunCostModifiers`:** values fixed for the whole run; wages and starting gold are never venue-modified (REQ-087) — no such knobs exist here by design.
- **Schema validation (adapter's duty):** unique ids; `salePrice ≥ 0`, `restockCost ≥ 0` integers; `category` ∈ {Food, Drink}; traits must exist in the trait registry (CON-011 catalog) — cross-file check at load; unknown fields rejected fail-fast.

## Conformance tests

`tests/contracts/economy/driven/`:

- Golden-file load of sample `menu.json`; every validation rule rejects a crafted bad file naming the offending field (incl. cross-file unknown trait).
- Unlock/venue filtering with stubbed CON-013 state.
- `IRunCostModifiers` bridge returns the current venue sheet's multipliers and is constant across a run (stub prestige changes it).

## Change history

| Version | Date | Change | Approved by | Affected tickets |
|---|---|---|---|---|
| 1.0 | 2026-07-13 | initial | user | — |
