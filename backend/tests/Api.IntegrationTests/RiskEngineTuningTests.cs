using System.Net.Http.Json;

namespace Api.IntegrationTests;

public class RiskEngineTuningTests
{
    // Ensures risk severity and line-based evidence are emitted for file nodes.
    [Fact]
    public async Task RiskEngine_Assigns_Severity_And_Line_Evidence()
    {
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        var snapshotId = await CreateSnapshotAsync(client);
        await UploadDiffAsync(client, snapshotId);

        var buildResponse = await client.PostAsync($"/review-sessions/{snapshotId}/build", null);
        buildResponse.EnsureSuccessStatusCode();

        var tree = await client.GetFromJsonAsync<ReviewTreeResponse>(
            $"/review-sessions/{snapshotId}/change-tree");
        Assert.NotNull(tree);

        var fileNode = tree!.Nodes.FirstOrDefault(node => node.NodeType == "file");
        Assert.NotNull(fileNode);
        Assert.Equal("high", fileNode!.RiskSeverity);
        Assert.Contains(fileNode.Evidence, item =>
            item.StartsWith("keyword:stripe@", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(fileNode.Evidence, item =>
            item.StartsWith("hunk:", StringComparison.OrdinalIgnoreCase));
    }

    // Creates a snapshot for risk engine tests.
    private static async Task<Guid> CreateSnapshotAsync(HttpClient client)
    {
        var request = new
        {
            owner = "acme",
            name = "risk",
            prNumber = 33,
            baseSha = "base",
            headSha = Guid.NewGuid().ToString("N"),
            defaultBranch = "main",
            source = "fixture",
            title = "Risk engine tuning"
        };

        var response = await client.PostAsJsonAsync("/ingest/snapshot", request);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<IngestResponse>();
        Assert.NotNull(payload);
        return payload!.SnapshotId;
    }

    // Uploads a diff containing multiple risk keywords and hunk context.
    private static async Task UploadDiffAsync(HttpClient client, Guid snapshotId)
    {
        var uploadRequest = new
        {
            path = "src/api/payments/handler.cs",
            changeType = "modified",
            addedLines = 3,
            deletedLines = 1,
            diff = "@@ -10,1 +10,3 @@\n- var token = \"old\";\n+ var token = \"new\";\n+ await httpClient.PostAsync(\"https://api.stripe.com/charge\", payload);\n+ await queue.PublishAsync(\"payments\");\n"
        };

        var response = await client.PostAsJsonAsync(
            $"/snapshots/{snapshotId}/file-changes/upload",
            uploadRequest);
        response.EnsureSuccessStatusCode();
    }

    private sealed record IngestResponse(Guid SnapshotId, bool Created);
    private sealed record ReviewTreeResponse(List<ReviewNode> Nodes);
    private sealed record ReviewNode(string NodeType, string RiskSeverity, List<string> Evidence);
}
