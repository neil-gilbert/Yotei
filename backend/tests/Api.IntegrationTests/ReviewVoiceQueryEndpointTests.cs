using System.Net.Http.Json;

namespace Api.IntegrationTests;

public class ReviewVoiceQueryEndpointTests
{
    // Ensures voice queries are persisted and retrievable per session.
    [Fact]
    public async Task ReviewVoiceQuery_Persists_Transcript()
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

        var voiceRequest = new VoiceQueryRequest("What should I double-check?", null);
        var voiceResponse = await client.PostAsJsonAsync(
            $"/review-nodes/{fileNode!.Id}/voice-query",
            voiceRequest);
        voiceResponse.EnsureSuccessStatusCode();

        var voicePayload = await voiceResponse.Content.ReadFromJsonAsync<VoiceQueryResponse>();
        Assert.NotNull(voicePayload);
        Assert.False(string.IsNullOrWhiteSpace(voicePayload!.Answer));

        var transcript = await client.GetFromJsonAsync<TranscriptResponse>(
            $"/review-sessions/{snapshotId}/transcript");
        Assert.NotNull(transcript);
        Assert.NotEmpty(transcript!.Entries);
        Assert.Contains(transcript.Entries, entry => entry.ReviewNodeId == fileNode.Id);
    }

    // Creates a snapshot for voice query tests.
    private static async Task<Guid> CreateSnapshotAsync(HttpClient client)
    {
        var request = new
        {
            owner = "acme",
            name = "voice",
            prNumber = 44,
            baseSha = "base",
            headSha = Guid.NewGuid().ToString("N"),
            defaultBranch = "main",
            source = "fixture",
            title = "Voice query"
        };

        var response = await client.PostAsJsonAsync("/ingest/snapshot", request);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<IngestResponse>();
        Assert.NotNull(payload);
        return payload!.SnapshotId;
    }

    // Uploads a diff for voice query tests.
    private static async Task UploadDiffAsync(HttpClient client, Guid snapshotId)
    {
        var uploadRequest = new
        {
            path = "src/api/voice.cs",
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
    private sealed record VoiceQueryRequest(string Question, string? Transcript);
    private sealed record VoiceQueryResponse(string Answer);
    private sealed record TranscriptResponse(List<TranscriptEntry> Entries);
    private sealed record TranscriptEntry(Guid ReviewNodeId);
}
