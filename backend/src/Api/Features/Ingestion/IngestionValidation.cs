namespace Yotei.Api.Features.Ingestion;

public static class IngestionValidation
{
    // Validates GitHub ingestion request parameters.
    public static List<string> Validate(this GitHubIngestRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.Owner))
        {
            errors.Add($"{nameof(GitHubIngestRequest.Owner)} is required");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors.Add($"{nameof(GitHubIngestRequest.Name)} is required");
        }

        if (request.PrNumber <= 0)
        {
            errors.Add($"{nameof(GitHubIngestRequest.PrNumber)} must be greater than zero");
        }

        return errors;
    }

    // Validates manual snapshot ingestion request parameters.
    public static List<string> Validate(this IngestSnapshotRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.Owner))
        {
            errors.Add($"{nameof(IngestSnapshotRequest.Owner)} is required");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors.Add($"{nameof(IngestSnapshotRequest.Name)} is required");
        }

        if (request.PrNumber <= 0)
        {
            errors.Add($"{nameof(IngestSnapshotRequest.PrNumber)} must be greater than zero");
        }

        if (string.IsNullOrWhiteSpace(request.BaseSha))
        {
            errors.Add($"{nameof(IngestSnapshotRequest.BaseSha)} is required");
        }

        if (string.IsNullOrWhiteSpace(request.HeadSha))
        {
            errors.Add($"{nameof(IngestSnapshotRequest.HeadSha)} is required");
        }

        return errors;
    }
}
