using Microsoft.EntityFrameworkCore;
using Yotei.Api.Data;
using Yotei.Api.Features.Tenancy;

namespace Yotei.Api.Features.Explanations;

public static class ExplanationEndpoints
{
    public static IEndpointRouteBuilder MapExplanationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/change-nodes/{nodeId:guid}/explanations", async (
            Guid nodeId,
            TenantContext tenantContext,
            YoteiDbContext db) =>
        {
            var explanations = await db.ChangeNodeExplanations
                .AsNoTracking()
                .Where(explanation =>
                    explanation.ChangeNodeId == nodeId &&
                    explanation.ChangeNode != null &&
                    explanation.ChangeNode.ChangeTree != null &&
                    explanation.ChangeNode.ChangeTree.PullRequestSnapshot != null &&
                    explanation.ChangeNode.ChangeTree.PullRequestSnapshot.TenantId == tenantContext.TenantId)
                .OrderByDescending(explanation => explanation.CreatedAt)
                .Select(explanation => new ExplanationResponse(
                    explanation.Id,
                    explanation.Model,
                    explanation.Prompt,
                    explanation.Response,
                    explanation.CreatedAt))
                .ToListAsync();

            return Results.Ok(explanations);
        });

        return app;
    }
}
