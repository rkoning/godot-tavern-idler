# SYS-004: Economy & Transactions

> Status: APPROVED (Gate 2 PASSED 2026-07-13)
> Parent: [PDD](../design/PDD.md)
> Children: [DOM-004 Economy](../domains/DOM-004-economy.md)

## Purpose

Owns gold and everything that moves it: the transaction catalog (food/drink, lodging, room entry fees, employee services, entrance fees), menu and stock (purchase, finite per-night counts, sell-outs, carryover), and settlement (wage deduction, supply tallies, Acclaim award timing, the night report, insolvency).

**Boundary (what it does NOT do)** — `DECIDED (user)` 2026-07-13: does not decide which guests transact or when (SYS-003 drives demand); does not define wage amounts per role (SYS-005 defines them; this system deducts); does not compute which milestones were earned (SYS-007 computes; this system awards the Acclaim at settlement per REQ-021); does not own venue cost multipliers (SYS-008 defines; applied here).

## Requirements owned

Copied by reference, not duplicated — the PDD row is canonical.

| REQ ID | Summary | Notes for this system |
|---|---|---|
| REQ-004 | Gold only from guest transactions, transferred at the moment they occur | Ledger rule |
| REQ-011 | Starting transaction types: food + drink purchases | |
| REQ-012 | Lodging rooms enable paid overnight stays | Room built via SYS-001 |
| REQ-013 | Room types may define per-guest entry fees | |
| REQ-014 | Employee types may offer paid services | Employee via SYS-005 |
| REQ-015 | Main-area entry free by default; perks/employees can add entrance fee | Unlock via SYS-007 |
| REQ-019 | Wages deducted at settlement, once per cycle | |
| REQ-020 | Supply consumption + restock costs tallied at settlement | |
| REQ-021 | Acclaim computed and awarded at settlement, not mid-night | Milestone detection in SYS-007 |
| REQ-022 | Night report: gold, guest breakdown, satisfaction summary, notable events | |
| REQ-025 | Stock purchased in prep; finite per-night count per item | |
| REQ-026 | Sell-out → satisfaction penalty for wanting guest | Penalty applied on SYS-003 guest |
| REQ-027 | Leftover stock tallied at settlement | |
| REQ-028 | Insolvency: unpaid employees refuse work until back pay paid in full | State surfaced to SYS-005 |
| REQ-030 | Leftover stock carries over between nights | |
| REQ-105 | Menu item sheet: fixed sale price, restock cost, stock count, traits | New 2026-07-13 (breakdown); owns the schema |
| REQ-106 | Upkeep deducted before wages; REQ-028 covers wage shortfall only | New 2026-07-13 (breakdown) |

## Interactions with other systems

`DECIDED (user)` 2026-07-13.

| Other system | Direction | Nature of interaction | Contract (stage 4) |
|---|---|---|---|
| SYS-001 Construction | both | Build/upgrade costs, demolish refunds, upkeep charged; lodging/entry-fee room data | — |
| SYS-002 Night Cycle | both | Settlement runs when triggered; completion signal; prep gates stock purchase | — |
| SYS-003 Guest Simulation | both | Executes guest transactions, debits wallets, applies satisfaction modifier to price | — |
| SYS-005 Staffing | both | Wage bill per role/hire; insolvency → refusal state | — |
| SYS-006 Traits & Synergy | in | Spending multipliers on involved guests' transactions; menu item traits | — |
| SYS-007 Acclaim & Prestige | both | Awards milestone Acclaim at settlement; gold reset at prestige | — |
| SYS-008 Venues | in | Build-cost and restock-cost multipliers | — |

## System-specific detail

- **Settlement order (REQ-106):** upkeep first, then wages; wage shortfall triggers REQ-028 refusal. Stock restock is player-initiated during prep and can never overdraw.
- **Pricing (REQ-105):** all sale prices are content data; the player's economic levers are what to build, stock, and staff — never price setting.
- **Lodging revenue (REQ-012 + REQ-107):** stay fee is a normal transaction at purchase; the occupancy tail into the next cycle is SYS-003's concern.

## Open questions

| ID | Question | Status |
|---|---|---|
| SYS004-Q1 | If gold at settlement is less than the upkeep bill itself, what happens to the remainder? | RESOLVED (user 2026-07-13, /contracts) → floor at 0, remainder forgiven (CON-007) |

## Decision log

| Date | Decision | Chosen by |
|---|---|---|
| 2026-07-12 | REQ assignment per approved 8-system partition | user |
| 2026-07-13 | Fixed per-item prices (REQ-105) | user |
| 2026-07-13 | Upkeep priority over wages (REQ-106) | user |
| 2026-07-13 | Lodgers pay + persist (REQ-107, assigned SYS-003) | user |
| 2026-07-13 | Boundary statement + interactions