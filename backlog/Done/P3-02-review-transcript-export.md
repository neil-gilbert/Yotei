# P3-02 Review transcript export

## Goal
Export review transcripts for compliance and audit.

## Scope
- Add API to export transcript as JSON/CSV.

## Technical notes
- Backend
  - Endpoint: `GET /review-sessions/{id}/transcript/export?format=json|csv`.
  - Use stored transcript entries from Phase 2.

- Frontend
  - Add “Export Transcript” button in review panel.

## Acceptance criteria
- Export endpoints return correct formats.

## Tests
- Integration test for JSON + CSV export.
