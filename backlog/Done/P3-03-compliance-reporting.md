# P3-03 Compliance reporting

## Goal
Generate compliance-friendly reports of risk areas and review actions.

## Scope
- Produce a report summary with risk tags, checklist completion, and transcript highlights.

## Technical notes
- Backend
  - Endpoint: `GET /review-sessions/{id}/compliance-report`.
  - Report includes: risk tags, checklist items, summary, transcript excerpts.

- Frontend
  - Add report view + download action.

## Acceptance criteria
- Compliance report returns all required sections.

## Tests
- Integration test for report content.
