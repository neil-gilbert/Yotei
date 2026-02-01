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
- No GitHub webhooks are used in this phase; ingestion will be pull-based or fixture-based.
