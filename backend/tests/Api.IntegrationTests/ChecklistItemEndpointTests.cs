using System.Net.Http.Json;

namespace Api.IntegrationTests;

public class ChecklistItemEndpointTests
{
    // Adds a conversation checklist item and verifies it is returned with source metadata.
    [Fact]
    public async Task ChecklistItem_Adds_Conversation_Item()
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

        var request = new ChecklistItemRequest("Verify retries are idempotent.", "conversation");
        var response = await client.PostAsJsonAsync(
            $"/review-nodes/{fileNode!.Id}/checklist/items",
            request);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Checklist item request failed: {body}");
        }

        var payload = await response.Content.ReadFromJsonAsync<ChecklistResponse>();
        Assert.NotNull(payload);
        Assert.Contains(payload!.Items, item =>
            string.Equals(item.Text, request.Text, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(item.Source, "conversation", StringComparison.OrdinalIgnoreCase));
    }

    // Creates a snapshot for checklist item tests.
    private static async Task<Guid> CreateSnapshotAsync(HttpClient client)
    {
        var request = new
        {
            owner = "acme",
            name = "checklist",
            prNumber = 55,
            baseSha = "base",
            headSha = Guid.NewGuid().ToString("N"),
            defaultBranch = "main",
            source = "fixture",
            title = "Checklist items"
        };

        var response = await client.PostAsJsonAsync("/ingest/snapshot", request);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<IngestResponse>();
        Assert.NotNull(payload);
        return payload!.SnapshotId;
    }

    // Uploads a diff for checklist item tests.
    private static async Task UploadDiffAsync(HttpClient client, Guid snapshotId)
    {
        var uploadRequest = new
        {
            path = "src/api/checklist.cs",
            changeType = "modified",
            addedLines = 2,
            deletedLines = 1,
            diff = "@@ -1 +1 @@\n+await httpClient.PostAsync(\"https://api.stripe.com/charge\", payload);\n+await db.SaveChangesAsync();\n"
        };

        var response = await client.PostAsJsonAsync(
            $"/snapshots/{snapshotId}/file-changes/upload",
            uploadRequest);
        response.EnsureSuccessStatusCode();
    }

    private sealed record IngestResponse(Guid SnapshotId, bool Created);
    private sealed record ReviewTreeResponse(List<ReviewNode> Nodes);
    private sealed record ReviewNode(Guid Id, string NodeType);
    private sealed record ChecklistItemRequest(string Text, string Source);
    private sealed record ChecklistResponse(List<ChecklistItem> Items);
    private sealed record ChecklistItem(string Text, string Source);
}
