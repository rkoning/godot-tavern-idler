# TKT-027: Adapter project auto-discovery (build config)

> Status: TODO
> Type: build-config
> Domain: build / adapters (cross-cutting)
> Traces to: architecture convention (2026-07-13 `/requirement`: adapter tickets own their own project; no ticket edits the sln/test csproj); platform/stack table (PDD §5)
> Blocked by: TKT-017 | Blocks: TKT-019, TKT-020, TKT-021
> Session: —

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

- [ ] `tests/TavernIdler.Tests.csproj` references domain + adapter projects via glob (`<ProjectReference Include="..\src\domains\**\*.csproj" />`, `..\src\adapters\**\*.csproj`), **excluding** the Godot project (`src/adapters/godot/**`, handled in TKT-022)
- [ ] TKT-017's explicit `..\src\adapters\random\TavernIdler.Adapters.Random.csproj` ProjectReference is **removed** (the glob now covers it; a duplicate ProjectReference is an MSBuild error)
- [ ] A throwaway empty `src/adapters/<probe>/<probe>.csproj` is picked up by `dotnet test` with **zero** edits to `.sln`/`Tests.csproj` (proves discovery), then removed before done
- [ ] CI builds the whole non-Godot graph (App included) and runs the suite green; Godot build stays out of this workflow
- [ ] Full suite still green (73+ tests); nothing green turns red
- [ ] TDD (superpowers **test-driven-development**); contract-compliance check (note contracts = N/A); BOARD row + status updated on start/finish

## Implementation notes

- MSBuild `ProjectReference` supports wildcard `Include`; the single `tests/TavernIdler.Tests.csproj` already globs `tests/**/*.cs`, so all adapter tests compile into it — this ticket adds only the production-project references.
- The `.sln` stays for IDE convenience (it lists the random adapter from TKT-017); it is **not** authoritative for CI, and adapter tickets do not add to it. If keeping the full graph buildable without the sln needs an MSBuild traversal project, that is this ticket's call.
- The App project has no test referencing it — ensure the glob (or a CI traversal/sln build) still builds `src/app/**/*.csproj`.

## Session log

| Date | Event |
|---|---|
