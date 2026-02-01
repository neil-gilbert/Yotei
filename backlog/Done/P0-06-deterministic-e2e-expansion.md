# P0-06 Deterministic E2E expansion

## Goal
Expand deterministic E2E coverage to validate the Phase 0 feature set.

## Scope
- Add flow graph assertions and UI checks.
- Ensure diffs, summaries, and risk tags are verified.

## Technical notes
- E2E
  - Extend `e2e/run.sh` to hit `/review-sessions/{id}/flow` and assert nodes/edges.
  - Add UI check for flow container element (via curl or lightweight headless test if available).
  - Ensure fixture data produces stable risk/side-effect tags.

## Acceptance criteria
- E2E passes on clean docker-compose run.
- Tests fail if flow graph is missing.

## Tests
- Updated `e2e/run.sh` + any new UI smoke script.
