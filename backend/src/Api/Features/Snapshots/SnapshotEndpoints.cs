using Microsoft.EntityFrameworkCore;
using Yotei.Api.Data;
using Yotei.Api.Features.FileChanges;
namespace Yotei.Api.Features.Snapshots;

public static class SnapshotEndpoints
{
    public static IEndpointRouteBuilder MapSnapshotEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/snapshots", async (int? limit, int? offset, YoteiDbContext db) =>
        {
            var resolvedLimit = limit ?? 20;
            var resolvedOffset = offset ?? 0;
            var errors = new List<string>();

            if (resolvedLimit < 1 || resolvedLimit > 100)
            {
                errors.Add("limit must be between 1 and 100");
            }

            if (resolvedOffset < 0)
            {
                errors.Add("offset must be greater than or equal to 0");
            }

            if (errors.Count > 0)
            {
                return Results.BadRequest(new { errors });
            }

            var snapshots = await db.PullRequestSnapshots
                .AsNoTracking()
                .Include(snapshot => snapshot.Repository)
                .OrderByDescending(snapshot => snapshot.IngestedAt)
                .Skip(resolvedOffset)
                .Take(resolvedLimit)
                .Select(snapshot => new SnapshotListItem(
                    snapshot.Id,
                    snapshot.Repository!.Owner,
                    snapshot.Repository.Name,
                    snapshot.PrNumber,
                    snapshot.BaseSha,
                    snapshot.HeadSha,
                    snapshot.Title,
                    snapshot.IngestedAt))
                .ToListAsync();

            return Results.Ok(snapshots);
        });

        app.MapGet("/snapshots/{snapshotId:guid}", async (
            Guid snapshotId,
            bool? includeFileChanges,
            YoteiDbContext db) =>
        {
            var includeChanges = includeFileChanges ?? true;

            var snapshot = await db.PullRequestSnapshots
                .AsNoTracking()
                .Include(s => s.Repository)
                .Include(s => s.FileChanges)
                .FirstOrDefaultAsync(s => s.Id == snapshotId);

            if (snapshot is null)
            {
                return Results.NotFound(new { error = "snapshot not found" });
            }

            var fileChanges = includeChanges
                ? snapshot.FileChanges
                    .OrderBy(fc => fc.Path)
                    .Select(fc => new FileChangeItem(
                        fc.Path,
                        fc.ChangeType,
                        fc.AddedLines,
                        fc.DeletedLines,
                        fc.RawDiffRef,
                        fc.CreatedAt))
                    .ToList()
                : [];

            var response = new SnapshotDetail(
                snapshot.Id,
                snapshot.Repository!.Owner,
                snapshot.Repository.Name,
                snapshot.PrNumber,
                snapshot.BaseSha,
                snapshot.HeadSha,
                snapshot.Title,
                snapshot.Source,
                snapshot.Repository.DefaultBranch,
                snapshot.IngestedAt,
                fileChanges);

            return Results.Ok(response);
        });

        app.MapDelete("/snapshots/{snapshotId:guid}", async (Guid snapshotId, YoteiDbContext db) =>
        {
            var snapshot = await db.PullRequestSnapshots
                .Include(s => s.FileChanges)
                .FirstOrDefaultAsync(s => s.Id == snapshotId);

            if (snapshot is null)
            {
                return Results.NotFound(new { error = "snapshot not found" });
            }

            db.PullRequestSnapshots.Remove(snapshot);
            await db.SaveChangesAsync();

            return Results.NoContent();
        });

        return app;
    }
}
