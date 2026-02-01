# P2-01 Animated flow highlighting

## Goal
Animate the flow graph to highlight execution paths and risk hotspots.

## Scope
- Add Framer Motion or React Flow animations on selection.
- Highlight risk-tagged nodes.

## Technical notes
- Frontend
  - Animate edge glow on node selection.
  - Pulse risk nodes based on tag severity.
  - Maintain subtle motion (no neon).

## Acceptance criteria
- Selecting a node animates its flow path.
- Risk nodes have visible, subtle pulse.

## Tests
- Manual UI validation.
