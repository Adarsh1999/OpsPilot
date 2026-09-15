using OpsPilot.Web.Models;

namespace OpsPilot.Web.Services;

public interface IIncidentAnalyzer
{
    Task<IncidentAssessment> AnalyzeAsync(IncidentRequest request, CancellationToken cancellationToken = default);
}

// Deliberately small, typed, read-only seams for future function tools.
// This version calls them directly; no function calling or agent loop is enabled.
public interface IRunbookService
{
    Task<Runbook?> GetRunbookAsync(string serviceName, CancellationToken cancellationToken = default);
}

public interface IDeploymentHistoryService
{
    Task<IReadOnlyList<Deployment>> GetRecentDeploymentsAsync(string serviceName, CancellationToken cancellationToken = default);
}
