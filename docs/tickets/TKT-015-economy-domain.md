# TKT-015: Economy domain implementation (ledger + settlement)

> Status: TODO
> Type: implementation
> Domain: DOM-004 | System: SYS-004
> Traces to: REQ-004, REQ-011, REQ-012, REQ-013, REQ-014, REQ-015, REQ-019, REQ-020, REQ-021, REQ-022, REQ-025, REQ-026, REQ-027, REQ-028, REQ-030, REQ-105, REQ-106
> Blocked by: TKT-002, TKT-007 | Blocks: TKT-019
> Session: —

## Goal

`Ledger`/`Menu`/`SettlementBook`/`BackPayAccount` implementing CON-007 against CON-008 driven ports (stubbed): transaction pricing with the sanctioned rounding, stock with carryover and sell-out events, the normative settlement order (upkeep floor-at-zero → wages all-or-nothing with arrears seniority → awards → tallies → report), insolvency state, prestige gold reset.

## Contracts

| Contract | Role |
|---|---|
| CON-007 | implements |
| CON-008 | consumes (stubs in tests) |
| CON-006 | consumes (request/result types) |
| CON-002 | consumes |
| CON-001 | consumes |

Consumed contracts are read-only. Implemented contracts must pass their conformance tests.

## File ownership (exclusive)

Only these paths may be created/modified by this ticket's session:

```
src/domains/economy/**   (EXCEPT src/domains/economy/ports/** — read-only, owned by the contract ticket)
tests/domains/economy/**
```

## Acceptance criteria

- [ ] Pricing table green incl. Money.MultiplyRounded edge cases and wallet caps
- [ ] All four settlement golden scenarios green (CON-007)
- [ ] StockDepleted on last unit; SoldOut on zero stock; carryover (REQ-030)
- [ ] PayBackPay full-or-nothing; arrears senior next settlement; double RunSettlement throws
- [ ] All conformance tests for implemented contracts pass (`dotnet test`, suites in `tests/contracts/`)
- [ ] contract-compliance skill check passes (`.claude/skills/contract-compliance`)
- [ ] Unit tests written first (superpowers **test-driven-development** skill) and passing
- [ ] Ticket status + BOARD.md row updated on start and finish

## Implementation notes

Every ledger mutation is a recorded entry — the report is derived from entries, not parallel counters where avoidable.

## Session log

| Date | Event |
|---|---|
