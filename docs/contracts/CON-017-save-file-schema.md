# CON-017: Save File Schema v1.0

> Status: FROZEN (Gate 4 PASSED 2026-07-13)
> Kind: data schema
> Provider: persistence adapter (owns files); payload sub-schemas owned by each domain's snapshot type
> Consumers: all domains' snapshot ports (CON-002/003/005/007/009/013), app composition root, Steam Auto-Cloud (file-level)
> Conformance tests: `tests/contracts/save/`

## Purpose

The on-disk save format: JSON envelope (decision 4, user 2026-07-13), lifetime/run scope split, versioning, and write discipline. Saves occur only at phase boundaries (Decision C). Traces: REQ-035, REQ-044 (lifetime scope), platform table (Steam Cloud).

## Interface definition

### Envelope (file `save_slot{n}.json`, UTF-8, camelCase)

```json
{
  "formatVersion": 1,
  "gameVersion": "0.1.0",
  "savedAtUtc": "2026-07-13T21:04:00Z",
  "rngSeed": 123456789,
  "lifetime": {
    "progression": { "schemaVersion": 1, "payload": { } },
    "codex":       { "schemaVersion": 1, "discovered": [ "outlaw-x-lawful" ] }
  },
  "run": {
    "cycle":       { "schemaVersion": 1, "phase": "Prep", "nightNumber": 4, "now": 48200, "runModeActive": false, "reportPending": false },
    "structure":   { "schemaVersion": 1, "payload": { } },
    "staffing":    { "schemaVersion": 1, "payload": { } },
    "economy":     { "schemaVersion": 1, "payload": { } },
    "guests":      { "schemaVersion": 1, "lodgers": [ ], "vipStates": [ ], "nextGuestIdValue": 341 },
    "progressionRun": { "schemaVersion": 1, "payload": { } }
  }
}
```

### Per-domain payloads

Each `payload` is the JSON serialization of the owning contract's snapshot record (`CycleSnapshot` CON-002, `StructureSnapshot` CON-003, `GuestsSnapshot` CON-005, `EconomySnapshot` CON-007, `StaffingSnapshot` CON-009, `ProgressionSnapshot` lifetime/run halves CON-013, `CodexSnapshot` CON-011). Domains own their payload shape; this contract owns the envelope, scope placement, and versioning rules. Snapshot records serialize with `System.Text.Json`, camelCase, enums as strings.

```csharp
namespace TavernIdler.App;

public interface ISaveStore
{
    void Save(int slot, SaveEnvelope envelope);      // atomic (see semantics)
    SaveEnvelope? Load(int slot);                    // null = no save
    IReadOnlyList<SaveSlotInfo> Slots();
}
public sealed record SaveEnvelope(int FormatVersion, string GameVersion, DateTime SavedAtUtc,
    long RngSeed, LifetimeState Lifetime, RunState Run);
public sealed record LifetimeState(string ProgressionJson, string CodexJson);
public sealed record RunState(string CycleJson, string StructureJson, string StaffingJson,
    string EconomyJson, string GuestsJson, string ProgressionRunJson);
public sealed record SaveSlotInfo(int Slot, string GameVersion, DateTime SavedAtUtc, int NightNumber);
```

## Semantics

- **Scopes:** `lifetime` survives prestige (lifetime Acclaim, earned milestones, unlocks, codex — REQ-035/044); `run` is replaced wholesale at prestige. Loading restores lifetime first, then run.
- **Write points (Decision C):** autosave on `ReportDismissed` (prep entry), after settlement completes, and after prestige (CON-016). Never mid-service; quitting mid-service loses the night by design.
- **Atomicity:** write to `save_slot{n}.json.tmp`, fsync, rename over the target. A `.tmp` present at load is discarded.
- **Location:** Godot `user://saves/` (maps to the OS user-data dir); the whole directory is the Steam Auto-Cloud sync root (Q-010 deferral: no Steamworks API involved).
- **Versioning:** `formatVersion` bump = envelope structure change (this contract). Per-payload `schemaVersion` bumps are the owning contract's change. Loader rules: unknown `formatVersion` → refuse load with user-visible message (no partial loads); `gameVersion` is informational; unknown JSON fields inside payloads → reject (strict), forcing conscious migrations.
- **Integrity:** loader validates cross-references after restore (e.g. lodger `RoomId` exists in structure; arrears `EmployeeId` in roster). A failed validation refuses the load and reports the offending path — never a silently patched state.
- `rngSeed` is the run's base seed (CON-015 night reseeding makes stream state non-persistable by design).

## Conformance tests

`tests/contracts/save/`:

- Full round-trip: play a scripted 3-night session in-memory → save → load into fresh domains → all queryable state equal (per-domain equality via snapshot comparison).
- Prestige scope test: save → prestige → save; lifetime block preserved (codex, milestones, unlocks), run block replaced.
- Atomicity: simulated crash between tmp-write and rename leaves the previous save loadable; stray `.tmp` ignored.
- Version rules: `formatVersion: 2` refused; unknown payload field refused with path in the error.
- Integrity: hand-broken cross-reference (lodger in demolished room) refuses load, names the path.
- Golden file: the sample envelope above loads (with stub payloads) and re-serializes byte-identically (stable ordering).

## Change history

| Version | Date | Change | Approved by | Affected tickets |
|---|---|---|---|---|
| 1.0 | 2026-07-13 | initial | user | — |
