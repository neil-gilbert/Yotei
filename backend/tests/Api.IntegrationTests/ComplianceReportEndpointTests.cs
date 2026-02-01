using System.Net.Http.Json;

namespace Api.IntegrationTests;

public class ComplianceReportEndpointTests
{
    // Ensures compliance reports include risk tags, checklist items, and transcript highlights.
    [Fact]
    public async Task ComplianceReport_Returns_Report_Content()
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

        var voiceRequest = new VoiceQueryRequest("What should I verify?", null);
        var voiceResponse = await client.PostAsJsonAsync(
            $"/review-nodes/{fileNode!.Id}/voice-query",
            voiceRequest);
        voiceResponse.EnsureSuccessStatusCode();

        var response = await client.GetAsync($"/review-sessions/{snapshotId}/compliance-report");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<ComplianceReportResponse>();
        Assert.NotNull(payload);
        Assert.NotNull(payload!.Summary);
        Assert.NotEmpty(payload.RiskTags);
        Assert.NotNull(payload.Checklist);
        Assert.NotEmpty(payload.Checklist.Items);
        Assert.True(payload.Transcript.TotalEntries > 0);
        Assert.NotEmpty(payload.TranscriptHighlights);
    }

    // Creates a snapshot for compliance report tests.
    private static async Task<Guid> CreateSnapshotAsync(HttpClient client)
    {
        var request = new
        {
            owner = "acme",
            name = "compliance",
            prNumber = 91,
            baseSha = "base",
            headSha = Guid.NewGuid().ToString("N"),
            defaultBranch = "main",
            source = "fixture",
            title = "Compliance report"
        };

        var response = await client.PostAsJsonAsync("/ingest/snapshot", request);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<IngestResponse>();
        Assert.NotNull(payload);
        return payload!.SnapshotId;
    }

    // Uploads a diff for compliance report tests.
    private static async Task UploadDiffAsync(HttpClient client, Guid snapshotId)
    {
        var uploadRequest = new
        {
            path = "src/api/compliance.cs",
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
    private sealed record ComplianceReportResponse(
        ComplianceSummary Summary,
        List<string> RiskTags,
        ComplianceChecklistSummary Checklist,
        ComplianceTranscriptSummary Transcript,
        List<ComplianceTranscriptExcerpt> TranscriptHighlights);
    private sealed record ComplianceSummary(int ChangedFilesCount);
    private sealed record ComplianceChecklistSummary(int TotalItems, List<ChecklistItem> Items);
    private sealed record ChecklistItem(string Text, string Source);
    private sealed record ComplianceTranscriptSummary(int TotalEntries);
    private sealed record ComplianceTranscriptExcerpt(Guid TranscriptId);
}
