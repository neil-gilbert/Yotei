# P1-01 LLM-backed behaviour summaries

## Goal
Replace stub behaviour summaries with LLM-generated summaries stored as structured JSON.

## Scope
- Integrate OpenAI API in backend for file-level summaries.
- Keep deterministic fallback on failure.

## Technical notes
- Backend
  - Add `OpenAI` client wrapper with retry/backoff and timeouts.
  - New service: `ReviewBehaviourSummaryGenerator`.
  - Store results in existing `ReviewNodeBehaviourSummary` table.
  - Prompt includes file path + diff + risk evidence; output JSON with `behaviourChange`, `scope`, `reviewerFocus`.
  - Use config: `OpenAI__ApiKey`, `OpenAI__Model`.

## Acceptance criteria
- Behaviour summaries are non-empty and AI-sourced when OpenAI is configured.
- Fallback summary used when API fails.

## Tests
- Unit test using stubbed OpenAI client for deterministic JSON.
