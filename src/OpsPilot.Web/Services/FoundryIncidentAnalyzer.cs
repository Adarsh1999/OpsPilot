using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;
using OpsPilot.Web.Models;

namespace OpsPilot.Web.Services;

public sealed class FoundryIncidentAnalyzer(IChatClient client, IRunbookService runbooks,
    IDeploymentHistoryService deployments, IncidentPromptBuilder prompts, AnalysisGate gate,
    ILogger<FoundryIncidentAnalyzer> logger) : IIncidentAnalyzer
{
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(60);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerOptions.Default)
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public async Task<IncidentAssessment> AnalyzeAsync(IncidentRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (IncidentRequestValidator.Validate(request).Count > 0)
            throw new ArgumentException("Invalid incident input.", nameof(request));
        using var lease = gate.Acquire();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(RequestTimeout);
        var watch = Stopwatch.StartNew();
        var analysisId = Guid.NewGuid();
        logger.LogInformation("Analysis {AnalysisId} started with {LogCharacters} characters", analysisId, request.Logs.Length);
        try
        {
            var runbook = await runbooks.GetRunbookAsync(request.ServiceName, timeout.Token);
            var history = await deployments.GetRecentDeploymentsAsync(request.ServiceName, timeout.Token);
            var response = await client.GetResponseAsync<IncidentAssessment>(
                prompts.Build(request, runbook, history), JsonOptions,
                new ChatOptions { MaxOutputTokens = 2400 },
                useJsonSchemaResponseFormat: true, cancellationToken: timeout.Token);
            if (response.FinishReason is { } reason && reason != ChatFinishReason.Stop)
                throw new JsonException("Incomplete model response.");
            var assessment = response.Result;
            if (!AssessmentSafety.IsGrounded(assessment, request))
                throw new JsonException("Assessment failed evidence validation.");
            logger.LogInformation("Analysis {AnalysisId} completed in {ElapsedMs} ms with severity {Severity}",
                analysisId, watch.ElapsedMilliseconds, assessment.Severity);
            // The model cannot waive the application's approval boundary.
            return assessment with { RequiresHumanApproval = true };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("Analysis {AnalysisId} cancelled", analysisId);
            throw;
        }
        catch (Exception exception)
        {
            // SDK exceptions can contain endpoint, response body, or credentials. Never log them.
            logger.LogWarning("Analysis {AnalysisId} returned fallback after {ElapsedMs} ms; failure type {FailureType}; HTTP status {HttpStatus}",
                analysisId, watch.ElapsedMilliseconds, exception.GetType().Name,
                exception is System.ClientModel.ClientResultException apiError ? apiError.Status : (int?)null);
            return AssessmentSafety.Fallback();
        }
    }
}
