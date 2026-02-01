namespace Yotei.Api.Features.Storage;

public static class RawDiffValidation
{
    public static List<string> Validate(this RawDiffUploadRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.Path))
        {
            errors.Add("path is required");
        }

        if (string.IsNullOrWhiteSpace(request.ChangeType))
        {
            errors.Add("changeType is required");
        }

        if (request.AddedLines < 0)
        {
            errors.Add("addedLines must be greater than or equal to 0");
        }

        if (request.DeletedLines < 0)
        {
            errors.Add("deletedLines must be greater than or equal to 0");
        }

        if (string.IsNullOrWhiteSpace(request.Diff))
        {
            errors.Add("diff is required");
        }

        return errors;
    }
}
