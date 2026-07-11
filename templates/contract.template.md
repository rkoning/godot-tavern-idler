# CON-###: {Contract Name} v{1.0}

> Status: DRAFT | APPROVED | FROZEN | SUPERSEDED by CON-### v{x}
> Kind: port interface | domain event | data schema | adapter binding | shared type
> Provider: DOM-### ({who implements it})
> Consumers: {DOM-###/TKT-### list — every consumer, kept current}
> Conformance tests: `tests/contracts/{path}`

## Purpose

What interaction this contract governs and which requirement(s) trace to it: REQ-###.

## Interface definition

The exact, code-level definition. Signatures, types, schemas — in {base language}. This section IS the contract; prose elsewhere is commentary.

```{language}
// complete interface / type / schema here
```

## Semantics

- Preconditions, postconditions, invariants per operation.
- Error behavior: every failure mode and how it is surfaced (exception type, result variant, error event).
- Ordering/threading/async guarantees, if any.
- Units, ranges, nullability for every field where not obvious from types.

## Conformance tests

What the shared test suite asserts. Implementation tickets run these; they may not modify them.

## Change history

| Version | Date | Change | Approved by | Affected tickets |
|---|---|---|---|---|
| 1.0 | {date} | initial | user | — |
