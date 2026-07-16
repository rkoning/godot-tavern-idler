using System;
using System.Collections.Generic;
using System.Linq;
using TavernIdler.Domains.Economy;
using TavernIdler.Domains.Guests;    // TransactionResult, NightGuestStats
using TavernIdler.Domains.Structure; // BuildCostKind, ChargeResult (CON-004)
using TavernIdler.Domains.Cycle;     // Phase
using TavernIdler.Kernel;

namespace TavernIdler.Tests.Contracts.Economy;

/// <summary>
/// CON-007 (Economy API v1.0) abstract behavioral conformance suite. Covers every bullet of the
/// contract's "Conformance tests" section: the pricing table (satisfaction × spending, half-away
/// rounding, wallet cap), stock (restock multiplier, decrement, last-unit <see cref="StockDepleted"/>,
/// zero-stock <see cref="TransactionResult.SoldOut"/>, carryover), the four settlement golden scenarios
/// (solvent / upkeep-shortfall / partial wages / arrears seniority), <c>PayBackPay</c>, every
/// <see cref="NightReport"/> field, double-settlement / phase-gate guards, <c>ResetGold</c>, and the
/// snapshot round-trip.
///
/// ABSTRACT — xUnit never instantiates it, so nothing runs until the Economy domain ticket (TKT-015)
/// supplies a concrete subclass over the real ledger via <see cref="CreateSut"/>. This ticket (TKT-007)
/// only defines the suite + the frozen port types it targets. Settlement is deterministic integer
/// arithmetic, so assertions are exact.
/// </summary>
public abstract class EconomyConformanceTests
{
    /// Build the SUT over the real economy domain for <paramref name="world"/>, starting in a fresh
    /// pre-run state (no settlement yet, gold = StartingGold).
    protected abstract IEconomyTestHarness CreateSut(EconomyWorld world);

    // ════════════════════════════════════════════════════════════
    //  Helpers
    // ════════════════════════════════════════════════════════════

    private static int UnitsOf(IEconomyQueries q, string item) =>
        q.Stock.FirstOrDefault(s => s.Item == new MenuItemId(item))?.Units ?? 0;

    private static Money TotalArrears(IEconomyQueries q) =>
        q.Arrears.Aggregate(Money.Zero, (acc, a) => acc + a.Owed);

    private static SettlementResult Settle(IEconomyTestHarness h, int night, SettlementInput input)
    {
        h.EnterPhase(Phase.Settlement, night);
        return h.Commands.RunSettlement(input);
    }

    /// A SUT with `units` of every menu item stocked (in Prep), left in Service ready to transact.
    private IEconomyTestHarness ServiceSut(EconomyWorld world, int units = 100)
    {
        var h = CreateSut(world);
        h.EnterPhase(Phase.Prep);
        foreach (var it in world.Menu)
            Assert.IsType<Outcome<EconomyError>.Success>(h.Commands.PurchaseStock(it.Id, units));
        h.EnterPhase(Phase.Service);
        return h;
    }

    // ════════════════════════════════════════════════════════════
    //  Pricing table (REQ-023 / CON-007 "Pricing")
    // ════════════════════════════════════════════════════════════

    public static IEnumerable<object[]> PricingCases()
    {
        // salePrice, satisfaction, spending, expected paid (= price.MultiplyRounded(sat × spend), half-away)
        yield return new object[] { 7L, 0.5, 1.0, 4L };    // 3.5  → 4
        yield return new object[] { 7L, 1.0, 1.0, 7L };    // 7
        yield return new object[] { 7L, 1.5, 1.0, 11L };   // 10.5 → 11
        yield return new object[] { 7L, 0.5, 2.0, 7L };    // ×1.0 = 7
        yield return new object[] { 5L, 1.5, 2.0, 15L };   // ×3.0 = 15
        yield return new object[] { 3L, 0.5, 1.0, 2L };    // 1.5  → 2
        yield return new object[] { 9L, 1.5, 2.0, 27L };   // ×3.0 = 27
    }

