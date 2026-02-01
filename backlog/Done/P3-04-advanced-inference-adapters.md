# P3-04 Advanced inference adapters

## Goal
Support language-specific inference adapters for deeper flow analysis.

## Scope
- Adapter interface and sample implementations (e.g., C#, JS).

## Technical notes
- Backend
  - Define `IFlowInferenceAdapter` interface.
  - Add adapter registry and config-driven selection by repo language.
  - Implement a basic C# adapter using Roslyn (future step) and JS adapter using regex/AST.

## Acceptance criteria
- Adapter selection is pluggable and testable.

## Tests
- Unit tests for adapter selection and basic inference output.
