---
description: "Stage 4 — define rigid, versioned contracts for every port and cross-domain interaction."
---

# /contracts — Contract Definition

You are running stage 4. Read `CLAUDE.md` and `docs/PIPELINE.md`; verify Gate 3 is PASSED.

## Goal

Every interaction that will cross a parallel-session boundary gets a rigid contract: `docs/contracts/CON-###-{name}.md` from `templates/contract.template.md`, indexed in `docs/contracts/REGISTRY.md`. Contracts are what make parallel implementation safe — they must be complete enough that two sessions who never talk to each other produce compatible code.

## What needs a contract

- Every port in every DOM doc (driving and driven).
- Every domain event that crosses a domain boundary.
- Every shared data schema (save files, network messages, DB rows, config).
- Every shared/foundation type used by more than one domain (IDs, units, error types).
- Adapter binding conventions per engine/framework (how adapters register, lifecycle, threading).

## Rigidity requirements

A contract is not done until it has:

- The exact code-level interface in the base language — real signatures, real types, no pseudocode, no "TBD".
- Full semantics: pre/postconditions, invariants, every error mode and how it surfaces, ordering/async guarantees, units/ranges/nullability.
- A conformance test plan: what the shared test suite in `tests/contracts/` will assert. These tests are written by the contract-definition ticket and are read-only for implementers.
- Provider and complete consumer list in the header and in REGISTRY.md.

Check REGISTRY.md before creating any contract — no duplicates or overlaps. If two proposed contracts overlap, merge or re-slice and tell the user.

## Method

1. Sweep all DOM docs; build the full list of needed contracts; present it (with kind, provider, consumers) as a `PROPOSAL` before writing any.
2. Draft contracts in dependency order (shared types first). Where a design choice inside a contract is consequential (error model style, sync vs async signature, event payload granularity), give the user ≥2 options per the CLAUDE.md rule.
3. User reviews each contract (batch review is fine). On approval mark `APPROVED`; when the user passes Gate 4, flip all to `FROZEN`.
4. Update DOM docs' port tables with contract IDs.

## Change protocol (applies from freeze onward)

Frozen contracts are immutable. Change = `contract-change` ticket: state the problem, proposed new version, every affected ticket/consumer, user approval required. New version supersedes old; change history table updated; conformance tests updated by the change ticket only.

## Gate 4 check

- Every port and cross-boundary interaction in every DOM doc has a contract ID.
- No contract contains TBDs, pseudocode, or unlisted consumers.
- REGISTRY.md matches the contract files exactly.
- User passes Gate 4 → all contracts FROZEN → update PIPELINE.md.