    [Theory]
    [MemberData(nameof(PricingCases))]
    public void Menu_purchase_prices_by_satisfaction_times_spending_rounded_half_away(
        long salePrice, double sat, double spend, long expectedPaid)
    {
        var h = ServiceSut(Econ.World(1_000_000, menu: Econ.Item("ale", salePrice, 1)));
        var before = h.Queries.Gold;

        var result = h.Commands.ExecuteTransaction(Econ.Buy(1, "ale", wallet: 1_000_000, sat: sat, spend: spend));

        var paid = Assert.IsType<TransactionResult.Completed>(result).Paid;
        Assert.Equal(new Money(expectedPaid), paid);
        Assert.Equal(new Money(salePrice).MultiplyRounded(sat * spend), paid);
        Assert.Equal(before + paid, h.Queries.Gold);   // ledger credited exactly Paid (REQ-004)
    }

    [Fact]
    public void Transaction_priced_above_wallet_returns_cannot_afford_and_charges_nothing()
    {
        var h = ServiceSut(Econ.World(1_000_000, menu: Econ.Item("wine", 100, 1)));
        var before = h.Queries.Gold;
        var stockBefore = UnitsOf(h.Queries, "wine");

        var result = h.Commands.ExecuteTransaction(Econ.Buy(1, "wine", wallet: 50));   // 100 > 50

        Assert.IsType<TransactionResult.CannotAfford>(result);
        Assert.Equal(before, h.Queries.Gold);
        Assert.Equal(stockBefore, UnitsOf(h.Queries, "wine"));
    }

    [Fact]
    public void Completed_transaction_emits_transaction_executed_with_the_paid_amount()
    {
        var h = ServiceSut(Econ.World(1_000_000, menu: Econ.Item("ale", 8, 1)));
        h.DrainEvents();   // clear anything from setup

        var paid = Assert.IsType<TransactionResult.Completed>(
            h.Commands.ExecuteTransaction(Econ.Buy(5, "ale", wallet: 1000))).Paid;

        var ev = Assert.Single(h.DrainEvents().OfType<TransactionExecuted>());
        Assert.Equal(new GuestId(5), ev.Guest);
        Assert.Equal(TransactionKind.MenuPurchase, ev.Kind);
        Assert.Equal(paid, ev.Paid);
    }

    [Fact]
    public void Menu_purchase_of_an_unlisted_item_is_not_offered()
    {
        var h = ServiceSut(Econ.World(1_000_000, menu: Econ.Item("ale", 4, 1)));
        Assert.IsType<TransactionResult.NotOffered>(h.Commands.ExecuteTransaction(Econ.Buy(1, "ghost", wallet: 1000)));
    }

    [Fact]
    public void Execute_transaction_outside_service_throws()
    {
        var h = CreateSut(Econ.World(1000, menu: Econ.Item("ale", 4, 1)));
        h.EnterPhase(Phase.Prep);
        Assert.Throws<InvalidOperationException>(() => h.Commands.ExecuteTransaction(Econ.Buy(1, "ale", wallet: 1000)));
    }

    // ════════════════════════════════════════════════════════════
    //  Stock (REQ-025 / REQ-026 / REQ-030 / REQ-087)
    // ════════════════════════════════════════════════════════════

    [Fact]
    public void Purchase_stock_applies_the_restock_multiplier_debits_and_adds_units()
    {
        // restockCost 10, restockMult 1.5, 3 units ⇒ cost = 10.MultiplyRounded(1.5) × 3 = 15 × 3 = 45.
        var world = Econ.World(1000, restockMult: 1.5, menu: Econ.Item("ale", 4, restockCost: 10));
        var h = CreateSut(world);
        h.EnterPhase(Phase.Prep);

        var outcome = h.Commands.PurchaseStock(new MenuItemId("ale"), 3);
        var ok = Assert.IsType<Outcome<EconomyError>.Success>(outcome);

        Assert.Equal(new Money(955), h.Queries.Gold);          // 1000 - 45
        var line = h.Queries.Stock.Single(s => s.Item == new MenuItemId("ale"));
        Assert.Equal(3, line.Units);
        Assert.Equal(new Money(4), line.SalePrice);
        Assert.Equal(new Money(15), line.RestockCost);         // post-multiplier per-unit restock
        Assert.Contains(new MenuItemId("ale"), h.Queries.StockedItems);
        Assert.Contains(ok.Events, e => e is StockPurchased sp
            && sp.Item == new MenuItemId("ale") && sp.Units == 3 && sp.Cost == new Money(45));
    }

