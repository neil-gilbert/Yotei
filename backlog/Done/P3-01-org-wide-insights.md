# P3-01 Org-wide insights dashboard

## Goal
Provide aggregated metrics across repositories and review sessions.

## Scope
- Build API to aggregate risk tags, hot paths, and review volume.
- Add UI dashboard section.

## Technical notes
- Backend
  - Endpoint: `GET /insights/org` returning counts by risk tag, repo, and time window.
  - Optional query params: `from`, `to`, `repo`.

- Frontend
  - New page/section showing charts for risk distribution and review velocity.

## Acceptance criteria
- API returns aggregate counts for existing sessions.

## Tests
- Integration test for summary response shape.
