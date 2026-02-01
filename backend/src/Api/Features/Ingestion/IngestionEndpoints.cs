using Microsoft.EntityFrameworkCore;
using Yotei.Api.Data;
using Yotei.Api.Models;

namespace Yotei.Api.Features.Ingestion;

public static class IngestionEndpoints
{
    /// <summary>
    /// Maps ingestion endpoints for snapshots and GitHub pull-based ingestion.
    /// </summary>
    /// <param name="app">The route builder used to register endpoints.</param>
    /// <returns>The same route builder instance for chaining.</returns>
    public static IEndpointRouteBuilder MapIngestionEndpoints(this IEndpointRouteBuilder app)
    {
        // Manually ingest a snapshot payload.
        app.MapPost("/ingest/snapshot", async (IngestSnapshotRequest request, YoteiDbContext db) =>
        {
            var validationErrors = request.Validate();
            if (validationErrors.Count > 0)
            {
                return Results.BadRequest(new { errors = validationErrors });
            }

            var repo = await db.Repositories
                .FirstOrDefaultAsync(r => r.Owner == request.Owner && r.Name == request.Name);

            if (repo is null)
            {
                repo = new Repository
                {
                    Owner = request.Owner,
                    Name = request.Name,
                    DefaultBranch = request.DefaultBranch ?? "main"
                };
                db.Repositories.Add(repo);
            }
            else if (!string.IsNullOrWhiteSpace(request.DefaultBranch) && repo.DefaultBranch != request.DefaultBranch)
            {
                repo.DefaultBranch = request.DefaultBranch;
            }

            PullRequestSnapshot? existing = null;
            if (repo.Id != Guid.Empty)
            {
                existing = await db.PullRequestSnapshots.FirstOrDefaultAsync(snapshot =>
                    snapshot.RepositoryId == repo.Id &&
                    snapshot.PrNumber == request.PrNumber &&
                    snapshot.HeadSha == request.HeadSha);
            }

            if (existing is not null)
            {
                return Results.Ok(new { snapshotId = existing.Id, created = false });
            }

            var snapshot = new PullRequestSnapshot
            {
                Repository = repo,
                PrNumber = request.PrNumber,
                BaseSha = request.BaseSha,
                HeadSha = request.HeadSha,
                Source = request.Source ?? "fixture",
                Title = request.Title
            };

            db.PullRequestSnapshots.Add(snapshot);
            await db.SaveChangesAsync();

            return Results.Ok(new { snapshotId = snapshot.Id, created = true });
        });

        // Pull a GitHub PR into the ingestion pipeline.
        app.MapPost("/ingest/github", async (
            GitHubIngestRequest request,
            IGithubIngestionService ingestionService,
            CancellationToken cancellationToken) =>
        {
            var validationErrors = request.Validate();
            if (validationErrors.Count > 0)
            {
                return Results.BadRequest(new { errors = validationErrors });
            }

            var result = await ingestionService.IngestPullRequestAsync(request, cancellationToken);
            if (result.Errors.Count > 0 || result.SnapshotId is null)
            {
                return Results.Problem(detail: string.Join(" | ", result.Errors));
            }

            var response = new GitHubIngestResponse(result.SnapshotId.Value, result.Created, result.FileChangesCount);
            return Results.Ok(response);
        });

        // Sync configured GitHub repos for open PRs.
        app.MapPost("/ingest/github/sync", async (
            IGithubIngestionService ingestionService,
            CancellationToken cancellationToken) =>
        {
            var result = await ingestionService.SyncConfiguredReposAsync(cancellationToken);
            var response = new GitHubSyncResponse(result.Repositories, result.PullRequests, result.SnapshotsCreated, result.Errors);
            return Results.Ok(response);
        });

        return app;
    }
}
