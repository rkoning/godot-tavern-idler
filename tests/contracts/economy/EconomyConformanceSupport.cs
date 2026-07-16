using System;
using System.Collections.Generic;
using System.Linq;
using TavernIdler.Domains.Economy;
using TavernIdler.Domains.Guests;   // NightGuestStats, TransactionRequest/Result, TransactionKind (CON-005/006)
using TavernIdler.Domains.Cycle;    // Phase (CON-002)
using TavernIdler.Kernel;

namespace TavernIdler.Tests.Contracts.Economy;

// ── CON-007 behavioral harness ───────────────────────────────────────────────
// The seam the abstract EconomyConformanceTests drives. The Economy domain ticket (TKT-015) provides
// a concrete IEconomyTestHarness from CreateSut(EconomyWorld): it builds the real economy domain
// seeded with EconomyWorld.StartingGold, the menu content sheets (IMenuContent, CON-008), and the run
// cost multipliers (IRunCostModifiers, CON-008), with the phase gate wired to a settable ICycleQueries
// stub that EnterPhase drives. TKT-007 only defines the suite + the frozen port types it targets — no
// domain behavior lives here.
public interface IEconomyTestHarness
{
    IEconomyCommands Commands { get; }
    IEconomyQueries Queries { get; }

    /// Drive the injected ICycleQueries: set the current phase (and, for a Settlement report, the
    /// night number reported in NightReport.NightNumber). Leaving Settlement resets the once-per-phase
    /// RunSettlement guard, so a test can run consecutive nights:
    /// EnterPhase(Settlement, n) → RunSettlement → EnterPhase(Prep, n+1) → …
    void EnterPhase(Phase phase, int nightNumber = 1);

    /// Domain events emitted by the event-less commands — ExecuteTransaction / ChargeBuild / PostRefund
    /// — since the previous drain. This is the collection channel CON-016's tick pipeline uses (step 6,
    /// "route all collected domain events") to pick up events from commands whose return value is not an
    /// event list (ExecuteTransaction returns a CON-006 TransactionResult). Commands that DO return their
    /// own events (PurchaseStock, RunSettlement, PayBackPay, ResetGold) do not re-surface them here.
    IReadOnlyList<IDomainEvent> DrainEvents();
}

/// <summary>
/// A fully-specified, deterministic economy scenario. Settlement is pure integer arithmetic (no RNG),
/// so every assertion in the suite is an exact value. TKT-015 maps its real domain onto this world.
/// </summary>
public sealed record EconomyWorld(
    Money StartingGold,
    IReadOnlyList<MenuItemSheet> Menu,
    double BuildCostMultiplier,
    double RestockCostMultiplier);

/// Compact builders so a behavioral test states only what it exercises.
public static class Econ
{
    public static MenuItemSheet Item(
        string id, long salePrice, long restockCost = 1,
        MenuCategory category = MenuCategory.Drink, params string[] traits) =>
        new(new MenuItemId(id), id, category, new Money(salePrice), new Money(restockCost),
            traits.Select(t => new TraitId(t)).ToArray());

    public static EconomyWorld World(
        long startingGold, double buildMult = 1.0, double restockMult = 1.0, params MenuItemSheet[] menu) =>
        new(new Money(startingGold),
            menu.Length > 0 ? menu : new[] { Item("ale", 4, 1) },
            buildMult, restockMult);

    public static TransactionRequest Buy(
        int guest, string item, long wallet, double sat = 1.0, double spend = 1.0) =>
        new(new GuestId(guest), TransactionKind.MenuPurchase, new MenuItemId(item), null, null,
            new Money(wallet), sat, spend);

    public static WageLine Wage(int employee, string role, long wage) =>
        new(new EmployeeId(employee), new RoleId(role), new Money(wage));

    public static MilestoneAward Award(string id, string displayName, long acclaim) =>
        new(new MilestoneId(id), displayName, acclaim);

    public static NightGuestStats Stats(
        int totalAdmitted = 0, int turnedAway = 0,
        IReadOnlyDictionary<GuestTypeId, int>? byType = null,
        double meanSat = 0.0, int maxConcurrent = 0,
        IReadOnlyList<string>? notable = null) =>
        new(totalAdmitted, turnedAway,
            byType ?? new Dictionary<GuestTypeId, int>(),
            meanSat, maxConcurrent,
            notable ?? Array.Empty<string>());

    public static SettlementInput SettleInput(
        long upkeep,
        IEnumerable<WageLine>? wages = null,
        IEnumerable<MilestoneAward>? awards = null,
        NightGuestStats? stats = null) =>
        new(new Money(upkeep),
            (wages ?? Array.Empty<WageLine>()).ToArray(),
            (awards ?? Array.Empty<MilestoneAward>()).ToArray(),
            stats ?? Stats());
}
