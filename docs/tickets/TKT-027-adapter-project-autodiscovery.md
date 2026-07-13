# TKT-027: Adapter project auto-discovery (build config)

> Status: DONE
> Type: build-config
> Domain: build / adapters (cross-cutting)
> Traces to: architecture convention (2026-07-13 `/requirement`: adapter tickets own their own project; no ticket edits the sln/test csproj); platform/stack table (PDD §5)
> Blocked by: TKT-017 | Blocks: TKT-019, TKT-020, TKT-021
> Session: /implement TKT-027 (2026-07-13)

## Goal

The test project auto-discovers every domain and adapter project via an MSBuild glob, so a new adapter ticket only creates its own `src/adapters/<x>/<x>.csproj` and never edits `TavernIdler.sln` or `tests/TavernIdler.Tests.csproj`. This realizes TKT-001's "no later ticket edits a csproj/sln" intent that TKT-017 had to break for lack of a discovery mechanism. CI builds the full non-Godot project graph.

## Contracts

None — build configuration only; touches no contract surface.

## File ownership (exclusive)

Ownership of these shared build files is granted to this ticket for this change (created by TKT-001 / added-to by TKT-017; both remain DONE):

```
tests/TavernIdler.Tests.csproj
TavernIdler.sln
.github/workflows/ci.yml
```

## Acceptance criteria

- [x] `tests/TavernIdler.Tests.csproj` references domain + adapter projects via glob (`..\src\domains\**\*.csproj`, `..\src\adapters\**\*.csproj`), **excluding** the Godot project (`..\src\adapters\godot\**\*.csproj`)
- [x] TKT-017's explicit `..\src\adapters\random\TavernIdler.Adapters.Random.csproj` ProjectReference is **removed** (glob covers it; no duplicate) — random conformance suite still resolves + passes, proving glob discovery of an existing adapter
- [x] A throwaway `src/adapters/_probe` project was picked up with **zero** edits beyond the one-time glob, then removed — proven via both `dotnet test` on the test project **and** `dotnet test TavernIdler.sln` (probe absent from the `.sln`, still built+run → 74) 
- [x] CI builds the whole non-Godot graph (App is a `.sln` member; adapters pulled in transitively via the Tests glob) and the suite is green; Godot excluded — no `ci.yml` change needed
- [x] Full suite still green (73 passed / 0 failed / 0 skipped, Release, via the `.sln`)
- [x] TDD (RED: probe CS0234 under explicit refs → GREEN: glob discovers it); contract-compliance = N/A contracts (report below); BOARD + status updated on start/finish

## Implementation notes

- MSBuild `ProjectReference` supports wildcard `Include`; the single `tests/TavernIdler.Tests.csproj` already globs `tests/**/*.cs`, so all adapter tests compile into it — this ticket adds only the production-project references.
- The `.sln` stays for IDE convenience (it lists the random adapter from TKT-017); it is **not** authoritative for CI, and adapter tickets do not add to it. If keeping the full graph buildable without the sln needs an MSBuild traversal project, that is this ticket's call.
- The App project has no test referencing it — ensure the glob (or a CI traversal/sln build) still builds `src/app/**/*.csproj`.

## Session log

| Date | Event |
|---|---|
| 2026-07-13 | Started (interactive). TKT-017 DONE. Plan: glob domain+adapter project refs in `Tests.csproj` (exclude Godot), drop TKT-017's explicit random ref; CI `.sln` build covers the graph transitively (Tests is a sln member). TDD via throwaway probe adapter proving net-new auto-discovery; random-adapter tests are the permanent regression. |
| 2026-07-13 | **RED** — added throwaway `src/adapters/_probe` project + `tests/adapters/_probe` test referencing it (no `Tests.csproj` edit); `dotnet build` failed `CS0234` (probe not referenced) → current explicit-ref config does not auto-discover new adapters. |
| 2026-07-13 | **GREEN** — replaced the two explicit `ProjectReference`s in `Tests.csproj` with globs (`src/domains/**`, `src/adapters/**` excl. `godot/**`). `dotnet test` → 74 passed (probe discovered; random tests still pass after dropping their explicit ref). |
| 2026-07-13 | **Verified CI path** — with probe re-added but **not** in the `.sln`, `dotnet test TavernIdler.sln` still built+ran it (74) → the solution build reaches non-member adapters via the Tests glob, so future adapter tickets need no `.sln` edit. Probe then removed; final full suite **73 passed / 0 failed / 0 skipped** (Release, `.sln`). No `.sln`/`ci.yml` edit required. |
| 2026-07-13 | **CONTRACT COMPLIANCE — TKT-027** — [PASS] 1 Ownership (changed: `tests/TavernIdler.Tests.csproj` [owned] + TKT-027/BOARD process docs; `.sln`/`ci.yml` owned but needed no edit; throwaway probe created + removed within session, no committed trace; no `tests/contracts/` touched) · [N/A] 2 Frozen-doc integrity (no `docs/contracts/`/REGISTRY edits) · [N/A] 3 Interface fidelity (implements no contract) · [PASS] 4 Conformance tests (all `tests/contracts/` suites unmodified + green; 73/0/0 via `.sln`) · [N/A] 5 Consumption fidelity (consumes no contract) · [PASS] 6 Domain purity (no `src/domains/` change) · [N/A] 7 Registry sync (not a contract ticket). **VERDICT: COMPLIANT.** Status → DONE; BOARD updated. Unblocks TKT-019/020/021 (adapters now auto-discovered). |
