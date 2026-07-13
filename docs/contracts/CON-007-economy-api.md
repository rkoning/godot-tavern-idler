# CON-007: Economy API v1.0

> Status: FROZEN (Gate 4 PASSED 2026-07-13)
> Kind: port interface + domain events
> Provider: DOM-004 Economy
> Consumers: guests bridge (CON-006 `ITransactions`), structure bridge (CON-004 `IBuildLedger`), economy UI adapter, app orchestrator (settlement), DOM-005 (refusal routing), persistence adapter
> Conformance tests: `tests/contracts/economy/`

## Purpose

The gold ledger, stock, transactions, and settlement. Traces: REQ-004, REQ-011–015, REQ-019–022, REQ-025–028, REQ-030, REQ-105, REQ-106; SYS004-Q1 resolved (floor-at-zero, user 2026-07-13).

## Interface definition

```csharp
namespace TavernIdler.Domains.Economy;
using TavernIdler.Kernel;
using TavernIdler.Domains.Guests;      // TransactionRequest/Result (CON-006), NightGuestStats (CON-005)
using TavernIdler.Domains.Structure;   // BuildCostKind, ChargeResult (CON-004)

public enum EconomyError
{
    WrongPhase,            // stock purchase / back pay outside Prep
    UnknownMenuItem,
    InsufficientGold,
    NoArrears              // PayBackPay with nothing owed
}

public interface IEconomyCommands
{
    /// CON-006 ITransactions target. Service phase only (guests exist only then).
    TransactionResult ExecuteTransaction(TransactionRequest request);

    Outcome<EconomyError> PurchaseStock(MenuItemId item, int units);       // Prep only (REQ-025); units ≥ 1
    ChargeResult ChargeBuild(Money baseCost, BuildCostKind kind);          // CON-004 IBuildLedger target
    void PostRefund(Money amount);
    SettlementResult RunSettlement(SettlementInput input);                 // once per Settlement phase
    Outcome<EconomyError> PayBackPay();                                    // Prep (REQ-028 recovery)
    IReadOnlyList<IDomainEvent> ResetGold();                               // prestige → starting gold (REQ-037)
    EconomySnapshot Capture();
    void Restore(EconomySnapshot snapshot);
}

public sealed record SettlementInput(
    Money UpkeepBill,                              // from CON-003 NightlyUpkeepBill
    IReadOnlyList<WageLine> Wages,                 // from CON-009 (REQ-019/064)
    IReadOnlyList<MilestoneAward> Awards,          // from CON-013 (REQ-021)
    NightGuestStats GuestStats);                   // from CON-005 (REQ-022)

public sealed record WageLine(EmployeeId Employee, RoleId Role, Money Wage);
public sealed record MilestoneAward(MilestoneId Milestone, string DisplayName, long Acclaim);

public sealed record SettlementResult(NightReport Report, IReadOnlyList<IDomainEvent> Events);

public sealed record NightReport(                  // REQ-022 + REQ-027
    int NightNumber,
    Money GoldEarned, Money UpkeepPaid, Money WagesPaid, Money WagesUnpaid,
    Money NetGold, Money ClosingBalance,
    IReadOnlyDictionary<GuestTypeId, int> GuestBreakdown,
    double MeanSatisfaction,
    IReadOnlyList<MilestoneAward> AcclaimAwarded,
    IReadOnlyDictionary<MenuItemId, int> LeftoverStock,
    IReadOnlyDictionary<MenuItemId, int> UnitsSold,
    IReadOnlyList<string> NotableEvents);

public interface IEconomyQueries
{
    Money Gold { get; }
    IReadOnlyList<StockLine> Stock { get; }                    // REQ-025/030
    IReadOnlyList<MenuItemId> StockedItems { get; }            // stock > 0 (composition input)
    bool EntranceFeeActive { get; }                            // REQ-015 (perk/employee driven)
    Money EntranceFeeAmount { get; }                           // meaningful iff active
    IReadOnlyList<ArrearsLine> Arrears { get; }                // REQ-028
    NightReport? LastReport { get; }
}

public sealed record StockLine(MenuItemId Item, int Units, Money SalePrice, Money RestockCost); // post-multiplier restock
public sealed record ArrearsLine(EmployeeId Employee, Money Owed);

public sealed record EconomySnapshot(int SchemaVersion /*1*/, string JsonPayload);   // schema in CON-017

// ── Events ──────────────────────────────────────────────────
public sealed record TransactionExecuted(GuestId Guest, TransactionKind Kind, Money Paid) : IDomainEvent;
public sealed record StockDepleted(MenuItemId Item) : IDomainEvent;                       // REQ-026 trigger
public sealed record StockPurchased(MenuItemId Item, int Units, Money Cost) : IDomainEvent;
public sealed record SettlementComputed(int NightNumber) : IDomainEvent;
public sealed record InsolvencyDeclared(IReadOnlyList<EmployeeId> Unpaid) : IDomainEvent; // → CON-009 SetRefusals
public sealed record BackPayCleared(IReadOnlyList<EmployeeId> Cleared) : IDomainEvent;    // → CON-009 SetRefusals(off)
public sealed record GoldReset(Money StartingGold) : IDomainEvent;
```

