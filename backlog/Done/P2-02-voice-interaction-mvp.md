# P2-02 Voice interaction MVP

## Goal
Enable push-to-talk voice review scoped to a selected node.

## Scope
- UI microphone control + transcript rendering.
- Backend endpoints for audio upload and transcript storage.

## Technical notes
- Backend
  - Add `ReviewTranscript` model/table (sessionId, nodeId, question, answer, createdAt).
  - Endpoints: `POST /review-nodes/{id}/voice-query` and `GET /review-sessions/{id}/transcript`.
  - Use OpenAI speech-to-text and text-to-speech (or stub) with scoping to selected node context.

- Frontend
  - Push-to-talk button, recording indicator, transcript panel.
  - Display responses in right panel.

## Acceptance criteria
- A voice query produces a stored transcript entry.
- Transcript is retrievable per review session.

## Tests
- Integration test for transcript persistence.