    [Fact]
    public void Purchase_stock_outside_prep_fails_wrong_phase()
    {
        var h = CreateSut(Econ.World(1000, menu: Econ.Item("ale", 4, 1)));
        h.EnterPhase(Phase.Service);
        var outcome = h.Commands.PurchaseStock(new MenuItemId("ale"), 1);
        Assert.Equal(EconomyError.WrongPhase, Assert.IsType<Outcome<EconomyError>.Failure>(outcome).Error);
    }

    [Fact]
    public void Purchase_stock_of_an_unknown_item_fails_unknown_menu_item()
    {
        var h = CreateSut(Econ.World(1000, menu: Econ.Item("ale", 4, 1)));
        h.EnterPhase(Phase.Prep);
        var outcome = h.Commands.PurchaseStock(new MenuItemId("ghost"), 1);
        Assert.Equal(EconomyError.UnknownMenuItem, Assert.IsType<Outcome<EconomyError>.Failure>(outcome).Error);
    }

    [Fact]
    public void Purchase_stock_with_insufficient_gold_fails_and_changes_nothing()
    {
        var h = CreateSut(Econ.World(5, menu: Econ.Item("ale", 4, restockCost: 10)));   // 10 > 5
        h.EnterPhase(Phase.Prep);
        var outcome = h.Commands.PurchaseStock(new MenuItemId("ale"), 1);
        Assert.Equal(EconomyError.InsufficientGold, Assert.IsType<Outcome<EconomyError>.Failure>(outcome).Error);
        Assert.Equal(new Money(5), h.Queries.Gold);    // Outcome.Failure mutates nothing (CON-001)
        Assert.Equal(0, UnitsOf(h.Queries, "ale"));
    }

    [Fact]
    public void Selling_a_unit_decrements_stock_and_only_the_last_unit_emits_stock_depleted()
    {
        var h = CreateSut(Econ.World(1_000_000, menu: Econ.Item("ale", 4, restockCost: 1)));
        h.EnterPhase(Phase.Prep);
        h.Commands.PurchaseStock(new MenuItemId("ale"), 2);
        h.EnterPhase(Phase.Service);
        h.DrainEvents();

        Assert.IsType<TransactionResult.Completed>(h.Commands.ExecuteTransaction(Econ.Buy(1, "ale", wallet: 1000)));
        Assert.DoesNotContain(h.DrainEvents(), e => e is StockDepleted);   // 2 → 1
        Assert.Equal(1, UnitsOf(h.Queries, "ale"));

        Assert.IsType<TransactionResult.Completed>(h.Commands.ExecuteTransaction(Econ.Buy(2, "ale", wallet: 1000)));
        Assert.Contains(h.DrainEvents(), e => e is StockDepleted d && d.Item == new MenuItemId("ale"));   // 1 → 0
        Assert.Equal(0, UnitsOf(h.Queries, "ale"));
        Assert.DoesNotContain(new MenuItemId("ale"), h.Queries.StockedItems);
    }

    [Fact]
    public void Selling_with_zero_stock_returns_sold_out_and_charges_nothing()
    {
        var h = CreateSut(Econ.World(1_000_000, menu: Econ.Item("ale", 4, 1)));
        h.EnterPhase(Phase.Service);   // never stocked ⇒ 0 units
        var before = h.Queries.Gold;
        Assert.IsType<TransactionResult.SoldOut>(h.Commands.ExecuteTransaction(Econ.Buy(1, "ale", wallet: 1000)));
        Assert.Equal(before, h.Queries.Gold);
    }

    [Fact]
    public void Leftover_stock_carries_over_across_settlement_into_the_next_prep()
    {
        var h = CreateSut(Econ.World(1_000_000, menu: Econ.Item("ale", 4, 1)));
        h.EnterPhase(Phase.Prep);
        h.Commands.PurchaseStock(new MenuItemId("ale"), 5);
        h.EnterPhase(Phase.Service);
        h.Commands.ExecuteTransaction(Econ.Buy(1, "ale", wallet: 1000));   // 5 → 4

        Settle(h, 1, Econ.SettleInput(upkeep: 0));
        h.EnterPhase(Phase.Prep, 2);

        Assert.Equal(4, UnitsOf(h.Queries, "ale"));   // REQ-030 carryover
    }

    // ════════════════════════════════════════════════════════════
    //  ChargeBuild + PostRefund (REQ-004 debit/credit; CON-004 target)
    // ════════════════════════════════════════════════════════════

