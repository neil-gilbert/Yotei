using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Yotei.Api.Features.ReviewSessions;
using Yotei.Api.Infrastructure;
using Yotei.Api.Models;
using Yotei.Api.Storage;

namespace Api.IntegrationTests;

public class ReviewSummaryOverviewGeneratorTests
{
    // Uses OpenAI result when the client returns a structured summary.
    [Fact]
    public async Task ReviewSummaryOverviewGenerator_Uses_OpenAi_When_Available()
    {
        var openAiClient = new StubOpenAiClient(new OpenAiReviewSummary(
            "Overall summary from OpenAI.",
            "Before state from OpenAI.",
            "After state from OpenAI."));
        var storage = new StubRawDiffStorage("@@ -1 +1 @@\n+added line");
        var settings = Options.Create(new OpenAiSettings
        {
            ApiKey = "test-key",
            Model = "test-model"
        });
        var generator = new ReviewSummaryOverviewGenerator(
            openAiClient,
            storage,
            settings,
            NullLogger<ReviewSummaryOverviewGenerator>.Instance);

        var snapshot = new PullRequestSnapshot
        {
            Id = Guid.NewGuid(),
            PrNumber = 42,
            Title = "Update billing flow",
            Repository = new Repository
            {
                Owner = "acme",
                Name = "payments"
            },
            FileChanges =
            [
                new FileChange
                {
                    Path = "src/api/billing.cs",
                    ChangeType = "modified",
                    RawDiffText = "@@ -1 +1 @@\n+added line",
                    AddedLines = 1,
                    DeletedLines = 0
                }
            ]
        };
        var summary = new ReviewSummary
        {
            ReviewSessionId = snapshot.Id,
            ChangedFilesCount = 1,
            NewFilesCount = 0,
            ModifiedFilesCount = 1,
            DeletedFilesCount = 0,
            EntryPoints = ["src/api"],
            SideEffects = [],
            RiskTags = [],
            TopPaths = ["src/api/billing.cs"]
        };

        var result = await generator.GenerateAsync(snapshot, summary, CancellationToken.None);

        Assert.Equal("Overall summary from OpenAI.", result.OverallSummary);
        Assert.Equal("Before state from OpenAI.", result.BeforeState);
        Assert.Equal("After state from OpenAI.", result.AfterState);
    }

    // Falls back to deterministic summary text when OpenAI is not configured.
    [Fact]
    public async Task ReviewSummaryOverviewGenerator_Falls_Back_When_OpenAi_Not_Configured()
    {
        var openAiClient = new StubOpenAiClient(null);
        var storage = new StubRawDiffStorage("@@ -1 +1 @@\n+added line");
        var settings = Options.Create(new OpenAiSettings());
        var generator = new ReviewSummaryOverviewGenerator(
            openAiClient,
            storage,
            settings,
            NullLogger<ReviewSummaryOverviewGenerator>.Instance);

        var snapshot = new PullRequestSnapshot
        {
            Id = Guid.NewGuid(),
            PrNumber = 101,
            Title = "Add retries",
            Repository = new Repository
            {
                Owner = "luna",
                Name = "ops-console"
            },
            FileChanges =
            [
                new FileChange
                {
                    Path = "src/retries/handler.ts",
                    ChangeType = "modified",
                    RawDiffText = "@@ -1 +1 @@\n+added line",
                    AddedLines = 1,
                    DeletedLines = 0
                }
            ]
        };
        var summary = new ReviewSummary
        {
            ReviewSessionId = snapshot.Id,
            ChangedFilesCount = 1,
            NewFilesCount = 0,
            ModifiedFilesCount = 1,
            DeletedFilesCount = 0,
            EntryPoints = ["src/retries/handler.ts"],
            SideEffects = ["queue"],
            RiskTags = ["retries"],
            TopPaths = ["src/retries/handler.ts"]
        };

        var result = await generator.GenerateAsync(snapshot, summary, CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(result.OverallSummary));
        Assert.False(string.IsNullOrWhiteSpace(result.BeforeState));
        Assert.False(string.IsNullOrWhiteSpace(result.AfterState));
    }

    private sealed class StubOpenAiClient : IOpenAiClient
    {
        private readonly OpenAiReviewSummary? _summary;

        // Creates a stub OpenAI client with a deterministic response.
        public StubOpenAiClient(OpenAiReviewSummary? summary)
        {
            _summary = summary;
        }

        // Returns no behavior summary for this stub.
        public Task<OpenAiBehaviourSummary?> GenerateBehaviourSummaryAsync(
            OpenAiBehaviourSummaryPrompt prompt,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<OpenAiBehaviourSummary?>(null);
        }

        // Returns no reviewer questions for this stub.
        public Task<OpenAiReviewerQuestions?> GenerateReviewerQuestionsAsync(
            OpenAiReviewerQuestionsPrompt prompt,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<OpenAiReviewerQuestions?>(null);
        }

        // Returns the predefined review summary.
        public Task<OpenAiReviewSummary?> GenerateReviewSummaryAsync(
            OpenAiReviewSummaryPrompt prompt,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_summary);
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
