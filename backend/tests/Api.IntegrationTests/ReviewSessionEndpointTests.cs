using System.Net;
using System.Net.Http.Json;

namespace Api.IntegrationTests;

public class ReviewSessionEndpointTests
{
    // Builds a review and asserts summary, checklist, and diff are available.
    [Fact]
    public async Task ReviewSession_Builds_Insights_For_File_Node()
    {
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        var snapshotId = await CreateSnapshotAsync(client);
        await UploadDiffAsync(client, snapshotId);

        var buildResponse = await client.PostAsync($"/review-sessions/{snapshotId}/build", null);
        buildResponse.EnsureSuccessStatusCode();

        var summary = await client.GetFromJsonAsync<ReviewSummaryResponse>(
            $"/review-sessions/{snapshotId}/summary");
        Assert.NotNull(summary);
        Assert.True(summary!.ChangedFilesCount > 0);

        var tree = await client.GetFromJsonAsync<ReviewTreeResponse>(
            $"/review-sessions/{snapshotId}/change-tree");
        Assert.NotNull(tree);

        var fileNode = tree!.Nodes.FirstOrDefault(node => node.NodeType == "file");
        Assert.NotNull(fileNode);
        Assert.Contains("money", fileNode!.RiskTags);

        var summaryResponse = await client.GetFromJsonAsync<ReviewBehaviourSummaryResponse>(
            $"/review-nodes/{fileNode.Id}/behaviour-summary");
        Assert.NotNull(summaryResponse);
        Assert.False(string.IsNullOrWhiteSpace(summaryResponse!.BehaviourChange));

        var checklistResponse = await client.GetFromJsonAsync<ReviewChecklistResponse>(
            $"/review-nodes/{fileNode.Id}/checklist");
        Assert.NotNull(checklistResponse);
        Assert.NotEmpty(checklistResponse!.Items);
        Assert.All(checklistResponse.Items, item => Assert.False(string.IsNullOrWhiteSpace(item.Text)));

        var diffResponse = await client.GetAsync($"/review-nodes/{fileNode.Id}/diff");
        diffResponse.EnsureSuccessStatusCode();
        var diffText = await diffResponse.Content.ReadAsStringAsync();
        Assert.Contains("stripe", diffText, StringComparison.OrdinalIgnoreCase);
    }

    // Verifies diff endpoint returns 404 for unknown nodes.
    [Fact]
    public async Task ReviewSession_Returns_NotFound_For_Missing_Node_Diff()
    {
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/review-nodes/{Guid.NewGuid()}/diff");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // Creates a snapshot for review session tests.
    private static async Task<Guid> CreateSnapshotAsync(HttpClient client)
    {
        var request = new
        {
            owner = "acme",
            name = "payments",
            prNumber = 11,
            baseSha = "base",
            headSha = Guid.NewGuid().ToString("N"),
            defaultBranch = "main",
            source = "fixture",
            title = "Review session"
        };

        var response = await client.PostAsJsonAsync("/ingest/snapshot", request);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<IngestResponse>();
        Assert.NotNull(payload);
        return payload!.SnapshotId;
    }

    // Uploads a diff with risk keywords for insights.
    private static async Task UploadDiffAsync(HttpClient client, Guid snapshotId)
    {
        var uploadRequest = new
        {
            path = "src/api/payments.cs",
            changeType = "modified",
            addedLines = 4,
            deletedLines = 1,
            diff = "@@ -1 +1 @@\n+token\n+await httpClient.PostAsync(\"https://api.stripe.com/charge\", payload);\n+await queue.PublishAsync(\"payments\");\n+await db.SaveChangesAsync();\n"
        };

        var response = await client.PostAsJsonAsync(
            $"/snapshots/{snapshotId}/file-changes/upload",
            uploadRequest);
        response.EnsureSuccessStatusCode();
    }

    private sealed record IngestResponse(Guid SnapshotId, bool Created);
    private sealed record ReviewSummaryResponse(int ChangedFilesCount);
    private sealed record ReviewTreeResponse(List<ReviewNode> Nodes);
    private sealed record ReviewNode(Guid Id, string NodeType, List<string> RiskTags);
    private sealed record ReviewBehaviourSummaryResponse(string BehaviourChange);
    private sealed record ReviewChecklistResponse(List<ReviewChecklistItem> Items);
    private sealed record ReviewChecklistItem(string Text, string Source);
}
