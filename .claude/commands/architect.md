---
description: "Stage 3 — coalesce requirements into DDD architecture with ports & adapters; per-domain DOM-*.md docs."
---

# /architect — Domain Architecture

You are running stage 3. Read `CLAUDE.md` and `docs/PIPELINE.md`; verify Gate 2 is PASSED. If not, stop and offer the right command.

## Goal

Turn the system docs into a domain-driven, hexagonal architecture: bounded contexts (`docs/domains/DOM-###-{name}.md`, from `templates/domain.template.md`), each with a pure-language domain model and explicit ports. Engine/framework code is confined to adapters.

## Hard architectural constraints (from CLAUDE.md)

- Business/game logic in the base programming language only. Nothing in `src/domains/` may import the UI framework or engine.
- Ports are interfaces owned by the domain: driving ports (how the outside invokes the domain) and driven ports (what the domain needs from outside — rendering, persistence, input, network, clock).
- Adapters implement ports against the chosen engine/framework and live in `src/adapters/`.

## Method

1. Read PDD + all SYS docs. Draft a domain map: proposed bounded contexts, which systems/REQs each serves, and a context diagram of relationships (upstream/downstream, shared kernel — prefer none, conformist, etc.). Present as `PROPOSAL` before creating files. A system may map to one or several domains; say why.
2. **Consequential decisions go to the user, always with ≥2 worked options.** Examples of decisions that qualify: event-driven vs finite-state-machine vs update-loop orchestration; synchronous vs async port boundaries; in-memory vs persisted aggregate state; push vs pull between domains; ECS integration strategy for game engines. For each: option, how it plays out in this project, tradeoffs, your recommendation and why. Record the user's choice in the domain doc's decision table. Never decide unilaterally, even if one option seems obviously right.
3. On approval of the map, write each DOM doc: ubiquitous language, entities/value objects/aggregates/domain events, ports table (each port will become a contract in stage 4), adapters-required table, source layout.
4. Update links both ways: SYS docs list child domains; DOM docs list parent systems and the PDD.
5. Cross-domain interactions: every arrow in the context diagram must appear in some domain's ports table. An interaction with no port is a design hole — resolve it now.

## Gate 3 check

- Every system maps to ≥1 domain; every REQ reachable via some domain's "Requirements served".
- All flagged decisions have a user-chosen answer in a decision table.
- No port references engine/framework types in its signature sketch.
- Present the audit; user passes Gate 3; update PIPELINE.md and mark DOM docs APPROVED.

Multi-session friendly: on resume, summarize the domain map state and remaining undecided items.
