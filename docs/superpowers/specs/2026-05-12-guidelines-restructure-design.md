# ProjectGuidelines Restructure Design
Date: 2026-05-12

## Problem
Unity AI Assistant ignores FAILURE and AWAITING_INPUT stop signals and continues executing autonomously. Root cause: behavioral rules ("stop", "do not") are rationalized away by the AI's default "be helpful and complete tasks" mode.

## Approach
Two combined techniques:
1. **Role redefinition** — reframe the AI as a stateless protocol relay, not a problem-solver. Incompatible identities cannot coexist.
2. **Output-format constraints** — replace behavioral rules with exact output templates. Each non-SUCCESS status maps to a complete response template with `[RESPONSE ENDS HERE]` marker. No structural room for further tool calls.

## Design

### Top of file: Role block
Placed before all other content. Defines the AI's identity as a Bridge Protocol Client with no judgment or problem-solving role. This primes the AI's framing before it reads any rules.

### Output Templates block
Replaces the current "Behavior for Each Return Status" section. Each status maps to:
- `SUCCESS` → short acknowledgment + continue
- `FAILURE` → exact 3-line template + `[RESPONSE ENDS HERE]`, zero tool calls
- `AWAITING_INPUT` → exact options template + `[RESPONSE ENDS HERE]`, zero tool calls

The `[RESPONSE ENDS HERE]` marker functions as a format boundary, not a behavioral instruction. The phrase "The template IS your complete response" leaves no ambiguity.

### Rules block
Same content, reframed in protocol terminology. Removes all "you must not" language.

### API Reference + Usage Sequences
Unchanged — content is correct.

## Success Criteria
- AI stops at FAILURE and outputs exactly the template
- AI stops at AWAITING_INPUT, presents options, does not pick one
- AI does not wrap bridge calls in scripts
