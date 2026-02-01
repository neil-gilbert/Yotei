using System.Threading;
using Microsoft.Extensions.Options;
using Yotei.Api.Features.Flow.Inference;

namespace Api.IntegrationTests;

public class FlowInferenceAdapterTests
{
    // Ensures repo-language configuration selects the matching adapter.
    [Fact]
    public void Registry_Selects_Adapter_By_RepoLanguage()
    {
        var adapters = new IFlowInferenceAdapter[]
        {
            new CSharpFlowInferenceAdapter(),
            new JavaScriptFlowInferenceAdapter()
        };
        var options = Options.Create(new FlowInferenceOptions
        {
            RepoLanguages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["acme/payments"] = "csharp"
            }
        });

        var registry = new FlowInferenceAdapterRegistry(adapters, options);
        var adapter = registry.ResolveAdapter("acme/payments", "src/api/PaymentsController.cs");

        Assert.IsType<CSharpFlowInferenceAdapter>(adapter);
    }

    // Ensures path extensions fall back to language inference when no repo mapping exists.
    [Fact]
    public void Registry_Falls_Back_To_Path_Extension()
    {
        var adapters = new IFlowInferenceAdapter[]
        {
            new CSharpFlowInferenceAdapter(),
            new JavaScriptFlowInferenceAdapter()
        };
        var options = Options.Create(new FlowInferenceOptions());

        var registry = new FlowInferenceAdapterRegistry(adapters, options);
        var adapter = registry.ResolveAdapter(null, "src/app/index.ts");

        Assert.IsType<JavaScriptFlowInferenceAdapter>(adapter);
    }

    // Ensures the C# adapter produces entry points and side effects from diff text.
    [Fact]
    public void CSharpAdapter_Infers_Basic_Signals()
    {
        var adapter = new CSharpFlowInferenceAdapter();
        var request = new FlowInferenceRequest(
            "src/api/PaymentsController.cs",
            "@@ -1 +1 @@\n+app.MapGet(\"/payments\", Handle);\n+var client = new HttpClient();\n+await dbContext.SaveChangesAsync();\n",
            adapter.Language);

        var result = adapter.Infer(request, CancellationToken.None);

        Assert.NotEmpty(result.EntryPoints);
        Assert.Contains(result.EntryPoints, item => item.Label.EndsWith("PaymentsController.cs"));
        Assert.Contains(result.SideEffects, item => item.Label == "network");
        Assert.Contains(result.SideEffects, item => item.Label == "db");
    }
}