    [Fact]
    public void Charge_build_applies_the_build_multiplier_and_debits()
    {
        var h = CreateSut(Econ.World(1000, buildMult: 1.5, menu: Econ.Item("ale", 4, 1)));
        h.EnterPhase(Phase.Prep);
        var charged = Assert.IsType<ChargeResult.Charged>(
            h.Commands.ChargeBuild(new Money(101), BuildCostKind.Room));   // 101 × 1.5 = 151.5 → 152
        Assert.Equal(new Money(152), charged.AmountCharged);
        Assert.Equal(new Money(848), h.Queries.Gold);
    }

    [Fact]
    public void Charge_build_with_insufficient_gold_charges_nothing()
    {
        var h = CreateSut(Econ.World(100, buildMult: 2.0, menu: Econ.Item("ale", 4, 1)));
        h.EnterPhase(Phase.Prep);
        var insufficient = Assert.IsType<ChargeResult.InsufficientGold>(
            h.Commands.ChargeBuild(new Money(100), BuildCostKind.Room));   // needs 200
        Assert.Equal(new Money(200), insufficient.Required);
        Assert.Equal(new Money(100), insufficient.Available);
        Assert.Equal(new Money(100), h.Queries.Gold);
    }

    [Fact]
    public void Post_refund_credits_the_ledger()
    {
        var h = CreateSut(Econ.World(100, menu: Econ.Item("ale", 4, 1)));
        h.EnterPhase(Phase.Prep);
        h.Commands.PostRefund(new Money(250));
        Assert.Equal(new Money(350), h.Queries.Gold);
    }

    // ════════════════════════════════════════════════════════════
    //  Settlement golden scenarios (REQ-019–021 / REQ-106 / SYS004-Q1)
    // ════════════════════════════════════════════════════════════

    [Fact]
    public void Solvent_settlement_pays_upkeep_and_all_wages_in_full()
    {
        var h = CreateSut(Econ.World(1000, menu: Econ.Item("ale", 4, 1)));
        var s = Settle(h, 1, Econ.SettleInput(upkeep: 100,
            wages: new[] { Econ.Wage(1, "bartender", 200), Econ.Wage(2, "barmaid", 150) }));
        var r = s.Report;

        Assert.Equal(new Money(100), r.UpkeepPaid);
        Assert.Equal(new Money(350), r.WagesPaid);
        Assert.Equal(Money.Zero, r.WagesUnpaid);
        Assert.Equal(new Money(550), r.ClosingBalance);        // 1000 - 100 - 350
        Assert.Equal(new Money(550), h.Queries.Gold);
        Assert.Empty(h.Queries.Arrears);
        Assert.DoesNotContain(s.Events, e => e is InsolvencyDeclared);
        Assert.IsType<SettlementComputed>(s.Events[^1]);       // emitted last
        Assert.Equal(1, ((SettlementComputed)s.Events[^1]).NightNumber);
    }

    [Fact]
    public void When_gold_is_below_upkeep_it_floors_at_zero_forgives_the_remainder_and_leaves_all_wages_unpaid()
    {
        var h = CreateSut(Econ.World(30, menu: Econ.Item("ale", 4, 1)));
        var s = Settle(h, 1, Econ.SettleInput(upkeep: 100,
            wages: new[] { Econ.Wage(1, "a", 50), Econ.Wage(2, "b", 20) }));
        var r = s.Report;

        Assert.Equal(new Money(30), r.UpkeepPaid);             // min(100, 30); 70 forgiven (SYS004-Q1)
        Assert.Equal(Money.Zero, r.WagesPaid);
        Assert.Equal(new Money(70), r.WagesUnpaid);
        Assert.Equal(Money.Zero, r.ClosingBalance);            // floored at 0
        Assert.Equal(Money.Zero, h.Queries.Gold);

        var insolvency = Assert.Single(s.Events.OfType<InsolvencyDeclared>());
        Assert.Equal(new[] { new EmployeeId(1), new EmployeeId(2) },
            insolvency.Unpaid.OrderBy(e => e.Value).ToArray());
        Assert.Equal(new Money(70), TotalArrears(h.Queries));
    }

