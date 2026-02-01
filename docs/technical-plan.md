# Yotei – AI-Native Code Review Platform

## Purpose

Yotei is a **language-agnostic, AI-native code review system** designed for the era of AI-generated code.

Traditional pull requests optimise for diff inspection.
Yotei optimises for **understanding, reasoning, and conversational review**.

The system uses AI to actively:

* Explain behavioural changes
* Infer execution paths and flows
* Detect risk hotspots
* Generate targeted review questions
* Allow reviewers to talk through a change via voice

The goal is not to replace GitHub.
The goal is to replace diff-fatigue.

> A reviewer should feel like they are pair-reviewing with a senior engineer.

---

# Core Product Thesis

AI-generated code increases volume and surface area.
Reviewers struggle not because they cannot read code — but because they cannot rapidly build a mental model of change.

Yotei solves this by:

1. Converting diffs into a structured change model
2. Using AI to generate behavioural explanations
3. Visualising execution flows and affected routes
4. Providing risk-aware review guidance
5. Enabling conversational (voice + text) review

---

# Design Principles

* **AI-First, Not AI-Decorated** – AI actively reviews code, not just summarises metadata
* **Behaviour Over Syntax** – focus on what changed in runtime behaviour
* **Language-Agnostic Core** – no AST dependence in MVP
* **Deterministic & Containerised** – full Docker-based local development
* **Graphical Mental Models** – visual flows over file trees
* **Conversational Review** – reviewers can ask questions naturally (voice or text)
* **Beautiful Dark UI** – modern, minimal, high-contrast, data-driven

---

# High-Level Architecture

```
GitHub Repo (Source of Truth)
        │
        ▼
PR Ingestion Adapter (Pull-based)
        │
        ▼
Review Engine
   ├─ Diff Parser
   ├─ Change Model Builder
   ├─ Execution Flow Inference
   ├─ Risk Engine
   ├─ LLM Review Engine
   └─ Conversation Engine (Voice/Text)
        │
        ▼
Review Session Store (Postgres)
        │
        ▼
Dark Mode Dashboard UI (Graphical)
```

---

# AI Review Engine (Core Capability)

Yotei is not a metadata viewer.
It actively uses AI to review code.

## Responsibilities

For each file or logical change unit, AI must generate:

* Behaviour summary
* Execution paths affected
* Side effects introduced
* Risk classification
* Targeted reviewer questions
* Suggested missing tests
* Known unknowns

All AI output is:

* Scoped
* Structured (JSON)
* Persisted
* Reproducible

---

# Conversational & Voice Review

Yotei supports conversational review:

## Capabilities

* Push-to-talk voice interaction
* Scoped contextual Q&A (bound to selected node or flow)
* AI voice responses (streamed)
* UI updates driven by AI answers
* Persistent review transcript

## Design Constraints

* Voice interactions are always scoped to the current change node or flow
* AI must reference diff evidence
* AI may update checklist and risk flags

Voice is not chat.
Voice is guided review.

---

# Change Model

## ReviewSession

```json
{
  "id": "uuid",
  "repo": "org/repo",
  "prNumber": 123,
  "baseSha": "...",
  "headSha": "...",
  "status": "in_review | approved",
  "aiVersion": "v1",
  "createdAt": "..."
}
```

## ChangeNode

```json
{
  "id": "uuid",
  "type": "file | flow | side_effect | risk | entry_point",
  "label": "src/jobs/refund.cs",
  "changeType": "added | modified | deleted",
  "riskTags": ["money", "async"],
  "behaviourSummary": "...",
  "executionPaths": [],
  "sideEffects": [],
  "reviewQuestions": [],
  "evidence": []
}
```

---

# Execution Flow Visualisation (Flagship Feature)

Instead of a simple file tree, Yotei renders an animated execution graph.

## Visual Elements

* User / API nodes
* Internal services
* Jobs / async workers
* Queues / messaging
* External services
* Database interactions

## Flow Behaviour

* Animated path highlighting when selecting a node
* Pulsing risk hotspots
* Side effects glow subtly
* Hover reveals evidence from diff
* Clicking a flow segment shows AI explanation

The flow should feel alive — but restrained.
No neon chaos.
Subtle motion, purposeful highlighting.

---

# UI Philosophy

## Dark Mode Dashboard

Primary experience is dark.

Visual tone:

* Deep charcoal background
* Soft gradients
* High-contrast text
* Muted accent colors
* Risk colors used sparingly

Design inspiration:

* Modern AI tools (Linear, Vercel, Notion AI, OpenAI dashboards)
* Clean spacing
* No decorative AI fluff
* No meaningless icons

## Layout

Left: Review Sessions
Center: Change Summary + Animated Flow
Right: Review Checklist + Voice + Raw Diff

Raw diff always available.
Never "Diff not available".

---

# Risk Engine

Risk tags inferred via heuristics:

| Risk     | Signals                |
| -------- | ---------------------- |
| money    | payment terms, amounts |
| async    | jobs, retry loops      |
| external | outbound calls         |
| data     | PII keywords           |
| auth     | token/permission usage |
| perf     | loops, heavy queries   |

Risk drives:

* Visual emphasis
* Checklist generation
* Conversational prompts

---

# Review Checklist Generation

AI generates contextual review questions based on:

* Diff content
* Risk tags
* Execution flows

Example (money + async):

* Is this idempotent under retry?
* What happens if external API times out?
* Can duplicate execution cause double payment?

Checklist updates dynamically when AI conversation uncovers new concerns.

---

# Diff Handling

Diff is ground truth.

Requirements:

* Raw diff stored in Postgres or mounted volume
* Always accessible via API
* Rendered with syntax highlighting
* Highlight segments referenced by AI

No reliance on S3 paths that fail locally.

---

# Local Development & Infrastructure

## Non-Negotiables

* Docker-only execution
* Deterministic fixtures
* Full E2E tests
* No ngrok
* Pull-based PR ingestion

## Stack

Backend:

* .NET (Minimal API)
* Postgres
* OpenAI API

Frontend:

* React + Vite
* Tailwind
* Framer Motion (subtle animation)
* D3 / React Flow for graph rendering

Infra:

* docker-compose
* LocalStack (S3 optional)

---

# End-to-End Testing

E2E tests must verify:

* PR fixture ingestion
* Change model generation
* AI behaviour summary exists
* Risk tags generated
* Flow graph renders
* Raw diff accessible
* Voice transcript persisted

CI runs identical docker-compose stack.

---

# MVP Scope (Revised)

Phase 0:

* PR ingestion (pull-based)
* Diff storage
* Change model generation
* AI file-level behaviour summaries
* Risk detection
* Dark dashboard UI
* Static execution flow graph
* Deterministic E2E tests

Phase 1:

* Animated flow highlighting
* Voice interaction (push-to-talk)
* Structured conversation persistence
* Checklist auto-updates

Phase 2:

* Org-wide insights
* Review transcripts export
* Compliance reporting
* Advanced inference adapters

---

# Success Criteria

Yotei succeeds when:

A reviewer can:

1. Open a PR
2. Visually understand execution impact in under 60 seconds
3. Ask questions conversationally
4. Feel confident approving without reading every line

And say:

> "I understand what changed and where the risk is."

---

# Summary

Yotei is not a prettier PR viewer.
It is an AI-native review system that:

* Builds behavioural mental models
* Visualises runtime impact
* Generates risk-aware guidance
* Enables conversational reasoning
* Delivers a beautiful, dark, graphical dashboard experience

Designed for the realities of AI-generated code.