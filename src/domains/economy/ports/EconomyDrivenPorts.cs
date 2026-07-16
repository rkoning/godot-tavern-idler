namespace TavernIdler.Domains.Economy;
using TavernIdler.Kernel;

// ── CON-008: Economy Driven Ports v1.0 (FROZEN 2026-07-13) ──────────────────
// Menu catalog input and venue cost multipliers. Port interfaces + data schema
// only. Traces: REQ-087, REQ-105.

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
