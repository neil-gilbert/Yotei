using Microsoft.EntityFrameworkCore;
using Yotei.Api.Data;
using Yotei.Api.Infrastructure;
using Yotei.Api.Storage;
using Yotei.Api.Features.Tenancy;
using ChangeTreeModel = Yotei.Api.Models.ChangeTree;
using ChangeNodeModel = Yotei.Api.Models.ChangeNode;

namespace Yotei.Api.Features.ChangeTree;

public static class ChangeTreeEndpoints
{
    public static IEndpointRouteBuilder MapChangeTreeEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/snapshots/{snapshotId:guid}/change-tree/build", async (
            Guid snapshotId,
            TenantContext tenantContext,
            YoteiDbContext db,
            IRawDiffStorage storage,
            IExplanationGenerator explanationGenerator,
            CancellationToken cancellationToken) =>
        {
            var snapshot = await db.PullRequestSnapshots
                .Include(s => s.FileChanges)
                .FirstOrDefaultAsync(
                    s => s.Id == snapshotId && s.TenantId == tenantContext.TenantId,
                    cancellationToken);

            if (snapshot is null)
            {
                return Results.NotFound(new { error = "snapshot not found" });
            }

            var existingTree = await db.ChangeTrees
                .Include(tree => tree.Nodes)
                .FirstOrDefaultAsync(tree => tree.PullRequestSnapshotId == snapshotId, cancellationToken);

            if (existingTree is not null)
            {
                db.ChangeTrees.Remove(existingTree);
            }

            var tree = new ChangeTreeModel
            {
                PullRequestSnapshotId = snapshotId
            };

            var rootNode = new ChangeNodeModel
            {
                NodeType = "root",
                Label = $"Snapshot {snapshot.PrNumber}",
                ChangeTree = tree
            };

            tree.Nodes.Add(rootNode);

            foreach (var change in snapshot.FileChanges)
            {
                var fileNode = new ChangeNodeModel
                {
                    NodeType = "file",
                    Label = change.Path,
                    Path = change.Path,
                    ChangeType = change.ChangeType,
                    AddedLines = change.AddedLines,
                    DeletedLines = change.DeletedLines,
                    RawDiffRef = change.RawDiffRef,
                    Parent = rootNode
                };

                tree.Nodes.Add(fileNode);

                if (!string.IsNullOrWhiteSpace(change.RawDiffRef))
                {
                    var diff = await storage.GetDiffAsync(change.RawDiffRef, cancellationToken);
                    if (!string.IsNullOrWhiteSpace(diff))
                    {
                        var hunkNodes = BuildHunkNodes(change.Path, change.ChangeType, change.RawDiffRef, diff, fileNode);
                        tree.Nodes.AddRange(hunkNodes);
                    }
                }
            }

            db.ChangeTrees.Add(tree);
            await db.SaveChangesAsync(cancellationToken);

            var explanations = new List<Yotei.Api.Models.ChangeNodeExplanation>();
            foreach (var node in tree.Nodes)
            {
                var explanation = await explanationGenerator.GenerateAsync(node, cancellationToken);
                explanations.Add(explanation);
            }

            db.ChangeNodeExplanations.AddRange(explanations);
            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(new ChangeTreeBuildResponse(tree.Id, tree.Nodes.Count));
        });

        app.MapGet("/snapshots/{snapshotId:guid}/change-tree", async (
            Guid snapshotId,
            TenantContext tenantContext,
            YoteiDbContext db) =>
        {
            var tree = await db.ChangeTrees
                .AsNoTracking()
                .Include(t => t.Nodes)
                .FirstOrDefaultAsync(t =>
                    t.PullRequestSnapshotId == snapshotId &&
                    t.PullRequestSnapshot != null &&
                    t.PullRequestSnapshot.TenantId == tenantContext.TenantId);

            if (tree is null)
            {
                return Results.NotFound(new { error = "change tree not found" });
            }

            var response = new ChangeTreeResponse(
                tree.Id,
                tree.PullRequestSnapshotId,
                tree.CreatedAt,
                tree.Nodes
                    .OrderBy(node => node.NodeType)
                    .Select(node => new ChangeNodeResponse(
                        node.Id,
                        node.ParentId,
                        node.NodeType,
                        node.Label,
                        node.Path,
                        node.ChangeType,
                        node.AddedLines,
                        node.DeletedLines,
                        node.RawDiffRef))
                    .ToList());

            return Results.Ok(response);
        });

        return app;
    }

    private static List<ChangeNodeModel> BuildHunkNodes(
        string? path,
        string? changeType,
        string? rawDiffRef,
        string diff,
        ChangeNodeModel parent)
    {
        var nodes = new List<ChangeNodeModel>();
        var lines = diff.Replace("\r", string.Empty).Split('\n');
        var currentHeader = string.Empty;
        var currentAdded = 0;
        var currentDeleted = 0;

        void Flush()
        {
            if (string.IsNullOrWhiteSpace(currentHeader))
            {
                return;
            }

            nodes.Add(new ChangeNodeModel
            {
                NodeType = "hunk",
                Label = currentHeader,
                Path = path,
                ChangeType = changeType,
                AddedLines = currentAdded,
                DeletedLines = currentDeleted,
                RawDiffRef = rawDiffRef,
                Parent = parent
            });

            currentHeader = string.Empty;
            currentAdded = 0;
            currentDeleted = 0;
        }

        foreach (var line in lines)
        {
            if (line.StartsWith("@@"))
            {
                Flush();
                currentHeader = line.Trim();
                continue;
            }

            if (string.IsNullOrEmpty(currentHeader))
            {
                continue;
            }

            if (line.StartsWith("+") && !line.StartsWith("+++"))
            {
                currentAdded++;
            }
            else if (line.StartsWith("-") && !line.StartsWith("---"))
            {
                currentDeleted++;
            }
        }

        Flush();

        return nodes;
    }
}
