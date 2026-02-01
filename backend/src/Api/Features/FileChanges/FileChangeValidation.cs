namespace Yotei.Api.Features.FileChanges;

public static class FileChangeValidation
{
    public static List<string> Validate(this FileChangeBatchRequest request)
    {
        var errors = new List<string>();

        if (request.Changes is null || request.Changes.Count == 0)
        {
            errors.Add("changes must contain at least one item");
            return errors;
        }

        foreach (var change in request.Changes)
        {
            if (string.IsNullOrWhiteSpace(change.Path))
            {
                errors.Add("change path is required");
                break;
            }

            if (string.IsNullOrWhiteSpace(change.ChangeType))
            {
                errors.Add("changeType is required");
                break;
            }

            if (change.AddedLines < 0)
            {
                errors.Add("addedLines must be greater than or equal to 0");
                break;
            }

            if (change.DeletedLines < 0)
            {
                errors.Add("deletedLines must be greater than or equal to 0");
                break;
            }
        }

        return errors;
    }

}
