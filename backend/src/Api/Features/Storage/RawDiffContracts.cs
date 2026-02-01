namespace Yotei.Api.Features.Storage;

public record RawDiffUploadRequest(
    string Path,
    string? ChangeType,
    int AddedLines,
    int DeletedLines,
    string Diff);
