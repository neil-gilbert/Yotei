namespace Yotei.Api.Features.Explanations;

public record ExplanationResponse(
    Guid Id,
    string Model,
    string Prompt,
    string Response,
    DateTimeOffset CreatedAt);
