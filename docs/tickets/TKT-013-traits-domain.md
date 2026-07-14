# TKT-013: Traits domain implementation (rule engine)

> Status: DONE
> Type: implementation
> Domain: DOM-006 | System: SYS-006
> Traces to: REQ-040, REQ-041, REQ-042, REQ-043, REQ-044, REQ-045, REQ-046, REQ-047, REQ-094, REQ-095, REQ-096, REQ-110, REQ-111
> Blocked by: TKT-002, TKT-005 | Blocks: TKT-019
> Session: /implement TKT-013 (2026-07-13)

## Goal

`RuleBook`/`Codex`/`EpisodeLedger` implementing CON-011, pulling `PresenceSnapshot` via CON-012 (stubbed): episode diffing, reach + broadcaster logic, per-rule stacking, once-per-episode behavior rolls via the `"traits"` RNG stream, discovery, and the lifetime codex.

## Contracts

| Contract | Role |
|---|---|
| CON-011 | implements |
| CON-012 | consumes (stubs in tests) |
| CON-015 | consumes |
| CON-002 | consumes (Tick service-only gate) |
| CON-001 | consumes |

Consumed contracts are read-only. Implemented contracts must pass their conformance tests.

## File ownership (exclusive)

Only these paths may be created/modified by this ticket's session:

```
src/domains/traits/**   (EXCEPT src/domains/traits/ports/** — read-only, owned by the contract ticket)
tests/domains/traits/**
```

## Acceptance criteria

- [x] Episode open/close/re-entry with new EpisodeId; behavior rolls once per episode (seeded determinism)
- [x] REQ-040 guest-participation gate; broadcaster widening (REQ-047)
- [x] CountScaling formulas with caps; effect ordering Ended→Began→Triggered
- [x] Discovery exactly once ever; codex survives simulated prestige (REQ-044)
- [x] All conformance tests for implemented contracts pass (`dotnet test`, suites in `tests/contracts/`)
- [x] contract-compliance skill check passes (`.claude/skills/contract-compliance`)
- [x] Unit tests written first (superpowers **test-driven-development** skill) and passing
- [x] Ticket status + BOARD.md row updated on start and finish

## Implementation notes

Pair counting: qualifying pairs per rule per reach-scope; diff against previous tick's episode set. Keep it allocation-light — this runs every tick.

## Session log

| Date | Event |
|---|---|
| 2026-07-13 | `/implement TKT-013` started (worktree `tkt-013`). Blockers TKT-002/TKT-005 DONE. Status → IN PROGRESS; BOARD row updated. Implementing `RuleBook`/`Codex`/`EpisodeLedger`/`TraitsEngine` (CON-011 v1.1) against the abstract conformance suite from TKT-005; presence stubbed (CON-012), rolls via the `"traits"` stream (CON-015). |
| 2026-07-13 | TDD: fixtures + unit tests first (compile-RED), then the engine (GREEN). `src/domains/traits/`: `RuleBook` (+`TraitsCatalogJson` parse/validate), `Codex`, `EpisodeLedger`, `CoPresenceEvaluator`, `TraitsEngine`. Episodes key on the **qualifying pair set** (unordered distinct-carrier pairs) so count *or* membership churn reopens with a new `EpisodeId` and current `Targets`; the behavior roll is armed once per activation span (a churn reopen does not re-roll — only full closure or a night boundary does). Mutation check: keying episodes on pair *count* instead of the set fails 2 tests, confirming the v1.1 churn assertions bite. |
| 2026-07-13 | **Design note for TKT-020 (no contract/ownership change):** the CON-011 catalog schema is parsed + validated in the domain (`RuleBook.FromJson`, pure BCL text→model, no I/O). The content adapter should read `content/traits.json` and delegate here rather than reimplementing the schema rules; this session subclasses `TraitsCatalogConformanceTests` against it (TKT-020 supplies its own file-loading fixture). |
| 2026-07-13 | Interpretation recorded (no test weakened): a churn reopen is an episode open, so it emits `RuleActivated` with the new `EpisodeId` — every episode id carries exactly one activation event, and discovery still fires only on the rule's first activation ever. |
| 2026-07-13 | Full suite **127 passed / 0 failed / 0 skipped** (54 traits: 21 CON-011 API conformance + 8 catalog conformance + 25 domain unit). contract-compliance: **COMPLIANT** (report below). Status → DONE; BOARD updated. Unblocks TKT-019. |

## Contract compliance report

```
CONTRACT COMPLIANCE — TKT-013 — 2026-07-13
[PASS] 1. Ownership — changed: src/domains/traits/{RuleBook,TraitsCatalogJson,Codex,EpisodeLedger,CoPresence,TraitsEngine}.cs,
          tests/domains/traits/{TraitsEngineFixtures,TraitsEngineTests,RuleBookJsonTests}.cs, plus this ticket + its BOARD row.
          src/domains/traits/ports/** and tests/contracts/** untouched.
[PASS] 2. Frozen-doc integrity — no docs/contracts/** or REGISTRY.md edits.
[PASS] 3. Interface fidelity — CON-011: TraitsEngine implements ITraitsCommands + ITraitsQueries verbatim from
          src/domains/traits/ports/TraitsApi.cs (compiler-enforced); no port type added, renamed or widened. Additional public
          types are DOM-006 domain model (RuleBook/EffectSpec/RuleDefinition/TraitDefinition/Codex/EpisodeLedger/
          CoPresenceEvaluator/QualifyingPairs/CarrierPair/TraitsCatalogException), not contract surface.
[PASS] 4. Conformance tests — CON-011: 21 API + 8 catalog conformance tests pass via fixtures in tests/domains/traits/;
          suite files byte-identical to their pre-session state (git status clean under tests/contracts/). 0 skipped.
          CON-012's driven suite stays abstract — the presence bridge is TKT-019's to implement.
[PASS] 5. Consumption fidelity — CON-012: IPresenceSource.Current() called exactly once per Tick(), never cached across ticks,
          no re-entrancy into ITraitsCommands. CON-015: IRandomSource.GetStream("traits") once at construction; only
          NextDouble() for behavior rolls. CON-002: no cycle calls — Tick's Service-only gate is the orchestrator's (CON-016);
          the engine additionally rejects Tick outside BeginNight..EndNight. CON-001: kernel ids/events only. No error
          variants swallowed (neither consumed port defines any).
[PASS] 6. Domain purity — src/domains/traits/ imports only TavernIdler.Kernel and System.Text.Json (BCL); no Godot/engine types.
[N/A ] 7. Registry sync — implementation ticket; no contract defined or changed.
VERDICT: COMPLIANT
```
