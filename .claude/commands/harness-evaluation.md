---
description: "Capture a problem with the harness itself (not the project) into HARNESS-EVALUATION.md for later iteration on the harness."
argument-hint: "what went wrong or felt wrong, optional"
---

# /harness-evaluation — Log Harness Feedback

This command is about the **harness** (the pipeline, its commands, templates, rules), never about the project being built. Output goes only to `HARNESS-EVALUATION.md` at the repo root — create it from the header below if absent. Touch no other file.

## Method

1. If `$ARGUMENTS` is empty or vague, ask what happened. Otherwise clarify only what's needed to make the entry actionable.
2. Add context you can observe yourself: which command/stage was active, what the harness instructed, what actually happened.
3. **Generalize.** Strip project specifics — the file travels between projects and back to the harness author. "The /contracts sweep has no step for contracts between two adapters" is right; naming the project's domains is wrong. If a project detail is essential to understand the issue, abstract it ("a domain with 20+ ports…").
4. Append an entry (never edit or delete previous ones):

```markdown
## HEV-### — {short title}
- **Date:**
- **Stage/command involved:**
- **Severity:** blocker | friction | idea
- **Problem:** what the harness did, allowed, or failed to prevent — generalized.
- **Expected:** what the harness should have done.
- **Proposed change:** (optional) which harness file(s) and what edit — proposal only.
- **Status:** OPEN
```

5. Confirm the entry to the user in one sentence. Do not modify the harness files themselves — changes happen in a separate iteration pass when the user hands this file back.

## File header (when creating the file)

```markdown
# Harness Evaluation Log
Feedback about the pipeline harness itself, collected during real use.
Project-agnostic by design — safe to carry between projects.
Entries are append-only; the harness author resolves them and flips Status to RESOLVED with a note.
```
