# P0-04 Dark dashboard layout

## Goal
Implement the dark-mode, three-panel review dashboard per technical plan.

## Scope
- Redesign layout with left sessions, center flow/summary, right checklist+diff.
- Apply dark theme visual language.

## Technical notes
- Frontend
  - Update `frontend/src/styles.css` with dark palette + CSS variables.
  - Update `App.jsx` layout: left sessions list, center summary + flow graph, right review checklist + diff.
  - Add placeholder sections for flow graph + voice panel (Phase 1).

## Acceptance criteria
- UI loads in dark mode by default.
- Layout matches left/center/right composition from plan.

## Tests
- Manual UI smoke check.
