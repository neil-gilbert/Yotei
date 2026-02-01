using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace Yotei.Api.Storage;

public interface IRawDiffStorage
{
    Task<string> StoreDiffAsync(Guid snapshotId, string path, string diff, CancellationToken cancellationToken);
    Task<string?> GetDiffAsync(string rawDiffRef, CancellationToken cancellationToken);
}

public class S3RawDiffStorage : IRawDiffStorage
{
    private readonly StorageSettings _settings;
    private readonly IAmazonS3 _s3Client;

    public S3RawDiffStorage(IOptions<StorageSettings> settings)
    {
        _settings = settings.Value;
        _s3Client = CreateClient(_settings);
    }

    public async Task<string> StoreDiffAsync(Guid snapshotId, string path, string diff, CancellationToken cancellationToken)
    {
        var key = BuildKey(snapshotId, path);

        var request = new PutObjectRequest
        {
            BucketName = _settings.S3Bucket,
            Key = key,
            ContentBody = diff,
            ContentType = "text/plain"
        };

        await _s3Client.PutObjectAsync(request, cancellationToken);

        return $"s3://{_settings.S3Bucket}/{key}";
    }

    public async Task<string?> GetDiffAsync(string rawDiffRef, CancellationToken cancellationToken)
    {
        if (!TryParseS3Uri(rawDiffRef, out var bucket, out var key))
        {
            return null;
        }

        try
        {
            var response = await _s3Client.GetObjectAsync(bucket, key, cancellationToken);
            using var reader = new StreamReader(response.ResponseStream);
            return await reader.ReadToEndAsync(cancellationToken);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private static string BuildKey(Guid snapshotId, string path)
    {
        var normalized = path.Replace('\\', '/').Trim();
        normalized = normalized.TrimStart('/');
        normalized = normalized.Replace("..", "_");
        return $"snapshots/{snapshotId}/{normalized}.diff";
    }

    private static bool TryParseS3Uri(string rawDiffRef, out string bucket, out string key)
    {
        bucket = string.Empty;
        key = string.Empty;

        if (string.IsNullOrWhiteSpace(rawDiffRef) || !rawDiffRef.StartsWith("s3://", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var remainder = rawDiffRef[5..];
        var separatorIndex = remainder.IndexOf('/');
        if (separatorIndex <= 0 || separatorIndex == remainder.Length - 1)
        {
            return false;
        }

        bucket = remainder[..separatorIndex];
        key = remainder[(separatorIndex + 1)..];
        return true;
    }

    private static IAmazonS3 CreateClient(StorageSettings settings)
    {
        var config = new AmazonS3Config
        {
            ServiceURL = settings.Endpoint,
            ForcePathStyle = true,
            AuthenticationRegion = settings.Region
        };

        var credentials = new BasicAWSCredentials(settings.AccessKey, settings.SecretKey);
        return new AmazonS3Client(credentials, config);
    }
}

public record StorageSettings
{
    public string Provider { get; init; } = "S3";
    public string Endpoint { get; init; } = "http://localhost:4566";
    public string S3Bucket { get; init; } = "yotei-artifacts";
    public string AccessKey { get; init; } = "test";
    public string SecretKey { get; init; } = "test";
    public string Region { get; init; } = "us-east-1";
}
