using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpsPilot.Web.Models;
using OpsPilot.Web.Services;

namespace OpsPilot.Tests;

public class AnalyzerTests
{
    private static readonly IncidentRequest Request = new("checkout-api", "demo", "HTTP 500\nTaxMapper failed");
    internal static IncidentAssessment Valid => new(IncidentSeverity.High, "Checkout errors observed.",
        ["[L1] HTTP 500", "[L2] TaxMapper failed"], ["Tax mapping may be failing [L2]."],
        ["Review the tax mapping change."], true, "Checkout errors are under investigation. No action taken.");

    [Fact]
    public async Task Requests_native_schema_includes_context_and_forces_approval()
    {
        var client = new FakeChatClient((_, _, _) => Task.FromResult(Response(Valid with { RequiresHumanApproval = false })));
        using var gate = new AnalysisGate();
        var result = await Create(client, gate).AnalyzeAsync(Request);
        Assert.Equal(JsonSerializer.Serialize(Valid), JsonSerializer.Serialize(result));
        Assert.True(result.RequiresHumanApproval);
        Assert.IsType<ChatResponseFormatJson>(client.Options!.ResponseFormat);
        Assert.Contains("Runbook", client.Messages![1].Text);
    }

    [Theory]
    [InlineData("not JSON")]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("{\"Severity\":\"Critical\"}")]
    public async Task Parse_failures_return_safe_fallback(string json)
    {
        using var gate = new AnalysisGate();
        var analyzer = Create(new FakeChatClient((_, _, _) => Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, json)))), gate);
        AssertFallback(await analyzer.AnalyzeAsync(Request));
    }

    [Theory]
    [InlineData("evidence")]
    [InlineData("cause")]
    [InlineData("uncited")]
    [InlineData("severity")]
    [InlineData("null-array")]
    [InlineData("empty-summary")]
    [InlineData("too-long")]
    public async Task Invalid_assessments_return_safe_fallback(string defect)
    {
        var bad = defect switch
        {
            "evidence" => Valid with { Evidence = ["[L1] All systems down"] },
            "cause" => Valid with { LikelyCauses = ["A deployment caused this [L99]."] },
            "uncited" => Valid with { LikelyCauses = ["A deployment caused this."] },
            "severity" => Valid with { Severity = (IncidentSeverity)99 },
            "null-array" => Valid with { Evidence = null! },
            "empty-summary" => Valid with { Summary = "" },
            _ => Valid with { StakeholderUpdate = new string('x', 1201) }
        };
        using var gate = new AnalysisGate();
        AssertFallback(await Create(new FakeChatClient((_, _, _) => Task.FromResult(Response(bad))), gate).AnalyzeAsync(Request));
    }

    [Fact]
    public async Task Truncated_output_is_rejected_even_if_it_contains_valid_json()
    {
        using var gate = new AnalysisGate();
        var client = new FakeChatClient((_, _, _) => Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, JsonSerializer.Serialize(Valid))) { FinishReason = ChatFinishReason.Length }));
        AssertFallback(await Create(client, gate).AnalyzeAsync(Request));
    }

    [Fact]
    public async Task Schema_requires_all_seven_fields_and_disallows_additional_fields()
    {
        using var gate = new AnalysisGate();
        var client = new FakeChatClient((_, _, _) => Task.FromResult(Response(Valid)));
        await Create(client, gate).AnalyzeAsync(Request);
        var format = Assert.IsType<ChatResponseFormatJson>(client.Options!.ResponseFormat);
        var schema = format.Schema!.Value;
        Assert.Equal(7, schema.GetProperty("required").GetArrayLength());
        Assert.False(schema.GetProperty("additionalProperties").GetBoolean());
    }

    [Fact]
    public async Task Ai_failure_never_exposes_exception_details_in_result_or_logs()
    {
        const string sensitive = "secret-token endpoint.example deployment-private";
        using var gate = new AnalysisGate();
        var logger = new CapturingLogger<FoundryIncidentAnalyzer>();
        var analyzer = Create(new FakeChatClient((_, _, _) => throw new HttpRequestException(sensitive)), gate, logger);
        var result = await analyzer.AnalyzeAsync(Request);
        AssertFallback(result);
        Assert.DoesNotContain(sensitive, JsonSerializer.Serialize(result));
        Assert.DoesNotContain(sensitive, string.Join('\n', logger.Entries));
        Assert.Contains("HttpRequestException", string.Join('\n', logger.Entries));
    }

    [Fact]
    public async Task Timeout_returns_fallback_but_caller_cancellation_propagates()
    {
        using var gate = new AnalysisGate();
        var client = new FakeChatClient(async (_, _, token) => { await Task.Delay(Timeout.Infinite, token); return Response(Valid); });
        var analyzer = Create(client, gate);
        analyzer.RequestTimeout = TimeSpan.FromMilliseconds(30);
        AssertFallback(await analyzer.AnalyzeAsync(Request));
        using var cancel = new CancellationTokenSource(10);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => analyzer.AnalyzeAsync(Request, cancel.Token));
    }

    [Fact]
    public async Task Missing_configuration_returns_fallback()
    {
        using var gate = new AnalysisGate();
        using var client = AzureChatClientFactory.Create(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        AssertFallback(await Create(client, gate).AnalyzeAsync(Request));
    }

    [Fact]
    public async Task Context_failure_returns_fallback_without_calling_model()
    {
        using var gate = new AnalysisGate();
        var client = new FakeChatClient((_, _, _) => throw new Xunit.Sdk.XunitException("Model should not be called"));
        var analyzer = new FoundryIncidentAnalyzer(client, new BrokenContext(), new EmptyContext(), new(), gate, NullLogger<FoundryIncidentAnalyzer>.Instance);
        AssertFallback(await analyzer.AnalyzeAsync(Request));
        Assert.Null(client.Messages);
    }

    [Fact]
    public async Task Invalid_input_is_rejected_before_model_call()
    {
        using var gate = new AnalysisGate();
        var client = new FakeChatClient((_, _, _) => Task.FromResult(Response(Valid)));
        await Assert.ThrowsAsync<ArgumentException>(() => Create(client, gate).AnalyzeAsync(Request with { Logs = "" }));
        Assert.Null(client.Messages);
    }

    private static FoundryIncidentAnalyzer Create(IChatClient client, AnalysisGate gate, ILogger<FoundryIncidentAnalyzer>? logger = null) =>
        new(client, new EmptyContext(), new EmptyContext(), new(), gate, logger ?? NullLogger<FoundryIncidentAnalyzer>.Instance);

    private static ChatResponse Response(IncidentAssessment assessment) =>
        new(new ChatMessage(ChatRole.Assistant, JsonSerializer.Serialize(assessment))) { FinishReason = ChatFinishReason.Stop };

    private static void AssertFallback(IncidentAssessment result)
    {
        Assert.True(result.RequiresHumanApproval);
        Assert.Empty(result.Evidence);
        Assert.Empty(result.LikelyCauses);
        Assert.Contains("unavailable", result.Summary);
        Assert.Contains("not an inferred severity", result.Summary);
        Assert.NotEmpty(result.RecommendedActions);
    }

    private sealed class EmptyContext : IRunbookService, IDeploymentHistoryService
    {
        public Task<Runbook?> GetRunbookAsync(string serviceName, CancellationToken cancellationToken = default) => Task.FromResult<Runbook?>(null);
        public Task<IReadOnlyList<Deployment>> GetRecentDeploymentsAsync(string serviceName, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Deployment>>([]);
    }
    private sealed class BrokenContext : IRunbookService
    {
        public Task<Runbook?> GetRunbookAsync(string serviceName, CancellationToken cancellationToken = default) => throw new IOException("private path");
    }
}

internal sealed class FakeChatClient(Func<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken, Task<ChatResponse>> respond) : IChatClient
{
    public ChatOptions? Options { get; private set; }
    public ChatMessage[]? Messages { get; private set; }
    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        Messages = messages.ToArray(); Options = options;
        return respond(Messages, options, cancellationToken);
    }
    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public object? GetService(Type serviceType, object? serviceKey = null) => null;
    public void Dispose() { }
}

internal sealed class CapturingLogger<T> : ILogger<T>
{
    public List<string> Entries { get; } = [];
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => Entries.Add(formatter(state, exception) + exception?.ToString());
}
