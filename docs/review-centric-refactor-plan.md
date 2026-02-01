# Review-Centric Refactor Execution Plan (Phase 0 MVP)

This plan refactors the current snapshot‑admin experience into a review‑centric comprehension interface without adding language‑specific parsing, webhooks, or new platforms.

---

## Step 1 — Reframe the surface around reviews (no new integration)
- [x] Treat snapshots as internal fixtures only; introduce **ReviewSession** as the public concept in API + UI.
- [x] Update labels/copy so users see “Review Session” instead of “Snapshot”.
- [x] Keep ingestion and fixtures unchanged; only map them to review sessions.

Acceptance criteria:
- UI and API surfaces present “review sessions” rather than “snapshots”.
- Existing fixtures still load with no changes to ingestion workflow.

---

## Step 2 — Add review‑centric data model (minimal new tables)
- [x] Add **ReviewSession** (1:1 with snapshot).
- [x] Add **ReviewSummary** (persisted, not computed on request).
- [x] Replace ChangeTree with **ReviewNode** (tree), including evidence + risk tags.
- [x] Add **ReviewNodeExplanation** with non‑empty fallback text.

Acceptance criteria:
- ReviewSession, ReviewSummary, ReviewNode, ReviewNodeExplanation are persisted.
- Every ReviewNode has `nodeId`, `nodeType`, `label`, `changeType`, `riskTags[]`, `evidence[]`.

---

## Step 3 — Implement Change Tree v0 (language‑agnostic)
Top‑level groups:
- Overview
- Files Changed
- Risks
- Side Effects

Tasks:
- [x] Build group nodes first, then attach child nodes.
- [x] Files Changed group: one node per changed file (+ optional hunk nodes).
- [x] Risks group: one node per detected risk tag with evidence.
- [x] Side Effects group: one node per detected side‑effect with evidence.
- [x] Overview group: summary node + entry‑point node(s).

Acceptance criteria:
- Tree always includes all four top‑level groups.
- Tree contains non‑file nodes when heuristics detect any risks/side‑effects/entry points.

---

## Step 4 — Heuristics engine (no AST, no framework logic)
Implement detections using **file paths + diff keywords only**:

Risk tags:
- money: `billing`, `payment`, `invoice`, `price`, `charge`, `stripe`, `amount`
- auth: `auth`, `token`, `jwt`, `permission`, `role`, `login`
- data: `email`, `phone`, `ssn`, `address`, `dob`, `user`, `pii`, `gdpr`
- async: `queue`, `job`, `worker`, `retry`, `cron`, `schedule`
- external: `http`, `https`, `api`, `client`, `sdk`, `webhook`

Side effects:
- db: `sql`, `db`, `repository`, `migration`
- network: `http`, `fetch`, `axios`, `curl`, `client`
- filesystem: `file`, `fs`, `s3`, `write`, `upload`
- messaging: `queue`, `kafka`, `sns`, `sqs`, `pubsub`
- email: `email`, `smtp`, `sendgrid`, `mail`

Where it runs (entry points):
- paths/keywords: `controller`, `route`, `handler`, `api`, `endpoint`, `job`, `worker`, `cron`, `scheduler`

Acceptance criteria:
- At least one risk tag and side‑effect is detected for seeded fixtures with matching keywords.
- Entry‑point nodes appear when paths/keywords match (no AST).

---

## Step 5 — Explanations are never blank
- [x] Generate a deterministic fallback explanation for every node.
- [x] If LLM is used, store response; if not, store fallback.
- [x] Fallback text must be human‑readable and mention evidence (file paths, hunks, keywords).

Acceptance criteria:
- `GET /review-nodes/{id}/explanation` always returns non‑empty text.
- UI never shows “None” or blank explanation.

---

## Step 6 — Fix raw diff reliability (ground truth)
- [x] Always store diff blobs in S3 (LocalStack locally).
- [x] Store S3 key in DB on ingestion.
- [x] Raw diff endpoint always reads from S3; no in‑memory fallbacks.
- [x] Update fixtures to ensure diffs are written into LocalStack.

Acceptance criteria:
- Raw diff endpoint returns content for fixture files.
- “Diff not available” only appears if the file truly has no diff.

---

## Step 7 — API surface aligned to reviews
Add/rename endpoints:
- [x] `GET /review-sessions`
- [x] `GET /review-sessions/{id}`
- [x] `GET /review-sessions/{id}/summary`
- [x] `POST /review-sessions/{id}/build` (summary + tree + explanations)
- [x] `GET /review-sessions/{id}/change-tree`
- [x] `GET /review-nodes/{id}/explanation`
- [x] `POST /review-nodes/{id}/explanation` (LLM trigger)

Acceptance criteria:
- Review endpoints work with existing fixtures and return non‑empty data.

---

## Step 8 — Frontend refactor to review‑centric UI
- [x] Replace snapshot list with review session list (same data).
- [x] Add **Change Summary** card at top of detail view.
- [x] Replace file‑tree with Review Tree v0 groups.
- [x] Ensure explanation panel shows fallback text when LLM hasn’t run.
- [x] Keep raw diff panel as ground truth.

Acceptance criteria:
- Review summary visible for each session.
- Change tree shows Overview/Files/Risks/Side Effects.
- Explanations never blank; raw diff loads.

---

## Step 9 — E2E tests for review comprehension
- [x] Seed fixture snapshot + diffs into LocalStack.
- [x] Call build endpoint for a review session.
- [x] Assert summary is present and non‑empty.
- [x] Assert tree includes all four top‑level groups.
- [x] Assert at least one risk tag is returned.
- [x] Assert raw diff content is returned for a known file.
- [x] Assert explanation text is non‑empty for a node.

Acceptance criteria:
- E2E test passes locally with containers only.
- Test fails if tree is ROOT→FILE only or raw diff is missing.

---

## Step 10 — Definition of done (Phase 0 refactor)
- [ ] Reviewer can answer “what changed / where it runs / side effects / risks” without reading full diff.
- [ ] UI reads as a review product (not snapshot admin).
- [ ] Everything runs locally in Docker/Podman with LocalStack + Postgres.

---

## Review Accelerator Improvements

### Step 11 — Reliable raw diffs
- [x] Store raw diff text in Postgres at ingest time.
- [x] Add review-node diff endpoint.
- [x] UI renders diffs from review-node endpoint.

### Step 12 — Behaviour summaries
- [x] Generate 3-bullet behaviour summary per file node.
- [x] Persist behaviour summary.
- [x] Replace explanation panel with behaviour summary in UI.

### Step 13 — Review checklist
- [x] Generate checklist questions from risk tags.
- [x] Persist checklist items.
- [x] Display checklist for file nodes.

### Step 14 — Review-oriented tree
- [x] Attach risks/side-effects/checklist under each file node.
- [x] Keep a small change summary; navigation is file-first.

### Step 15 — E2E coverage
- [x] Diff loads for seeded fixtures.
- [x] Behaviour summary present.
- [x] Checklist includes money/async/external prompts.
