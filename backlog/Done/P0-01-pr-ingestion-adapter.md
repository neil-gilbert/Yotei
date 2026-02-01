# P0-01 PR ingestion adapter (pull-based GitHub)

## Goal
Implement a pull-based GitHub ingestion adapter that creates review sessions from live PRs without pushing payloads into the API.

## Scope
- Add a GitHub ingestion service that can poll configured repos and PR numbers.
- Create snapshots + file changes + raw diffs from GitHub API responses.
- Keep fixture ingestion for deterministic dev/test, but route “real” ingestion through the adapter.

## Technical notes
- Backend (.NET Minimal API)
  - Add new endpoints:
    - `POST /ingest/github` (body: repo, prNumber, optional ref) to trigger a one-off pull.
    - `POST /ingest/github/sync` to enqueue or run a scheduled sync for configured repos.
  - Add new service: `IGithubIngestionService` with implementation using GitHub REST API.
  - Add config section `GitHub__Token`, `GitHub__BaseUrl`, `GitHub__Repos`, `GitHub__SyncIntervalMinutes`.
  - Use GitHub endpoints: `GET /repos/{owner}/{repo}/pulls/{number}`, `GET /repos/{owner}/{repo}/pulls/{number}/files`.
  - Create `PullRequestSnapshot`, `FileChange`, and raw diff payloads from PR file list.
  - Store raw diffs in configured `IRawDiffStorage` implementation.
  - Add idempotency: if snapshot already exists (same repo/pr/headSha) return existing id.
  - Handle pagination in `/pulls/{number}/files`.
  - Track last sync per repo via new table `IngestionCursor` (repo, lastSyncedAt, lastHeadSha).

- Infra
  - Add secrets handling for GitHub token in `docker-compose.yml`.

## Acceptance criteria
- Given a repo/prNumber, the system can create a snapshot and file changes without manual payloads.
- Duplicate pulls do not create duplicate snapshots.
- Raw diffs are persisted and accessible via existing diff endpoints.
- Adapter handles pagination for PRs with >100 files.

## Tests
- Integration test: pull a fixture-backed PR via mocked GitHub responses.
- E2E: use deterministic fixtures to validate end-to-end ingestion without manual payloads.
