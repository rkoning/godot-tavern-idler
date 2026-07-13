# SYS-007: Acclaim & Prestige

> Status: APPROVED (Gate 2 PASSED 2026-07-13)
> Parent: [PDD](../design/PDD.md)
> Children: [DOM-007 Progression](../domains/DOM-007-progression.md)

## Purpose

Owns long-term progression: milestone definitions and detection, the Acclaim pool (lifetime totals, spend-anytime, refund at prestige), the prestige reset, the single Acclaim shop (perk tree, special rooms, named employees), perk effects including active abilities, unlock persistence, and the no-fail-state guarantee.

**Boundary (what it does NOT do)** — `DECIDED (user)` 2026-07-13: does not award Acclaim mid-night (SYS-004 awards at settlement per REQ-021); does not define venues (SYS-008) though it owns the prestige-time venue choice; does not implement what unlocked content does (rooms, employees, guest types live in their systems; this system flips availability); perk effects that are whole sub-systems (e.g., hedge wizard) are gated here but designed at content design (Q-044 deferred).

## Requirements owned

Copied by reference, not duplicated — the PDD row is canonical.

| REQ ID | Summary | Notes for this system |
|---|---|---|
| REQ-029 | All Acclaim via one-time milestones (volume, VIP, synergy, build/venue feats) | |
| REQ-031 | No fail state; unlimited nights | Progression-model guarantee |
| REQ-032 | One-time milestones; lifetime Acclaim total | |
| REQ-033 | Prestige resets tavern; full pool redistributable | |
| REQ-034 | Acclaim buys perks + special rooms/employees | |
| REQ-035 | Unlocked content persists across prestiges | |
| REQ-036 | Some milestones require specific builds/venues | |
| REQ-037 | Prestige removes rooms/staff; gold to starting amount | Executed with SYS-001/005/004 |
| REQ-038 | Venue chosen at prestige from milestone-unlocked set | Venue data from SYS-008 |
| REQ-039 | Spend during any prep phase; refund only at prestige | Amended 2026-07-13 |
| REQ-076 | Perks form a prerequisite tree | |
| REQ-077 | Acclaim is pure points; gating via prerequisites + lifetime totals | |
| REQ-078 | Perk effects unrestricted (modifiers, unlocks, rule changers, abilities, sub-systems) | Hedge wizard flagship; Q-044 deferred |
| REQ-079 | One shop, one pool; prestige redistribution covers all purchases | |
| REQ-080 | Active abilities: cooldown, uses-per-night, optional resource costs | |
| REQ-112 | Milestone list visible from start; some flagged secret until earned | New 2026-07-13 (breakdown) |
| REQ-113 | Prestige any time incl. mid-service; abandoned night unsettled | New 2026-07-13 (breakdown) |

## Interactions with other systems

`DECIDED (user)` 2026-07-13.

| Other system | Direction | Nature of interaction | Contract (stage 4) |
|---|---|---|---|
| SYS-001 Construction | both | Build-feat milestone conditions in; special rooms + reset out | — |
| SYS-003 Guest Simulation | both | Guest-feat/VIP milestone signals in; lifetime Acclaim for attraction/VIP conditions out | — |
| SYS-004 Economy | both | Milestone results for settlement award; gold reset at prestige | — |
| SYS-005 Staffing | out | Named/special employee unlocks; staff reset at prestige | — |
| SYS-006 Traits & Synergy | in | Synergy-feat milestone signals; codex persistence exemption from reset | — |
| SYS-008 Venues | both | Venue unlock state; venue-only milestones; prestige venue choice | — |

## System-specific detail

- **Milestone browsing (REQ-112):** the visible list is the player's build-planning surface (per the PDD "deliberate build-crafting runs" vision); secret milestones are a content-design flag, not a separate mechanism.
- **Mid-service prestige (REQ-113):** abandoning the night skips settlement entirely — wages, tallies, and Acclaim award for that night never happen. Milestone conditions met during the abandoned night do not award (award happens only at settlement, REQ-021).
- **Amended spend window:** REQ-039 spend is prep-only (amended 2026-07-13).

## Open questions

| ID | Question | Status |
|---|---|---|

## Decision log

| Date | Decision | Chosen by |
|---|---|---|
| 2026-07-12 | REQ assignment per approved 8-system partition | user |
| 2026-07-13 | Milestones visible + hidden few (REQ-112) | user |
| 2026-07-13 | Prestige any time; mid-service abandons night (REQ-113) | user |
| 2026-07-13 |