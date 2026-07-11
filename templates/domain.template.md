# DOM-###: {Domain Name}

> Status: DRAFT | APPROVED
> Parents: [PDD](../design/PDD.md), {SYS-*.md links}
> Contracts: {CON-*.md links, added in stage 4}
> Tickets: {TKT-*.md links, added in stage 5}

## Bounded context

What this domain models. Ubiquitous language: key terms and their exact meanings here. Where the boundary sits and why.

## Requirements served

| REQ ID | Via system | How this domain serves it |
|---|---|---|

## Domain model

Entities, value objects, aggregates, domain events. Pure {base language} — no engine/framework types anywhere in this section.

## Architecture decisions

Each consequential choice documented as: options presented, tradeoffs, user's pick.

| Decision | Options considered | Chosen | Rationale | Chosen by |
|---|---|---|---|---|
| e.g. state handling | EDA vs FSM vs polling | ... | ... | user |

## Ports (owned by this domain)

| Port | Direction | Purpose | Contract |
|---|---|---|---|
| {Name}Port | driving (API in) / driven (SPI out) | ... | CON-### (stage 4) |

## Adapters required

| Adapter | Implements port | Binds to | Owned by ticket |
|---|---|---|---|
| ... | ...Port | {engine/framework} | TKT-### (stage 5) |

## Source layout

```
src/domains/{name}/        pure domain code + ports
src/adapters/{name}/       adapter implementations
tests/domains/{name}/      unit tests
tests/contracts/{name}/    contract conformance tests
```

## Open questions

| ID | Question | Status |
|---|---|---|
