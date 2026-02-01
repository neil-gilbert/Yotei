# Yotei

Local-first review intelligence for pull requests.

## Requirements

- Podman (with `podman compose` plugin or `podman-compose`)
- Podman machine running on macOS: `podman machine start`

## Quick start

1) Copy `.env.example` to `.env` and set `OPENAI_API_KEY`.
2) Boot the stack:

```sh
podman compose up --build
```

3) Open the UI at `http://localhost:5173`.
4) API health check at `http://localhost:8080/health`.

## End-to-end smoke test

Run the containerized E2E check:

```sh
podman compose run --rm e2e
```

## Services

- `api`: .NET API for ingestion and read endpoints
- `frontend`: minimal UI (Vite + React)
- `db`: PostgreSQL
- `localstack`: S3 + SQS for local AWS-compatible infra

## Useful API endpoints

- `POST /ingest/snapshot` → create a snapshot
- `GET /snapshots` → list recent snapshots
- `GET /snapshots/{id}` → snapshot detail + file changes
- `POST /snapshots/{id}/file-changes` → upsert file changes
- `POST /snapshots/{id}/file-changes/upload` → upload a raw diff to S3 and attach it

## Notes

- LocalStack seeds an S3 bucket and SQS queue on startup.
- GitHub ingestion supports both manual pull-based triggers and webhook-driven sync.

## GitHub App ingestion (recommended)

For production-style usage, configure a GitHub App instead of PATs.

1) Create a GitHub App and install it on the repo or org.
2) Set the app credentials (see `.env.example`):
   - `GITHUB_APP_ID`
   - `GITHUB_APP_INSTALLATION_ID`
   - `GITHUB_APP_PRIVATE_KEY` (PEM or base64-encoded PEM)
   - `GITHUB_APP_WEBHOOK_SECRET`
3) Add a webhook pointing to `/ingest/github/webhook`.

The API will automatically ingest PRs on `opened`, `reopened`, `synchronize`, and `ready_for_review`.