    [Fact]
    public void Partial_wage_coverage_pays_earlier_list_entries_in_full_and_leaves_later_ones_unpaid()
    {
        // 200 gold, upkeep 80 ⇒ 120 for wages [100, 50, 30] in list order:
        //   #1 100 paid (20 left) → #2 50 unaffordable → #3 30 unaffordable.
        // (Same outcome under stop-at-first or continue interpretations, so satisfiable; and it rejects
        //  any "pay the cheapest first" reading, pinning list-order seniority.)
        var h = CreateSut(Econ.World(200, menu: Econ.Item("ale", 4, 1)));
        var s = Settle(h, 1, Econ.SettleInput(upkeep: 80,
            wages: new[] { Econ.Wage(1, "a", 100), Econ.Wage(2, "b", 50), Econ.Wage(3, "c", 30) }));
        var r = s.Report;

        Assert.Equal(new Money(80), r.UpkeepPaid);
        Assert.Equal(new Money(100), r.WagesPaid);
        Assert.Equal(new Money(80), r.WagesUnpaid);            // 50 + 30
        Assert.Equal(new Money(20), r.ClosingBalance);

        var unpaid = Assert.Single(s.Events.OfType<InsolvencyDeclared>())
            .Unpaid.OrderBy(e => e.Value).ToArray();
        Assert.Equal(new[] { new EmployeeId(2), new EmployeeId(3) }, unpaid);
        Assert.Equal(new Money(50), h.Queries.Arrears.Single(a => a.Employee == new EmployeeId(2)).Owed);
        Assert.Equal(new Money(30), h.Queries.Arrears.Single(a => a.Employee == new EmployeeId(3)).Owed);
    }

    [Fact]
    public void Prior_night_arrears_are_senior_to_current_wages_the_next_night()
    {
        var h = CreateSut(Econ.World(0, menu: Econ.Item("ale", 4, 1)));
        // Night 1: no gold ⇒ employee 1's wage 100 unpaid ⇒ arrears[1] = 100.
        Settle(h, 1, Econ.SettleInput(upkeep: 0, wages: new[] { Econ.Wage(1, "a", 100) }));
        Assert.Equal(new Money(100), TotalArrears(h.Queries));

        // Prep night 2: top up to exactly 100 (covers the arrears OR the new wage, not both).
        h.EnterPhase(Phase.Prep, 2);
        h.Commands.PostRefund(new Money(100));

        // Night 2: current wage for employee 2 (100). Arrears senior ⇒ #1 paid, #2 unpaid.
        var s = Settle(h, 2, Econ.SettleInput(upkeep: 0, wages: new[] { Econ.Wage(2, "b", 100) }));

        Assert.DoesNotContain(h.Queries.Arrears, a => a.Employee == new EmployeeId(1));   // senior arrears cleared
        Assert.Equal(new Money(100), h.Queries.Arrears.Single(a => a.Employee == new EmployeeId(2)).Owed);
        Assert.Contains(s.Events, e => e is InsolvencyDeclared d && d.Unpaid.Contains(new EmployeeId(2)));
        Assert.Equal(Money.Zero, h.Queries.Gold);
    }

    [Fact]
    public void Running_settlement_twice_in_one_settlement_phase_throws()
    {
        var h = CreateSut(Econ.World(1000, menu: Econ.Item("ale", 4, 1)));
        h.EnterPhase(Phase.Settlement, 1);
        h.Commands.RunSettlement(Econ.SettleInput(upkeep: 0));
        Assert.Throws<InvalidOperationException>(() => h.Commands.RunSettlement(Econ.SettleInput(upkeep: 0)));
    }

    // ════════════════════════════════════════════════════════════
    //  Night report — every field (REQ-022 / REQ-027)
    // ════════════════════════════════════════════════════════════

