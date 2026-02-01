using Microsoft.EntityFrameworkCore;
using Yotei.Api.Data;
using Yotei.Api.Models;

namespace Yotei.Api.Features.FileChanges;

public static class FileChangeEndpoints
{
    public static IEndpointRouteBuilder MapFileChangeEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/snapshots/{snapshotId:guid}/file-changes", async (
            Guid snapshotId,
            FileChangeBatchRequest request,
            YoteiDbContext db) =>
        {
            var validationErrors = request.Validate();
            if (validationErrors.Count > 0)
            {
                return Results.BadRequest(new { errors = validationErrors });
            }

            var snapshot = await db.PullRequestSnapshots
                .Include(s => s.FileChanges)
                .FirstOrDefaultAsync(s => s.Id == snapshotId);

            if (snapshot is null)
            {
                return Results.NotFound(new { error = "snapshot not found" });
            }

            var existingByPath = snapshot.FileChanges.ToDictionary(fc => fc.Path, StringComparer.OrdinalIgnoreCase);
            var created = 0;
            var updated = 0;

            foreach (var change in request.Changes ?? [])
            {
                if (existingByPath.TryGetValue(change.Path, out var existing))
                {
                    existing.ChangeType = change.ChangeType ?? existing.ChangeType;
                    existing.AddedLines = change.AddedLines;
                    existing.DeletedLines = change.DeletedLines;
                    existing.RawDiffRef = change.RawDiffRef;
                    if (!string.IsNullOrWhiteSpace(change.RawDiffText))
                    {
                        existing.RawDiffText = change.RawDiffText;
                    }
                    updated++;
                }
                else
                {
                    snapshot.FileChanges.Add(new FileChange
                    {
                        Path = change.Path,
                        ChangeType = change.ChangeType ?? "modified",
                        AddedLines = change.AddedLines,
                        DeletedLines = change.DeletedLines,
                        RawDiffRef = change.RawDiffRef,
                        RawDiffText = change.RawDiffText
                    });
                    created++;
                }
            }

            await db.SaveChangesAsync();

            return Results.Ok(new { created, updated });
        });

        app.MapGet("/snapshots/{snapshotId:guid}/file-changes", async (
            Guid snapshotId,
            string? changeType,
            string? pathPrefix,
            YoteiDbContext db) =>
        {
            var errors = new List<string>();
            if (changeType is not null && string.IsNullOrWhiteSpace(changeType))
            {
                errors.Add("changeType is required");
            }

            if (pathPrefix is not null && string.IsNullOrWhiteSpace(pathPrefix))
            {
                errors.Add("pathPrefix is required");
            }

            if (errors.Count > 0)
            {
                return Results.BadRequest(new { errors });
            }

            var snapshot = await db.PullRequestSnapshots
                .AsNoTracking()
                .Include(s => s.FileChanges)
                .FirstOrDefaultAsync(s => s.Id == snapshotId);

            if (snapshot is null)
            {
                return Results.NotFound(new { error = "snapshot not found" });
            }

            IEnumerable<FileChange> changes = snapshot.FileChanges;

            if (!string.IsNullOrWhiteSpace(changeType))
            {
                changes = changes.Where(fc => string.Equals(fc.ChangeType, changeType, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(pathPrefix))
            {
                changes = changes.Where(fc => fc.Path.StartsWith(pathPrefix, StringComparison.OrdinalIgnoreCase));
            }

            var response = changes
                .OrderBy(fc => fc.Path)
                .Select(fc => new FileChangeItem(
                    fc.Path,
                    fc.ChangeType,
                    fc.AddedLines,
                    fc.DeletedLines,
                    fc.RawDiffRef,
                    fc.CreatedAt))
                .ToList();

            return Results.Ok(response);
        });

        return app;
    }
}
