using System.Net.Http.Json;

namespace Api.IntegrationTests;

public class TranscriptExportEndpointTests
{
    // Ensures transcript export returns JSON and CSV formats.
    [Fact]
    public async Task TranscriptExport_Returns_Json_And_Csv()
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

        var jsonExport = await client.GetFromJsonAsync<TranscriptExportResponse>(
            $"/review-sessions/{snapshotId}/transcript/export?format=json");
        Assert.NotNull(jsonExport);
        Assert.NotEmpty(jsonExport!.Entries);
        Assert.Contains(jsonExport.Entries, entry => entry.Question == voiceRequest.Question);

        var csvExport = await client.GetStringAsync(
            $"/review-sessions/{snapshotId}/transcript/export?format=csv");
        Assert.Contains("TranscriptId,ReviewSessionId,ReviewNodeId,Question,Answer,CreatedAt", csvExport);
        Assert.Contains(voiceRequest.Question, csvExport);
    }

    // Creates a snapshot for transcript export tests.
    private static async Task<Guid> CreateSnapshotAsync(HttpClient client)
    {
        var request = new
        {
            owner = "acme",
            name = "transcript",
            prNumber = 88,
            baseSha = "base",
            headSha = Guid.NewGuid().ToString("N"),
            defaultBranch = "main",
            source = "fixture",
            title = "Transcript export"
        };

        var response = await client.PostAsJsonAsync("/ingest/snapshot", request);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<IngestResponse>();
        Assert.NotNull(payload);
        return payload!.SnapshotId;
    }

    // Uploads a diff for transcript export tests.
    private static async Task UploadDiffAsync(HttpClient client, Guid snapshotId)
    {
        var uploadRequest = new
        {
            path = "src/api/transcript.cs",
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
    private sealed record TranscriptExportResponse(List<TranscriptExportEntry> Entries);
    private sealed record TranscriptExportEntry(Guid Id, string Question);
}
