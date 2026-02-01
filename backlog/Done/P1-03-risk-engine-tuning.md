# P1-03 Risk engine tuning

## Goal
Improve risk detection quality and evidence capture.

## Scope
- Expand keyword sets and add severity levels.
- Improve evidence tagging and ordering.

## Technical notes
- Backend
  - Expand `RiskKeywords` with additional domain terms.
  - Add `riskSeverity` (low/medium/high) to nodes based on tag combos.
  - Improve evidence to include line numbers/hunk context where available.
  - Update `ReviewNodeInsightsGenerator` focus questions based on severity.

## Acceptance criteria
- Risk tags include severity metadata.
- Evidence strings include hunk context when available.

## Tests
- Unit tests for severity and evidence mapping.
