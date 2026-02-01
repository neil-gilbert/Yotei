using System.Net.Http.Json;

namespace Api.IntegrationTests;

public class OrgInsightsEndpointTests
{
    // Ensures org insights return aggregate counts with filters.
    [Fact]
    public async Task OrgInsights_Returns_Aggregates()
    {
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        var snapshotId = await CreateSnapshotAsync(client);
        await UploadDiffAsync(client, snapshotId);

        var buildResponse = await client.PostAsync($"/review-sessions/{snapshotId}/build", null);
        buildResponse.EnsureSuccessStatusCode();

        var response = await client.GetAsync("/insights/org?repo=acme/insights");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<OrgInsightsResponse>();
        Assert.NotNull(payload);
        Assert.True(payload!.ReviewSessionCount > 0);
        Assert.True(payload.ReviewSummaryCount > 0);
        Assert.NotEmpty(payload.Repositories);
        Assert.NotEmpty(payload.RiskTags);
        Assert.NotEmpty(payload.HotPaths);
        Assert.NotEmpty(payload.ReviewVolume);
    }

    // Creates a snapshot for org insights tests.
    private static async Task<Guid> CreateSnapshotAsync(HttpClient client)
    {
        var request = new
        {
            owner = "acme",
            name = "insights",
            prNumber = 66,
            baseSha = "base",
            headSha = Guid.NewGuid().ToString("N"),
            defaultBranch = "main",
            source = "fixture",
            title = "Org insights"
        };

        var response = await client.PostAsJsonAsync("/ingest/snapshot", request);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<IngestResponse>();
        Assert.NotNull(payload);
        return payload!.SnapshotId;
    }

    // Uploads a diff for org insights tests.
    private static async Task UploadDiffAsync(HttpClient client, Guid snapshotId)
    {
        var uploadRequest = new
        {
            path = "src/api/insights.cs",
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
    private sealed record OrgInsightsResponse(
        int ReviewSessionCount,
        int ReviewSummaryCount,
        List<RepoInsightItem> Repositories,
        List<TagCountItem> RiskTags,
        List<TagCountItem> HotPaths,
        List<ReviewVolumeItem> ReviewVolume);
    private sealed record RepoInsightItem(string Owner, string Name, int ReviewSessionCount);
    private sealed record TagCountItem(string Label, int Count);
    private sealed record ReviewVolumeItem(DateTimeOffset Date, int Count);
}
