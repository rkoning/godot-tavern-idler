# TKT-001: Repo scaffold + Kernel & Random contracts

> Status: DONE
> Type: contract-definition
> Domain: shared kernel (cross-domain)
> Traces to: REQ-004 (Money exactness); platform/stack table (PDD §5)
> Blocked by: — | Blocks: TKT-002, TKT-003, TKT-005, TKT-017
> Session: 2026-07-13 /implement TKT-001

## Goal

The C# solution exists and builds in CI, and the shared kernel (CON-001) plus `IRandomSource`/`IRandom` (CON-015) exist in code with their conformance suites. Every other ticket builds on this. Projects are SDK-style with default globs so **no later ticket ever edits a csproj/sln**.

## Contracts

| Contract | Role |
|---|---|
| CON-001 | implements (types are the implementation) |
| CON-015 | defines interfaces + conformance suite (adapter implemented in TKT-017) |

Consumed contracts are read-only. Implemented contracts must pass their conformance tests.

## File ownership (exclusive)

Only these paths may be created/modified by this ticket's session:

```
TavernIdler.sln
src/domains/TavernIdler.Domains.csproj
src/app/TavernIdler.App.csproj
tests/TavernIdler.Tests.csproj
src/domains/kernel/**
tests/contracts/kernel/**
tests/contracts/random/**
.github/workflows/ci.yml
.editorconfig
```

## Acceptance criteria

- [x] All CON-001 types compile exactly as written in the contract's code block (namespace `TavernIdler.Kernel`) — `src/domains/kernel/Kernel.cs`, verbatim copy
- [x] CON-001 conformance tests implemented and passing (rounding table, overflow, GridRect, ids) — 37 tests, all pass
- [x] CON-015 abstract conformance suite written (determinism, stream independence, night reseeding, bounds) with `CreateSut()` factory — no implementation here — `tests/contracts/random/RandomSourceConformanceTests.cs` (abstract; adapter subclass in TKT-017)
- [x] GitHub Actions workflow runs `dotnet test` on push (PDD §5 CI decision); domains project targets plain .NET (no Godot references) — `.github/workflows/ci.yml`; domains csproj has no engine reference
- [x] All conformance tests for implemented contracts pass (`dotnet test`, suites in `tests/contracts/`) — CON-001: 37/37; CON-015: suite defined, execution deferred to TKT-017 (no SUT this ticket)
- [x] contract-compliance skill check passes (`.claude/skills/contract-compliance`) — COMPLIANT (report below)
- [x] Unit tests written first (superpowers **test-driven-development** skill) and passing — RED (CS0234 missing types) → GREEN observed
- [x] Ticket status + BOARD.md row updated on start and finish

## Implementation notes

Conformance-suite pattern (applies to all contract tickets): suites are abstract xUnit classes with an abstract `CreateSut()`; implementing tickets add a sealed fixture subclass in their own test directory. Suites in `tests/contracts/` are read-only for all other tickets. .NET 8 target (Godot 4.7 .NET compatible).

## Session log

| Date | Event |
|---|---|
| 2026-07-13 | Session started; status TODO→IN PROGRESS. No blockers (wave 1). Read CON-001, CON-015. Repo empty (.gitkeep only), .NET 8.0.303 available. |
| 2026-07-13 | Scaffolded `TavernIdler.sln` + 3 SDK-style net8.0 projects (Domains, App, Tests/xUnit) with default globs, `.editorconfig`, `.github/workflows/ci.yml` (restore/build/test on push + PR). Empty solution builds clean. |
| 2026-07-13 | TDD CON-001: wrote 37 conformance tests (Money rounding table + overflow, Money/Tick arithmetic & comparison, GridRect Contains/Area edges, id equality + ordinal case-sensitivity, Outcome exhaustive match) → RED (CS0234, types missing) → implemented `kernel/Kernel.cs` verbatim from contract → GREEN 37/37. |
| 2026-07-13 | CON-015: added `kernel/Random.cs` (IRandomSource/IRandom, verbatim). Wrote abstract `RandomSourceConformanceTests` with `CreateSut(seed, nightNumber)` factory covering determinism (10k), stream independence, per-night reseeding, bounds, `NextInt` guard, GetStream idempotence, loose chi-squared. Abstract → 0 runnable tests here; runs when TKT-017 subclasses. Full suite green (37/37), Release build 0 warnings. |
| 2026-07-13 | **Discovered adjacent work (not done — proposed follow-up):** repo has no `.gitignore`, so `bin/`/`obj/` show as untracked. `.gitignore` is outside this ticket's File Ownership block; flagged for the user / a micro-ticket rather than added here. |
| 2026-07-13 | contract-compliance run — see report below. |

### Contract-compliance report

```
CONTRACT COMPLIANCE — TKT-001 — 2026-07-13
[PASS] 1. Ownership — all changed source/config files inside ownership block
        (.sln, 3 csproj, src/domains/kernel/**, tests/contracts/{kernel,random}/**,
        .github/workflows/ci.yml, .editorconfig). bin/obj are build artifacts, not source.
        No files outside the block were created (.gitignore deliberately NOT added — see log).
[PASS] 2. Frozen-doc integrity — no CON-*.md or REGISTRY.md rows edited this session
        (CON-001/CON-015 only Read; REGISTRY untouched by this session).
[PASS] 3. Interface fidelity —
        CON-001: kernel/Kernel.cs is a byte-for-byte copy of the contract Interface Definition
                 (all ids, Money, Tick, CellCoord, GridRect, IDomainEvent, Outcome<TError>);
                 no extra public surface.
        CON-015: kernel/Random.cs matches IRandomSource (GetStream, Seed) and IRandom
                 (NextDouble, NextInt) exactly.
[PASS] 4. Conformance tests —
        CON-001: tests/contracts/kernel — 37 passed, 0 skipped.
        CON-015: tests/contracts/random — abstract suite defined; execution deferred to
                 TKT-017 per ticket (adapter implemented there). Nothing skipped incorrectly.
[PASS] 5. Consumption fidelity — ticket consumes no external contract; kernel/App reference
        nothing beyond BCL. N/A → PASS.
[PASS] 6. Domain purity — grep of src/domains for Godot/UnityEngine imports: none.
        Domains project has no engine reference; implicit usings (System.*) only.
[PASS] 7. Registry sync — REGISTRY rows for CON-001 & CON-015 present, v1.0, FROZEN, with
        conformance paths that now exist (tests/contracts/kernel, tests/contracts/random).
        Consistent; no registry change required by this ticket.
VERDICT: COMPLIANT
```

> **2026-07-13 follow-up (TKT-027):** the "no later ticket edits a csproj/sln" goal is realized repo-wide by an MSBuild glob added to `tests/TavernIdler.Tests.csproj` in TKT-027 (which owns that change). Status here unchanged.
