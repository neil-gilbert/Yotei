using Microsoft.EntityFrameworkCore;
using Yotei.Api.Data;
using Yotei.Api.Features.Tenancy;

namespace Yotei.Api.Features.Flow;

/// <summary>
/// Defines flow graph endpoints for review sessions.
/// </summary>
public static class FlowEndpoints
{
    /// <summary>
    /// Maps endpoints that expose the static execution flow graph for review sessions.
    /// </summary>
    /// <param name="app">The route builder used to register endpoints.</param>
    /// <returns>The same route builder instance for chaining.</returns>
    public static IEndpointRouteBuilder MapFlowEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/review-sessions/{sessionId:guid}/flow", async (
            Guid sessionId,
            TenantContext tenantContext,
            YoteiDbContext db,
            FlowGraphBuilder builder,
            CancellationToken cancellationToken) =>
        {
            var session = await db.ReviewSessions
                .AsNoTracking()
                .Include(item => item.Nodes)
                .FirstOrDefaultAsync(
                    item => item.Id == sessionId && item.TenantId == tenantContext.TenantId,
                    cancellationToken);

            if (session is null)
            {
                return Results.NotFound(new { error = "review session not found" });
            }

            var graph = builder.Build(session.Nodes);
            var response = new FlowGraphResponse(session.Id, session.CreatedAt, graph.Nodes, graph.Edges);

            return Results.Ok(response);
        });

        return app;
    }
}
