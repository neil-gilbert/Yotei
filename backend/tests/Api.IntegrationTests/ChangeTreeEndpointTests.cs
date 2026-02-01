using System.Net;
using System.Net.Http.Json;

namespace Api.IntegrationTests;

public class ChangeTreeEndpointTests
{
    [Fact]
    public async Task ChangeTree_Builds_From_FileChanges()
    {
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        var snapshotId = await CreateSnapshotAsync(client);
        await SeedFileChangesAsync(client, snapshotId);

        var buildResponse = await client.PostAsync($"/snapshots/{snapshotId}/change-tree/build", null);
        buildResponse.EnsureSuccessStatusCode();

        var tree = await client.GetFromJsonAsync<ChangeTreeResponse>(
            $"/snapshots/{snapshotId}/change-tree");

        Assert.NotNull(tree);
        Assert.Equal(snapshotId, tree!.SnapshotId);
        Assert.Equal(3, tree.Nodes.Count);
        Assert.Single(tree.Nodes, node => node.NodeType == "root");
        Assert.Equal(2, tree.Nodes.Count(node => node.NodeType == "file"));
    }

    [Fact]
    public async Task ChangeTree_Rebuild_Replaces_Nodes()
    {
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        var snapshotId = await CreateSnapshotAsync(client);
        await SeedFileChangesAsync(client, snapshotId);

        var buildResponse = await client.PostAsync($"/snapshots/{snapshotId}/change-tree/build", null);
        buildResponse.EnsureSuccessStatusCode();

        await AppendFileChangeAsync(client, snapshotId, "src/new/extra.cs");

        var rebuildResponse = await client.PostAsync($"/snapshots/{snapshotId}/change-tree/build", null);
        rebuildResponse.EnsureSuccessStatusCode();

        var tree = await client.GetFromJsonAsync<ChangeTreeResponse>(
            $"/snapshots/{snapshotId}/change-tree");

        Assert.NotNull(tree);
        Assert.Equal(4, tree!.Nodes.Count);
        Assert.Equal(3, tree.Nodes.Count(node => node.NodeType == "file"));
    }

    [Fact]
    public async Task ChangeTree_Builds_Hunk_Nodes_From_RawDiff()
    {
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        var snapshotId = await CreateSnapshotAsync(client);

        var uploadRequest = new
        {
            path = "src/api/payments.cs",
            changeType = "modified",
            addedLines = 2,
            deletedLines = 0,
            diff = "@@ -1 +1 @@\n+line1\n@@ -10 +11 @@\n+line2\n"
        };

        var uploadResponse = await client.PostAsJsonAsync(
            $"/snapshots/{snapshotId}/file-changes/upload",
            uploadRequest);
        uploadResponse.EnsureSuccessStatusCode();

        var buildResponse = await client.PostAsync($"/snapshots/{snapshotId}/change-tree/build", null);
        buildResponse.EnsureSuccessStatusCode();

        var tree = await client.GetFromJsonAsync<ChangeTreeResponse>(
            $"/snapshots/{snapshotId}/change-tree");

        Assert.NotNull(tree);
        Assert.Equal(2, tree!.Nodes.Count(node => node.NodeType == "hunk"));
    }

    [Fact]
    public async Task ChangeTree_Persists_Explanations_For_Nodes()
    {
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        var snapshotId = await CreateSnapshotAsync(client);
        await SeedFileChangesAsync(client, snapshotId);

        var buildResponse = await client.PostAsync($"/snapshots/{snapshotId}/change-tree/build", null);
        buildResponse.EnsureSuccessStatusCode();

        var tree = await client.GetFromJsonAsync<ChangeTreeResponse>(
            $"/snapshots/{snapshotId}/change-tree");

        Assert.NotNull(tree);
        var nodeId = tree!.Nodes.First().Id;

        var explanations = await client.GetFromJsonAsync<List<ExplanationResponse>>(
            $"/change-nodes/{nodeId}/explanations");

        Assert.NotNull(explanations);
        Assert.NotEmpty(explanations!);
    }

    [Fact]
    public async Task ChangeTree_Build_Returns_NotFound_For_Missing_Snapshot()
    {
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsync($"/snapshots/{Guid.NewGuid()}/change-tree/build", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ChangeTree_Get_Returns_NotFound_When_Missing()
    {
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        var snapshotId = await CreateSnapshotAsync(client);
        var response = await client.GetAsync($"/snapshots/{snapshotId}/change-tree");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<Guid> CreateSnapshotAsync(HttpClient client)
    {
        var request = new
        {
            owner = "acme",
            name = "payments",
            prNumber = 10,
            baseSha = "base",
            headSha = Guid.NewGuid().ToString("N"),
            defaultBranch = "main",
            source = "fixture",
            title = "Seed"
        };

        var response = await client.PostAsJsonAsync("/ingest/snapshot", request);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<IngestResponse>();
        Assert.NotNull(payload);
        return payload!.SnapshotId;
    }

    private static async Task SeedFileChangesAsync(HttpClient client, Guid snapshotId)
    {
        var changeRequest = new
        {
            changes = new[]
            {
                new
                {
                    path = "src/api/payments.cs",
                    changeType = "modified",
                    addedLines = 5,
                    deletedLines = 1,
                    rawDiffRef = "s3://fake/sample"
                },
                new
                {
                    path = "src/jobs/refund.cs",
                    changeType = "added",
                    addedLines = 12,
                    deletedLines = 0,
                    rawDiffRef = "s3://fake/refund"
                }
            }
        };

        var response = await client.PostAsJsonAsync($"/snapshots/{snapshotId}/file-changes", changeRequest);
        response.EnsureSuccessStatusCode();
    }

    private static async Task AppendFileChangeAsync(HttpClient client, Guid snapshotId, string path)
    {
        var changeRequest = new
        {
            changes = new[]
            {
                new
                {
                    path,
                    changeType = "added",
                    addedLines = 3,
                    deletedLines = 0,
                    rawDiffRef = "s3://fake/extra"
                }
            }
        };

        var response = await client.PostAsJsonAsync($"/snapshots/{snapshotId}/file-changes", changeRequest);
        response.EnsureSuccessStatusCode();
    }

    private sealed record IngestResponse(Guid SnapshotId, bool Created);
    private sealed record ChangeTreeResponse(Guid TreeId, Guid SnapshotId, List<ChangeNode> Nodes);
    private sealed record ChangeNode(Guid Id, string NodeType);
    private sealed record ExplanationResponse(string Response);
}
