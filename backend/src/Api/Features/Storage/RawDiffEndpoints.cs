using Microsoft.EntityFrameworkCore;
using Yotei.Api.Data;
using Yotei.Api.Models;
using Yotei.Api.Storage;

namespace Yotei.Api.Features.Storage;

public static class RawDiffEndpoints
{
    public static IEndpointRouteBuilder MapRawDiffEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/snapshots/{snapshotId:guid}/file-changes/upload", async (
            Guid snapshotId,
            RawDiffUploadRequest request,
            YoteiDbContext db,
            IRawDiffStorage storage,
            CancellationToken cancellationToken) =>
        {
            var validationErrors = request.Validate();
            if (validationErrors.Count > 0)
            {
                return Results.BadRequest(new { errors = validationErrors });
            }

            var snapshot = await db.PullRequestSnapshots
                .Include(s => s.FileChanges)
                .FirstOrDefaultAsync(s => s.Id == snapshotId, cancellationToken);

            if (snapshot is null)
            {
                return Results.NotFound(new { error = "snapshot not found" });
            }

            var rawDiffRef = await storage.StoreDiffAsync(snapshotId, request.Path, request.Diff, cancellationToken);

            var existing = snapshot.FileChanges
                .FirstOrDefault(fc => string.Equals(fc.Path, request.Path, StringComparison.OrdinalIgnoreCase));

            if (existing is null)
            {
                snapshot.FileChanges.Add(new FileChange
                {
                    Path = request.Path,
                    ChangeType = request.ChangeType ?? "modified",
                    AddedLines = request.AddedLines,
                    DeletedLines = request.DeletedLines,
                    RawDiffRef = rawDiffRef,
                    RawDiffText = request.Diff
                });
            }
            else
            {
                existing.ChangeType = request.ChangeType ?? existing.ChangeType;
                existing.AddedLines = request.AddedLines;
                existing.DeletedLines = request.DeletedLines;
                existing.RawDiffRef = rawDiffRef;
                existing.RawDiffText = request.Diff;
            }

            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(new { rawDiffRef });
        });

        app.MapGet("/raw-diffs/{snapshotId:guid}", async (
            Guid snapshotId,
            string? path,
            YoteiDbContext db,
            IRawDiffStorage storage,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return Results.BadRequest(new { errors = new[] { "path is required" } });
            }

            var snapshot = await db.PullRequestSnapshots
                .AsNoTracking()
                .Include(s => s.FileChanges)
                .FirstOrDefaultAsync(s => s.Id == snapshotId, cancellationToken);

            if (snapshot is null)
            {
                return Results.NotFound(new { error = "snapshot not found" });
            }

            var change = snapshot.FileChanges
                .FirstOrDefault(fc => string.Equals(fc.Path, path, StringComparison.OrdinalIgnoreCase));

            if (change is null)
            {
                return Results.NotFound(new { error = "raw diff not found" });
            }

            if (!string.IsNullOrWhiteSpace(change.RawDiffText))
            {
                return Results.Text(change.RawDiffText, "text/plain");
            }

            if (string.IsNullOrWhiteSpace(change.RawDiffRef))
            {
                return Results.NotFound(new { error = "raw diff not found" });
            }

            var diff = await storage.GetDiffAsync(change.RawDiffRef, cancellationToken);
            if (diff is null)
            {
                return Results.NotFound(new { error = "raw diff not found" });
            }

            change.RawDiffText = diff;
            db.FileChanges.Update(change);
            await db.SaveChangesAsync(cancellationToken);

            return Results.Text(diff, "text/plain");
        });

        return app;
    }
}
