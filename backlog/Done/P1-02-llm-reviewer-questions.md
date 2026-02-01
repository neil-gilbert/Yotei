# P1-02 LLM reviewer questions

## Goal
Generate targeted reviewer questions (beyond heuristic checklist) using LLM output.

## Scope
- Add new model and endpoint for LLM-generated questions.
- Render in UI under “Reviewer Questions”.

## Technical notes
- Backend
  - Add `ReviewNodeQuestions` model/table (nodeId, items, createdAt, source).
  - Endpoint: `GET /review-nodes/{id}/questions`.
  - Generate on build for file nodes using LLM prompt.
  - Store source = `llm` or `heuristic` fallback.

- Frontend
  - Fetch questions for selected file node and display below checklist.

## Acceptance criteria
- Questions are persisted and returned via API.
- UI shows LLM questions for a file node.

## Tests
- Integration test: build review, request questions, verify non-empty list.
