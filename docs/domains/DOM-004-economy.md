# DOM-004: Economy

> Status: APPROVED (Gate 3 PASSED 2026-07-13)
> Parents: [PDD](../design/PDD.md), [SYS-004 Economy & Transactions](../systems/SYS-004-economy.md)
> Contracts: [CON-007](../contracts/CON-007-economy-api.md), [CON-008](../contracts/CON-008-economy-driven-ports.md)
> Tickets: — (added in stage 5)

## Bounded context

Models gold and everything that moves it: the single ledger, the transaction catalog, menu/stock, and settlement. Authority on prices, balances, stock counts, insolvency state, and the night report's numbers.

Ubiquitous language:

- **Ledger** — the single gold balance; every change is a recorded entry (REQ-004: guest transactions are the only income).
- **Transaction** — an atomic priced exchange: food/drink, lodging stay, room entry fee, employee service, entrance fee (REQ-011–015).
- **Menu item sheet** — fixed sale price, restock cost, per-night stock count, trait list (REQ-105); prices are content data, never player-set.
- **Stock** — finite per-item count for the night; sell-out penalizes wanting guests (REQ-025/026); leftovers carry over (REQ-027/030).
- **Settlement** — end-of-night computation: upkeep first, then wages (REQ-106), supply tallies, milestone Acclaim award (REQ-021), night report (REQ-022).
- **Insolvency / back pay** — wage shortfall tracked per employee; unpaid employees refuse work until fully repaid (REQ-028).
- **Night report** — gold earned, guest breakdown, satisfaction summary, notable events, leftover stock.

Boundary: does not decide who transacts (DOM-003 drives demand), define wages (DOM-005 supplies the bill), detect milestones (DOM-007 supplies settlement awards), or own venue multipliers (DOM-007 defines; applied here).

## Requirements served

| REQ ID | Via system | How this domain serves it |
|---|---|---|
| REQ-004 | SYS-004 | Ledger entries only from executed guest transactions (+ build/refund/settlement postings) at the moment they occur |
| REQ-011 | SYS-004 | Food + drink transaction types at start |
| REQ-012 | SYS-004 | Lodging-stay transaction (occupancy tail is DOM-003's) |
| REQ-013 | SYS-004 | Room entry-fee transaction |
| REQ-014 | SYS-004 | Employee-service transaction |
| REQ-015 | SYS-004 | Entrance-fee transaction, active only when a perk/employee enables it |
| REQ-019 | SYS-004 | Wage deduction once per cycle at settlement |
| REQ-020 | SYS-004 | Supply consumption + restock cost tallies at settlement |
| REQ-021 | SYS-004 | Acclaim award executed at settlement from DOM-007's earned-milestone results |
| REQ-022 | SYS-004 | Night report assembly (guest stats supplied by DOM-003) |
| REQ-025 | SYS-004 | Prep-gated stock purchase; finite per-night counts |
| REQ-026 | SYS-004 | Sell-out outcome returned on transaction requests → satisfaction penalty applied by DOM-003 |
| REQ-027 | SYS-004 | Leftover-stock tally |
| REQ-028 | SYS-004 | Insolvency state machine (refusal until back pay cleared), surfaced to DOM-005 |
| REQ-030 | SYS-004 | Stock carryover between nights |
| REQ-105 | SYS-004 | Owns the menu item sheet schema |
| REQ-106 | SYS-004 | Settlement order: upkeep before wages; REQ-028 scoped to wage shortfall |

## Domain model

Pure C# — no engine types.

- **Aggregates:** `Ledger` (balance + entries), `Menu` (items + stock counts), `SettlementBook` (per-night accruals: consumption, upkeep due, wage bill input, awards; produces `NightReport`), `BackPayAccount` (per-employee arrears + refusal flags).
- **Value objects:** `Money` (shared kernel), `MenuItemSheet`, `TransactionRequest`/`TransactionResult` (incl. `SoldOut`), `NightReport`, `SettlementInput` (wage bill, upkeep bill, milestone awards, guest stats).
- **Domain events:** `TransactionExecuted`, `StockDepleted`, `StockPurchased`, `SettlementComputed`, `InsolvencyDeclared(employees)`, `BackPayCleared(employees)`, `GoldReset` (prestige).

Transaction pricing: `final = sheet price × satisfaction modifier × active spending multipliers`, capped by wallet (wallet enforcement is DOM-003's; the request carries affordable amount).

## Architecture decisions

Global decisions (orchestration, time, save, presentation, Steamworks, shared kernel) are recorded in [DOM-002](DOM-002-cycle.md) — user-chosen 2026-07-13. Consequences here: transactions execute synchronously inside the Guests tick via the bridge adapter; settlement runs once when DOM-002 triggers it.

| Decision | Options considered | Chosen | Rationale | Chosen by |
|---|---|---|---|---|
| — (no domain-local consequential decisions yet; SYS004-Q1 pending) | | | | |

## Ports (owned by this domain)

| Port | Direction | Purpose | Contract |
|---|---|---|---|
| `EconomyCommandsPort` | driving | `ExecuteTransaction(request)`, `PurchaseStock` (prep-gated), `ChargeBuild/PostRefund` (for DOM-001 bridge), `RunSettlement(SettlementInput)`, `SettleBackPay`, `ResetGold` (prestige), `Snapshot/Restore` | CON-007 |
| `EconomyQueriesPort` | driving | Gold balance, menu catalog + stock levels, insolvency/refusal state, last `NightReport`, composition inputs (menu-based attraction inputs) | CON-007 |
| `MenuContentPort` | driven | Menu item catalog (content data; venue-exclusive filtering input from DOM-007) | CON-008 |
| `RunCostModifierPort` | driven | Current venue's build-cost and restock-cost multipliers (over DOM-007, REQ-087) | CON-008 |

## Adapters required

| Adapter | Implements port | Binds to | Owned by ticket |
|---|---|---|---|
| Economy UI adapter | reads `EconomyQueriesPort`, calls `PurchaseStock` | Godot UI (gold display, stock screen, night report screen) | TKT-### (stage 5) |
| Menu content adapter | `MenuContentPort` | data files | TKT-### (stage 5) |
| Venue-modifier bridge | `RunCostModifierPort` | in-process call into DOM-007 queries | TKT-### (stage 5) |

(The Guests→Economy and Structure→Economy bridges are listed in DOM-003/DOM-001 as implementations of *their* driven ports; they bind to `EconomyCommandsPort`.)

## Source layout

```
src/domains/economy/        pure domain code + ports
src/adapters/economy/       adapter implementations
tests/domains/economy/      unit tests
tests/contracts/economy/    contract conformance tests
```

## Open questions

| ID | Question | Status |
|---|---|---|
| SYS004-Q1 | Gold < upkeep bill at settlement | RESOLVED (user 2026-07-13) → gold floors at 0, remainder forgiven; wage shortfall alone drives REQ-028 (CON-007 settlement step 1) |
