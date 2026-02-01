namespace Yotei.Api.Features.Health;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/", () => Results.Ok(new
        {
            service = "api",
            status = "ok"
        }));

        app.MapHealthChecks("/health");

        return app;
    }
}
