using System.Net.Http.Json;

namespace Api.IntegrationTests;

public class SnapshotEndpointTests
{
    [Fact]
    public async Task IngestSnapshot_CreatesAndListsSnapshot()
    {
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        var request = new
        {
            owner = "acme",
            name = "payments",
            prNumber = 1,
            baseSha = "base",
            headSha = "head",
            defaultBranch = "main",
            source = "fixture",
            title = "Add retry"
        };

        var response = await client.PostAsJsonAsync("/ingest/snapshot", request);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<IngestResponse>();
        Assert.NotNull(payload);
        Assert.True(payload!.Created);
        Assert.NotEqual(Guid.Empty, payload.SnapshotId);

        var list = await client.GetFromJsonAsync<List<SnapshotListItem>>("/snapshots");
        Assert.NotNull(list);
        Assert.Contains(list!, item => item.Id == payload.SnapshotId);
    }

    [Fact]
    public async Task FileChanges_CanBeAddedAndReadFromSnapshotDetail()
    {
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        var snapshotId = await CreateSnapshotAsync(client);

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
                }
            }
        };

        var changeResponse = await client.PostAsJsonAsync($"/snapshots/{snapshotId}/file-changes", changeRequest);
        changeResponse.EnsureSuccessStatusCode();

        var detail = await client.GetFromJsonAsync<SnapshotDetail>($"/snapshots/{snapshotId}");
        Assert.NotNull(detail);
        Assert.Contains(detail!.FileChanges, fc => fc.Path == "src/api/payments.cs");
    }

    [Fact]
    public async Task FileChanges_Filter_ByChangeType()
    {
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        var snapshotId = await CreateSnapshotAsync(client);
        await SeedFileChangesAsync(client, snapshotId);

        var changes = await client.GetFromJsonAsync<List<FileChangeItem>>(
            $"/snapshots/{snapshotId}/file-changes?changeType=added");

        Assert.NotNull(changes);
        Assert.All(changes!, change => Assert.Equal("added", change.ChangeType));
        Assert.Contains(changes!, change => change.Path == "src/jobs/refund.cs");
        Assert.DoesNotContain(changes!, change => change.Path == "src/api/payments.cs");
    }

    [Fact]
    public async Task FileChanges_Filter_ByPathPrefix()
    {
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        var snapshotId = await CreateSnapshotAsync(client);
        await SeedFileChangesAsync(client, snapshotId);

        var changes = await client.GetFromJsonAsync<List<FileChangeItem>>(
            $"/snapshots/{snapshotId}/file-changes?pathPrefix=src/api/");

        Assert.NotNull(changes);
        Assert.All(changes!, change => Assert.StartsWith("src/api/", change.Path));
        Assert.Contains(changes!, change => change.Path == "src/api/payments.cs");
        Assert.DoesNotContain(changes!, change => change.Path == "src/jobs/refund.cs");
    }

    [Fact]
    public async Task FileChanges_Filter_ByChangeType_And_PathPrefix()
    {
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        var snapshotId = await CreateSnapshotAsync(client);
        await SeedFileChangesAsync(client, snapshotId);

        var changes = await client.GetFromJsonAsync<List<FileChangeItem>>(
            $"/snapshots/{snapshotId}/file-changes?changeType=modified&pathPrefix=src/api/");

        Assert.NotNull(changes);
        Assert.Single(changes!);
        Assert.Equal("src/api/payments.cs", changes![0].Path);
        Assert.Equal("modified", changes[0].ChangeType);
    }

    [Fact]
    public async Task FileChanges_Filter_Requires_NonEmpty_Params()
    {
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        var snapshotId = await CreateSnapshotAsync(client);

        var emptyChangeType = await client.GetAsync($"/snapshots/{snapshotId}/file-changes?changeType=");
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, emptyChangeType.StatusCode);

        var emptyPathPrefix = await client.GetAsync($"/snapshots/{snapshotId}/file-changes?pathPrefix=");
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, emptyPathPrefix.StatusCode);
    }

    [Fact]
    public async Task RawDiffUpload_AttachesStorageReference()
    {
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        var snapshotId = await CreateSnapshotAsync(client);

        var uploadRequest = new
        {
            path = "src/api/payments.cs",
            changeType = "modified",
            addedLines = 4,
            deletedLines = 2,
            diff = "@@ -1 +1 @@"
        };

        var uploadResponse = await client.PostAsJsonAsync($"/snapshots/{snapshotId}/file-changes/upload", uploadRequest);
        uploadResponse.EnsureSuccessStatusCode();

        var uploadPayload = await uploadResponse.Content.ReadFromJsonAsync<UploadResponse>();
        Assert.NotNull(uploadPayload);
        Assert.StartsWith("s3://fake/", uploadPayload!.RawDiffRef);

        var detail = await client.GetFromJsonAsync<SnapshotDetail>($"/snapshots/{snapshotId}");
        Assert.NotNull(detail);
        Assert.Contains(detail!.FileChanges, fc => fc.RawDiffRef == uploadPayload.RawDiffRef);
    }

    [Fact]
    public async Task RawDiff_Returns_Content_For_Path()
    {
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        var snapshotId = await CreateSnapshotAsync(client);

        var uploadRequest = new
        {
            path = "src/api/payments.cs",
            changeType = "modified",
            addedLines = 1,
            deletedLines = 0,
            diff = "@@ -1 +1 @@"
        };

        var uploadResponse = await client.PostAsJsonAsync(
            $"/snapshots/{snapshotId}/file-changes/upload",
            uploadRequest);
        uploadResponse.EnsureSuccessStatusCode();

        var diffResponse = await client.GetAsync(
            $"/raw-diffs/{snapshotId}?path=src/api/payments.cs");
        diffResponse.EnsureSuccessStatusCode();

        var diffText = await diffResponse.Content.ReadAsStringAsync();
        Assert.Equal("@@ -1 +1 @@", diffText);
    }

    [Fact]
    public async Task RawDiff_Requires_Path()
    {
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        var snapshotId = await CreateSnapshotAsync(client);
        var response = await client.GetAsync($"/raw-diffs/{snapshotId}?path=");
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RawDiff_Returns_NotFound_When_Path_Missing()
    {
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        var snapshotId = await CreateSnapshotAsync(client);
        var response = await client.GetAsync($"/raw-diffs/{snapshotId}?path=src/missing.cs");
        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task FileChanges_Rejects_Invalid_LineCounts()
    {
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        var snapshotId = await CreateSnapshotAsync(client);
        var changeRequest = new
        {
            changes = new[]
            {
                new
                {
                    path = "src/api/payments.cs",
                    changeType = "modified",
                    addedLines = -1,
                    deletedLines = 0,
                    rawDiffRef = "s3://fake/sample"
                }
            }
        };

        var response = await client.PostAsJsonAsync($"/snapshots/{snapshotId}/file-changes", changeRequest);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task FileChanges_Rejects_Missing_ChangeType()
    {
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        var snapshotId = await CreateSnapshotAsync(client);
        var changeRequest = new
        {
            changes = new[]
            {
                new
                {
                    path = "src/api/payments.cs",
                    changeType = "",
                    addedLines = 1,
                    deletedLines = 0,
                    rawDiffRef = "s3://fake/sample"
                }
            }
        };

        var response = await client.PostAsJsonAsync($"/snapshots/{snapshotId}/file-changes", changeRequest);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Snapshots_Pagination_Works()
    {
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        var first = await CreateSnapshotAsync(client);
        var second = await CreateSnapshotAsync(client);

        var page = await client.GetFromJsonAsync<List<SnapshotListItem>>("/snapshots?limit=1&offset=1");
        Assert.NotNull(page);
        Assert.Single(page!);
        Assert.Contains(page!, item => item.Id == first || item.Id == second);
    }

    [Fact]
    public async Task Snapshots_Pagination_Validates_Params()
    {
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        var badLimit = await client.GetAsync("/snapshots?limit=0");
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, badLimit.StatusCode);

        var badOffset = await client.GetAsync("/snapshots?offset=-1");
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, badOffset.StatusCode);
    }

    [Fact]
    public async Task SnapshotDetail_Can_Omit_FileChanges()
    {
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        var snapshotId = await CreateSnapshotAsync(client);
        await SeedFileChangesAsync(client, snapshotId);

        var detail = await client.GetFromJsonAsync<SnapshotDetail>($"/snapshots/{snapshotId}?includeFileChanges=false");
        Assert.NotNull(detail);
        Assert.Empty(detail!.FileChanges);
    }

    [Fact]
    public async Task RawDiffUpload_Rejects_Invalid_Input()
    {
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        var snapshotId = await CreateSnapshotAsync(client);

        var missingChangeType = new
        {
            path = "src/api/payments.cs",
            changeType = "",
            addedLines = 1,
            deletedLines = 0,
            diff = "@@ -1 +1 @@"
        };

        var missingResponse = await client.PostAsJsonAsync(
            $"/snapshots/{snapshotId}/file-changes/upload",
            missingChangeType);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, missingResponse.StatusCode);

        var negativeLines = new
        {
            path = "src/api/payments.cs",
            changeType = "modified",
            addedLines = -1,
            deletedLines = 0,
            diff = "@@ -1 +1 @@"
        };

        var negativeResponse = await client.PostAsJsonAsync(
            $"/snapshots/{snapshotId}/file-changes/upload",
            negativeLines);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, negativeResponse.StatusCode);
    }

    [Fact]
    public async Task Snapshots_Delete_Removes_Snapshot()
    {
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        var snapshotId = await CreateSnapshotAsync(client);

        var deleteResponse = await client.DeleteAsync($"/snapshots/{snapshotId}");
        Assert.Equal(System.Net.HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getResponse = await client.GetAsync($"/snapshots/{snapshotId}");
        Assert.Equal(System.Net.HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Snapshots_Delete_Returns_NotFound_When_Missing()
    {
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        var response = await client.DeleteAsync($"/snapshots/{Guid.NewGuid()}");
        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<Guid> CreateSnapshotAsync(HttpClient client)
    {
        var request = new
        {
            owner = "acme",
            name = "payments",
            prNumber = 2,
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

    private sealed record IngestResponse(Guid SnapshotId, bool Created);
    private sealed record SnapshotListItem(Guid Id);
    private sealed record SnapshotDetail(List<FileChangeItem> FileChanges);
    private sealed record FileChangeItem(string Path, string ChangeType, string? RawDiffRef);
    private sealed record UploadResponse(string RawDiffRef);
}