## Semantics

- **Pricing:** `price = sheet.SalePrice.MultiplyRounded(SatisfactionModifier × SpendingMultiplier)` (CON-001 rounding). `price > WalletAvailable` → `CannotAfford`. `MenuPurchase` with zero stock → `SoldOut`; the sale decrementing stock to 0 additionally emits `StockDepleted` (event routing carries the penalty context; the *requesting* guest's penalty on a `SoldOut` result is DOM-003's, REQ-026). Lodging/entry/service fees come from room/employee content sheets; `EntranceFee` uses `EntranceFeeAmount`.
- **REQ-004:** ledger credits happen inside `ExecuteTransaction`/`PostRefund` only; debits inside `PurchaseStock` (restock multiplier applied per REQ-087: `cost = restockCost.MultiplyRounded(restockMult) × units`), `ChargeBuild` (CON-004 semantics), and `RunSettlement`.
- **Settlement order (REQ-106, REQ-019–021):** (1) upkeep: pay `min(UpkeepBill, Gold)`, remainder **forgiven**, gold floors at 0 (SYS004-Q1); (2) wages per `WageLine`, in list order: pay in full or not at all per employee; unpaid lines accrue to `Arrears` and are listed in `InsolvencyDeclared`; (3) awards: Acclaim totals recorded into the report (balance mutation lives in CON-013 `CommitSettlementAwards`); (4) stock tallies (REQ-027; leftover carries over per REQ-030); (5) report assembly. Emits `SettlementComputed` last. Calling twice in one Settlement phase throws `InvalidOperationException`.
- **Arrears (REQ-028):** `PayBackPay` pays *all* arrears in full or fails with `InsufficientGold` (no partial payment); success emits `BackPayCleared`. Settlement wage step also auto-includes arrears before current wages: arrears are senior.
- **Stock:** integer units ≥ 0; per-night stock count is simply current units (purchase adds; sales subtract; carryover is implicit).
- **`ResetGold`:** sets `Gold = StartingGold` (game config), clears arrears and stock, emits `GoldReset`. `Capture` legal in Prep/Settlement only (`InvalidOperationException` otherwise).
- Phase gates via injected `ICycleQueries`: `PurchaseStock`/`PayBackPay` Prep-only; `ExecuteTransaction` Service-only (violation → `WrongPhase` / `NotOffered` respectively — transactions get `NotOffered` since result type has no phase variant... **normative:** `ExecuteTransaction` outside Service throws `InvalidOperationException` (orchestrator bug, not player-reachable)).

## Conformance tests

`tests/contracts/economy/`:

- Pricing table: satisfaction 0.5/1.0/1.5 × multiplier 1.0/2.0 × odd prices — exact rounded results; wallet-cap → `CannotAfford` charges nothing.
- Stock: purchase applies restock multiplier; sale decrements; last-unit sale emits `StockDepleted`; zero-stock sale → `SoldOut`; carryover across settlement (REQ-030).
- Settlement golden scenarios: (a) solvent night — order upkeep→wages verified via ledger trace; (b) gold < upkeep — floors at 0, remainder forgiven, all wages unpaid → `InsolvencyDeclared` lists all; (c) partial wage coverage — per-employee all-or-nothing in list order; (d) arrears senior to current wages next night.
- `PayBackPay`: full-or-nothing; `NoArrears` error; `BackPayCleared` contents.
- Report fields: every `NightReport` field asserted against a scripted night.
- Double `RunSettlement` throws; phase gates enforced; `ResetGold` restores starting state and clears arrears/stock.
- Snapshot round-trip preserves gold, stock, arrears, `LastReport` nullability.

## Change history

| Version | Date | Change | Approved by | Affected tickets |
|---|---|---|---|---|
| 1.0 | 2026-07-13 | initial | user | — |
