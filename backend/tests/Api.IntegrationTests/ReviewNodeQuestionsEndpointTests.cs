using System.Net.Http.Json;

namespace Api.IntegrationTests;

public class ReviewNodeQuestionsEndpointTests
{
    // Builds a review and ensures reviewer questions are persisted.
    [Fact]
    public async Task ReviewNodeQuestions_Returns_Items_For_File_Node()
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

        var questions = await client.GetFromJsonAsync<ReviewNodeQuestionsResponse>(
            $"/review-nodes/{fileNode!.Id}/questions");
        Assert.NotNull(questions);
        Assert.NotEmpty(questions!.Items);
        Assert.False(string.IsNullOrWhiteSpace(questions.Source));
    }

    // Creates a snapshot for reviewer questions tests.
    private static async Task<Guid> CreateSnapshotAsync(HttpClient client)
    {
        var request = new
        {
            owner = "acme",
            name = "payments",
            prNumber = 22,
            baseSha = "base",
            headSha = Guid.NewGuid().ToString("N"),
            defaultBranch = "main",
            source = "fixture",
            title = "Questions test"
        };

        var response = await client.PostAsJsonAsync("/ingest/snapshot", request);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<IngestResponse>();
        Assert.NotNull(payload);
        return payload!.SnapshotId;
    }

    // Uploads a diff with risk keywords for questions.
    private static async Task UploadDiffAsync(HttpClient client, Guid snapshotId)
    {
        var uploadRequest = new
        {
            path = "src/api/questions.cs",
            changeType = "modified",
            addedLines = 3,
            deletedLines = 1,
            diff = "@@ -1 +1 @@\n+token\n+await httpClient.PostAsync(\"https://api.stripe.com/charge\", payload);\n+await db.SaveChangesAsync();\n"
        };

        var response = await client.PostAsJsonAsync(
            $"/snapshots/{snapshotId}/file-changes/upload",
            uploadRequest);
        response.EnsureSuccessStatusCode();
    }

    private sealed record IngestResponse(Guid SnapshotId, bool Created);
    private sealed record ReviewTreeResponse(List<ReviewNode> Nodes);
    private sealed record ReviewNode(Guid Id, string NodeType);
    private sealed record ReviewNodeQuestionsResponse(List<string> Items, string Source);
}
