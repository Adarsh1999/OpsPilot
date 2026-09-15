using Microsoft.AspNetCore.Components;
using OpsPilot.Web.Models;
using OpsPilot.Web.Services;

namespace OpsPilot.Web.Components.Pages;

public partial class Home
{
    [Inject] private ScenarioService Scenarios { get; set; } = default!;
    [Inject] private IIncidentAnalyzer Analyzer { get; set; } = default!;
    [Inject] private ILogger<Home> Logger { get; set; } = default!;
    private IncidentScenario[] scenarios = [];
    private string serviceName = "", environmentName = "", logs = "";
    private string? selectedId, notice;
    private IReadOnlyList<string> validationErrors = [];
    private IncidentAssessment? assessment;
    private IncidentRequest? assessedRequest;
    private bool isInteractive, isLoading, approved, disposed;
    private CancellationTokenSource? analysisCancellation;
    private readonly List<SimulationRecord> activity = [];

    protected override async Task OnInitializedAsync() => scenarios = await Scenarios.GetScenariosAsync();

    protected override void OnAfterRender(bool firstRender)
    {
        if (!firstRender) return;
        isInteractive = true;
        StateHasChanged();
    }

    private void LoadScenario(IncidentScenario scenario)
    {
        if (isLoading) return;
        serviceName = scenario.ServiceName;
        environmentName = scenario.Environment;
        logs = scenario.Logs;
        InputChanged();
        selectedId = scenario.Id;
    }

    private void ClearInput()
    {
        serviceName = environmentName = logs = "";
        InputChanged();
    }

    private void InputChanged()
    {
        assessment = null; assessedRequest = null; approved = false;
        notice = null; selectedId = null; validationErrors = [];
    }

    private async Task AnalyzeAsync()
    {
        if (isLoading || disposed) return;
        var request = new IncidentRequest(serviceName.Trim(), environmentName.Trim(), logs);
        validationErrors = IncidentRequestValidator.Validate(request);
        if (validationErrors.Count > 0) return;
        assessment = null; assessedRequest = null; approved = false; notice = null;
        isLoading = true;
        using var cancellation = new CancellationTokenSource();
        analysisCancellation = cancellation;
        try
        {
            var result = await Analyzer.AnalyzeAsync(request, cancellation.Token);
            if (disposed || cancellation.IsCancellationRequested) return;
            assessment = result;
            assessedRequest = request;
        }
        catch (OperationCanceledException) { notice = "Analysis cancelled. You can edit the logs and try again."; }
        catch (AnalysisRateLimitException) { notice = "The demo is busy or its analysis limit was reached. Wait a minute and try again."; }
        catch (Exception exception)
        {
            Logger.LogError("Dashboard analysis failed with {FailureType}", exception.GetType().Name);
            notice = "Analysis could not complete. Please try again.";
        }
        finally { isLoading = false; analysisCancellation = null; }
    }

    private void Cancel() => analysisCancellation?.Cancel();

    private void Approve()
    {
        if (approved || isLoading || assessment is null || assessedRequest is null) return;
        // Guard on the server and bind the action to the exact assessed input.
        var current = new IncidentRequest(serviceName.Trim(), environmentName.Trim(), logs);
        if (current != assessedRequest || !RemediationSimulation.CanApprove(assessment, assessedRequest)) return;
        activity.Insert(0, RemediationSimulation.Create(assessedRequest, assessment));
        if (activity.Count > 20) activity.RemoveAt(20);
        approved = true;
    }

    public void Dispose() { disposed = true; analysisCancellation?.Cancel(); }
}
