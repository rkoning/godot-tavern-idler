namespace TavernIdler.Domains.Economy;
using TavernIdler.Kernel;
using TavernIdler.Domains.Guests;      // TransactionRequest/Result (CON-006), NightGuestStats (CON-005)
using TavernIdler.Domains.Structure;   // BuildCostKind, ChargeResult (CON-004)

// ── CON-007: Economy API v1.0 (FROZEN 2026-07-13) ───────────────────────────
// The gold ledger, stock, transactions, and settlement. Port interfaces +
// value/error/event types only; no domain behavior lives here. Traces: REQ-004,
// REQ-011–015, REQ-019–022, REQ-025–028, REQ-030, REQ-105, REQ-106; SYS004-Q1.

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
