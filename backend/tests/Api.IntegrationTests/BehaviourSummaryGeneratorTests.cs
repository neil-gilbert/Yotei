using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Yotei.Api.Features.ReviewSessions;
using Yotei.Api.Infrastructure;
using Yotei.Api.Models;
using Yotei.Api.Storage;

namespace Api.IntegrationTests;

public class BehaviourSummaryGeneratorTests
{
    // Uses OpenAI result when the client returns a structured summary.
    [Fact]
    public async Task ReviewBehaviourSummaryGenerator_Uses_OpenAi_Summary_When_Available()
    {
        var openAiClient = new StubOpenAiClient(new OpenAiBehaviourSummary(
            "LLM behavior change",
            "Scope: API entry point",
            "Reviewer focus: verify billing logic"));
        var storage = new StubRawDiffStorage("@@ -1 +1 @@\n+added line");
        var settings = Options.Create(new OpenAiSettings
        {
            ApiKey = "test-key",
            Model = "test-model"
        });
        var generator = new ReviewBehaviourSummaryGenerator(
            openAiClient,
            storage,
            settings,
            new ReviewNodeInsightsGenerator(),
            NullLogger<ReviewBehaviourSummaryGenerator>.Instance);

        var node = new ReviewNode
        {
            Id = Guid.NewGuid(),
            Label = "payments.cs",
            Path = "src/api/payments.cs",
            NodeType = "file",
            RiskTags = ["money"],
            Evidence = ["sideEffect:db"]
        };
        var change = new FileChange
        {
            Path = "src/api/payments.cs",
            ChangeType = "modified",
            RawDiffRef = "db://snapshots/demo"
        };

        var summary = await generator.GenerateAsync(node, change, CancellationToken.None);

        Assert.Equal("LLM behavior change", summary.BehaviourChange);
        Assert.Equal("Scope: API entry point", summary.Scope);
        Assert.Equal("Reviewer focus: verify billing logic", summary.ReviewerFocus);
    }

    // Falls back to heuristics when OpenAI is not configured.
    [Fact]
    public async Task ReviewBehaviourSummaryGenerator_Falls_Back_When_OpenAi_Not_Configured()
    {
        var openAiClient = new StubOpenAiClient(null);
        var storage = new StubRawDiffStorage("@@ -1 +1 @@\n+added line");
        var settings = Options.Create(new OpenAiSettings());
        var generator = new ReviewBehaviourSummaryGenerator(
            openAiClient,
            storage,
            settings,
            new ReviewNodeInsightsGenerator(),
            NullLogger<ReviewBehaviourSummaryGenerator>.Instance);

        var node = new ReviewNode
        {
            Id = Guid.NewGuid(),
            Label = "payments.cs",
            Path = "src/api/controller/payments.cs",
            NodeType = "file",
            RiskTags = ["money"],
            Evidence = []
        };
        var change = new FileChange
        {
            Path = "src/api/controller/payments.cs",
            ChangeType = "added",
            RawDiffText = "@@ -1 +1 @@\n+added line"
        };

        var summary = await generator.GenerateAsync(node, change, CancellationToken.None);

        Assert.Contains("Introduces new behavior", summary.BehaviourChange, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Scope: API entry point", summary.Scope, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class StubOpenAiClient : IOpenAiClient
    {
        private readonly OpenAiBehaviourSummary? _summary;

        // Creates a stub OpenAI client with a deterministic response.
        public StubOpenAiClient(OpenAiBehaviourSummary? summary)
        {
            _summary = summary;
        }

        // Returns the predefined summary.
        public Task<OpenAiBehaviourSummary?> GenerateBehaviourSummaryAsync(
            OpenAiBehaviourSummaryPrompt prompt,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_summary);
        }

        // Returns no reviewer questions for this stub.
        public Task<OpenAiReviewerQuestions?> GenerateReviewerQuestionsAsync(
            OpenAiReviewerQuestionsPrompt prompt,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<OpenAiReviewerQuestions?>(null);
        }

        // Returns no review summary for this stub.
        public Task<OpenAiReviewSummary?> GenerateReviewSummaryAsync(
            OpenAiReviewSummaryPrompt prompt,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<OpenAiReviewSummary?>(null);
        }

        // Returns no combined review session response for this stub.
        public Task<OpenAiReviewSessionResponse?> GenerateReviewSessionAsync(
            OpenAiReviewSessionPrompt prompt,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<OpenAiReviewSessionResponse?>(null);
        }
    }

    private sealed class StubRawDiffStorage : IRawDiffStorage
    {
        private readonly string _diff;

        // Creates a stub raw diff storage provider.
        public StubRawDiffStorage(string diff)
        {
            _diff = diff;
        }

        // Throws because storing diffs is not needed for these tests.
        public Task<string> StoreDiffAsync(Guid snapshotId, string path, string diff, CancellationToken cancellationToken)
        {
            throw new NotSupportedException("StoreDiffAsync is not used in these tests.");
        }

        // Returns the predefined diff.
        public Task<string?> GetDiffAsync(string rawDiffRef, CancellationToken cancellationToken)
        {
            return Task.FromResult<string?>(_diff);
        }
    }
}
