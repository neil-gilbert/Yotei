# P0-05 Static graph rendering in UI

## Goal
Render the execution flow graph on the dashboard using React Flow or D3.

## Scope
- Add graph rendering library.
- Map flow API response to nodes/edges.

## Technical notes
- Frontend
  - Add dependency: `reactflow` (or D3 + custom renderer).
  - Add API call to `/review-sessions/{id}/flow`.
  - Render nodes by type (entry, file, db, queue, external, etc).
  - Provide fallback empty state if no flow.

## Acceptance criteria
- Flow graph renders for seeded fixtures.
- Selecting a file node still loads behaviour summary and diff.

## Tests
- E2E: verify flow endpoint data exists; screenshot or DOM presence check in UI tests if available.