    [Fact]
    public void Night_report_captures_every_field_for_a_scripted_night()
    {
        // restockCost 0 ⇒ no Prep spend, so NetGold (= earned − upkeep − wages) also equals the
        // closing−opening delta: both give −280, pinning NetGold unambiguously.
        var world = Econ.World(1000, menu: Econ.Item("ale", 10, restockCost: 0));
        var h = CreateSut(world);

        h.EnterPhase(Phase.Prep, 3);
        Assert.IsType<Outcome<EconomyError>.Success>(h.Commands.PurchaseStock(new MenuItemId("ale"), 5)); // free, gold 1000

        h.EnterPhase(Phase.Service, 3);
        h.Commands.ExecuteTransaction(Econ.Buy(1, "ale", wallet: 1000));   // +10
        h.Commands.ExecuteTransaction(Econ.Buy(2, "ale", wallet: 1000));   // +10 ⇒ gold 1020, 3 left

        var stats = Econ.Stats(totalAdmitted: 2, turnedAway: 1,
            byType: new Dictionary<GuestTypeId, int> { [new GuestTypeId("dwarf")] = 2 },
            meanSat: 0.25, maxConcurrent: 2, notable: new[] { "A dwarf sang." });
        var s = Settle(h, 3, new SettlementInput(new Money(100),
            new[] { Econ.Wage(1, "bartender", 200) },
            new[] { Econ.Award("first-night", "First Night", 25) },
            stats));
        var r = s.Report;

        Assert.Equal(3, r.NightNumber);
        Assert.Equal(new Money(20), r.GoldEarned);            // 2 × 10
        Assert.Equal(new Money(100), r.UpkeepPaid);
        Assert.Equal(new Money(200), r.WagesPaid);
        Assert.Equal(Money.Zero, r.WagesUnpaid);
        Assert.Equal(new Money(-280), r.NetGold);            // 20 − 100 − 200
        Assert.Equal(new Money(720), r.ClosingBalance);      // 1020 − 100 − 200
        Assert.Equal(new Money(720), h.Queries.Gold);
        Assert.Equal(2, r.GuestBreakdown[new GuestTypeId("dwarf")]);
        Assert.Equal(0.25, r.MeanSatisfaction, precision: 6);
        var award = Assert.Single(r.AcclaimAwarded);
        Assert.Equal(new MilestoneId("first-night"), award.Milestone);
        Assert.Equal(25, award.Acclaim);
        Assert.Equal(3, r.LeftoverStock[new MenuItemId("ale")]);
        Assert.Equal(2, r.UnitsSold[new MenuItemId("ale")]);
        Assert.Contains("A dwarf sang.", r.NotableEvents);
    }

    [Fact]
    public void Last_report_is_null_before_any_settlement_and_equals_the_result_after()
    {
        var h = CreateSut(Econ.World(1000, menu: Econ.Item("ale", 4, 1)));
        h.EnterPhase(Phase.Prep, 1);
        Assert.Null(h.Queries.LastReport);

        var s = Settle(h, 1, Econ.SettleInput(upkeep: 50));
        Assert.NotNull(h.Queries.LastReport);
        Assert.Equal(s.Report, h.Queries.LastReport);
    }

    // ════════════════════════════════════════════════════════════
    //  PayBackPay (REQ-028)
    // ════════════════════════════════════════════════════════════

    [Fact]
    public void Pay_back_pay_clears_all_arrears_in_full_and_emits_back_pay_cleared()
    {
        var h = CreateSut(Econ.World(0, menu: Econ.Item("ale", 4, 1)));
        var s = Settle(h, 1, Econ.SettleInput(upkeep: 0,
            wages: new[] { Econ.Wage(1, "bartender", 100), Econ.Wage(2, "barmaid", 40) }));
        Assert.Contains(s.Events, e => e is InsolvencyDeclared);
        Assert.Equal(new Money(140), TotalArrears(h.Queries));

        h.EnterPhase(Phase.Prep, 2);
        h.Commands.PostRefund(new Money(140));
        var outcome = h.Commands.PayBackPay();

        var ok = Assert.IsType<Outcome<EconomyError>.Success>(outcome);
        var cleared = Assert.Single(ok.Events.OfType<BackPayCleared>());
        Assert.Equal(new[] { new EmployeeId(1), new EmployeeId(2) },
            cleared.Cleared.OrderBy(e => e.Value).ToArray());
        Assert.Empty(h.Queries.Arrears);
        Assert.Equal(Money.Zero, h.Queries.Gold);            // 140 − 140
    }

    [Fact]
    public void Pay_back_pay_with_insufficient_gold_pays_nothing_and_fails()
    {
        var h = CreateSut(Econ.World(0, menu: Econ.Item("ale", 4, 1)));
        Settle(h, 1, Econ.SettleInput(upkeep: 0, wages: new[] { Econ.Wage(1, "bartender", 100) }));
        h.EnterPhase(Phase.Prep, 2);
        h.Commands.PostRefund(new Money(50));               // < 100 owed

        var outcome = h.Commands.PayBackPay();
        Assert.Equal(EconomyError.InsufficientGold, Assert.IsType<Outcome<EconomyError>.Failure>(outcome).Error);
        Assert.Equal(new Money(50), h.Queries.Gold);        // full-or-nothing: nothing paid
        Assert.Equal(new Money(100), TotalArrears(h.Queries));
    }

