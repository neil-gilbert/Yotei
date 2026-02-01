# P2-03 Checklist auto-updates from conversation

## Goal
Update checklist dynamically based on new risks surfaced in conversation.

## Scope
- Add API to append checklist items with audit trail.
- Update UI to show “new from conversation”.

## Technical notes
- Backend
  - Add `ChecklistItem` model with `Source` (heuristic/llm/conversation).
  - Endpoint: `POST /review-nodes/{id}/checklist/items`.
  - Update `ReviewNodeChecklist` to store item metadata.

- Frontend
  - Render checklist grouped by source.
  - Live-update on new items (poll or websocket later).

## Acceptance criteria
- Items added via API appear in checklist and marked with source.

## Tests
- Integration test adds item and verifies API response.
