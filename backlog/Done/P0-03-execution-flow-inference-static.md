# P0-03 Execution flow inference (static)

## Goal
Generate a static execution flow graph for a review session using heuristics (no AST).

## Scope
- Add flow graph model and API response.
- Build flow nodes/edges based on file paths, risk tags, and side-effect evidence.

## Technical notes
- Backend
  - New models: `FlowGraph`, `FlowNode`, `FlowEdge` (id, type, label, evidence).
  - Endpoint: `GET /review-sessions/{id}/flow` returning nodes/edges.
  - Heuristics:
    - Entry points = nodes derived from `entry_point` nodes or API-related file paths.
    - Side effects map to nodes: `db`, `queue`, `external`, `filesystem`, `email`.
    - Edges connect entry → file → side effect nodes.
  - Persist flow graph or compute on read (prefer persisted for deterministic UI).

## Acceptance criteria
- For seeded fixtures, flow graph returns at least one entry node + one side-effect node.
- Graph response is deterministic for the same snapshot.

## Tests
- Integration test asserts `/review-sessions/{id}/flow` returns nodes/edges.
- E2E asserts flow graph exists after build.
