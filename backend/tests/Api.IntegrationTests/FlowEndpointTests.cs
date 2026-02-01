using System.Net.Http.Json;

namespace Api.IntegrationTests;

public class FlowEndpointTests
{
    // Given a built review session, when the flow graph is requested, then it contains entry and side-effect nodes.
    [Fact]
    public async Task Given_ReviewSession_When_FlowRequested_Then_GraphContainsEntryAndSideEffects()
    {
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        var snapshotId = await CreateSnapshotAsync(client);
        await UploadDiffAsync(client, snapshotId);
        await BuildReviewAsync(client, snapshotId);

        var response = await client.GetFromJsonAsync<FlowGraphResponse>(
            $"/review-sessions/{snapshotId}/flow");

        Assert.NotNull(response);
        Assert.NotEmpty(response!.Nodes);
        Assert.Contains(response.Nodes, node => node.NodeType == "entry");
        Assert.Contains(response.Nodes, node => node.NodeType == "side_effect");
        Assert.NotEmpty(response.Edges);
    }

    // Creates a snapshot for flow graph tests.
    private static async Task<Guid> CreateSnapshotAsync(HttpClient client)
    {
        var request = new
        {
            owner = "acme",
            name = "payments",
            prNumber = 77,
            baseSha = "base",
            headSha = Guid.NewGuid().ToString("N"),
            defaultBranch = "main",
            source = "fixture",
            title = "Flow graph"
        };

        var response = await client.PostAsJsonAsync("/ingest/snapshot", request);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<IngestResponse>();
        Assert.NotNull(payload);
        return payload!.SnapshotId;
    }

    // Uploads a diff containing entry-point and side-effect keywords.
    private static async Task UploadDiffAsync(HttpClient client, Guid snapshotId)
    {
        var uploadRequest = new
        {
            path = "src/api/payments.cs",
            changeType = "modified",
            addedLines = 6,
            deletedLines = 1,
            diff = "@@ -1 +1 @@\n+await httpClient.PostAsync(\"https://api.stripe.com/charge\", payload);\n+await queue.PublishAsync(\"payments\");\n+await db.SaveChangesAsync();\n"
        };

        var response = await client.PostAsJsonAsync(
            $"/snapshots/{snapshotId}/file-changes/upload",
            uploadRequest);
        response.EnsureSuccessStatusCode();
    }

    // Builds the review session to generate review nodes before requesting the flow graph.
    private static async Task BuildReviewAsync(HttpClient client, Guid snapshotId)
    {
        var response = await client.PostAsync($"/review-sessions/{snapshotId}/build", null);
        response.EnsureSuccessStatusCode();
    }

    private sealed record IngestResponse(Guid SnapshotId, bool Created);
    private sealed record FlowGraphResponse(List<FlowNode> Nodes, List<FlowEdge> Edges);
    private sealed record FlowNode(Guid Id, string NodeType, string Label);
    private sealed record FlowEdge(Guid Id, Guid SourceId, Guid TargetId, string Label);
}
