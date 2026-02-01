# P0-02 Local raw-diff storage (no S3 reliance)

## Goal
Ensure raw diffs are always accessible locally without depending on S3/LocalStack.

## Scope
- Add a storage provider that persists raw diffs in Postgres (or local filesystem).
- Make storage provider configurable via `Storage__Provider`.

## Technical notes
- Backend
  - Create `DatabaseRawDiffStorage : IRawDiffStorage`.
  - New table: `RawDiffBlobs` with `Id`, `SnapshotId`, `Path`, `DiffText`, `CreatedAt`.
  - `StoreDiffAsync` inserts a row and returns a `db://{id}` ref.
  - `GetDiffAsync` resolves `db://` refs.
  - Update `IRawDiffStorage` resolution based on config (`S3` vs `Database`).
  - Update migrations and model snapshot.

## Acceptance criteria
- Diffs remain accessible even when S3/LocalStack is disabled.
- Upload endpoint persists raw diff text and can be fetched later via `/raw-diffs` and `/review-nodes/{id}/diff`.

## Tests
- Integration test: upload diff and retrieve it with `DatabaseRawDiffStorage`.