    [Fact]
    public void Pay_back_pay_with_no_arrears_fails_no_arrears()
    {
        var h = CreateSut(Econ.World(1000, menu: Econ.Item("ale", 4, 1)));
        h.EnterPhase(Phase.Prep);
        var outcome = h.Commands.PayBackPay();
        Assert.Equal(EconomyError.NoArrears, Assert.IsType<Outcome<EconomyError>.Failure>(outcome).Error);
    }

    [Fact]
    public void Pay_back_pay_outside_prep_fails_wrong_phase()
    {
        var h = CreateSut(Econ.World(0, menu: Econ.Item("ale", 4, 1)));
        // Settle leaves the SUT in Settlement phase with arrears present.
        Settle(h, 1, Econ.SettleInput(upkeep: 0, wages: new[] { Econ.Wage(1, "b", 100) }));
        var outcome = h.Commands.PayBackPay();
        Assert.Equal(EconomyError.WrongPhase, Assert.IsType<Outcome<EconomyError>.Failure>(outcome).Error);
    }

    // ════════════════════════════════════════════════════════════
    //  ResetGold (REQ-037) + snapshot round-trip (CON-017 payload)
    // ════════════════════════════════════════════════════════════

    [Fact]
    public void Reset_gold_restores_starting_gold_and_clears_stock_and_arrears()
    {
        var h = CreateSut(Econ.World(500, menu: Econ.Item("ale", 4, restockCost: 1)));
        h.EnterPhase(Phase.Prep, 1);
        h.Commands.PurchaseStock(new MenuItemId("ale"), 3);                          // gold 497, stock 3
        Settle(h, 1, Econ.SettleInput(upkeep: 0, wages: new[] { Econ.Wage(1, "a", 100000) }));  // unpayable ⇒ arrears
        Assert.NotEmpty(h.Queries.Arrears);

        var events = h.Commands.ResetGold();

        Assert.Equal(new Money(500), h.Queries.Gold);
        Assert.Empty(h.Queries.StockedItems);
        Assert.Equal(0, UnitsOf(h.Queries, "ale"));
        Assert.Empty(h.Queries.Arrears);
        var reset = Assert.Single(events.OfType<GoldReset>());
        Assert.Equal(new Money(500), reset.StartingGold);
    }

    [Fact]
    public void Capture_during_service_throws()
    {
        var h = CreateSut(Econ.World(1000, menu: Econ.Item("ale", 4, 1)));
        h.EnterPhase(Phase.Service);
        Assert.Throws<InvalidOperationException>(() => h.Commands.Capture());
    }

    [Fact]
    public void Snapshot_round_trips_gold_stock_arrears_and_last_report()
    {
        var world = Econ.World(400, menu: Econ.Item("ale", 6, restockCost: 1));
        var a = CreateSut(world);
        a.EnterPhase(Phase.Prep, 1);
        a.Commands.PurchaseStock(new MenuItemId("ale"), 4);                          // gold 396, stock 4
        a.EnterPhase(Phase.Settlement, 1);
        a.Commands.RunSettlement(Econ.SettleInput(upkeep: 0, wages: new[] { Econ.Wage(9, "a", 100000) }));
        var snap = a.Commands.Capture();
        Assert.Equal(1, snap.SchemaVersion);

        var b = CreateSut(world);
        b.Commands.Restore(snap);

        Assert.Equal(a.Queries.Gold, b.Queries.Gold);
        Assert.Equal(4, UnitsOf(b.Queries, "ale"));
        Assert.Equal(new Money(100000), b.Queries.Arrears.Single(x => x.Employee == new EmployeeId(9)).Owed);
        Assert.NotNull(b.Queries.LastReport);
        Assert.Equal(a.Queries.LastReport!.NightNumber, b.Queries.LastReport!.NightNumber);
    }

    [Fact]
    public void Snapshot_preserves_a_null_last_report_when_no_settlement_has_run()
    {
        var world = Econ.World(400, menu: Econ.Item("ale", 6, 1));
        var a = CreateSut(world);
        a.EnterPhase(Phase.Prep, 1);
        var snap = a.Commands.Capture();

        var b = CreateSut(world);
        b.Commands.Restore(snap);
        Assert.Null(b.Queries.LastReport);
    }
}
