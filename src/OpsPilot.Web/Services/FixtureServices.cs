using System.Text.Json;
using OpsPilot.Web.Models;

namespace OpsPilot.Web.Services;

// Fixed filenames, never a user-derived path. Files are shipped with the application.
public sealed class FixtureStore(IHostEnvironment environment)
{
    public async Task<T[]> ReadAsync<T>(string fileName, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(Path.Combine(environment.ContentRootPath, "Fixtures", fileName));
        return await JsonSerializer.DeserializeAsync<T[]>(stream, cancellationToken: cancellationToken)
            ?? throw new InvalidDataException("Fixture data is empty.");
    }
}

public sealed class JsonRunbookService(FixtureStore store) : IRunbookService
{
    public async Task<Runbook?> GetRunbookAsync(string serviceName, CancellationToken cancellationToken = default) =>
        (await store.ReadAsync<Runbook>("runbooks.json", cancellationToken))
        .FirstOrDefault(r => string.Equals(r.ServiceName, serviceName, StringComparison.OrdinalIgnoreCase));
}

public sealed class JsonDeploymentHistoryService(FixtureStore store) : IDeploymentHistoryService
{
    public async Task<IReadOnlyList<Deployment>> GetRecentDeploymentsAsync(string serviceName, CancellationToken cancellationToken = default) =>
        (await store.ReadAsync<Deployment>("deployments.json", cancellationToken))
        .Where(d => string.Equals(d.ServiceName, serviceName, StringComparison.OrdinalIgnoreCase))
        .OrderByDescending(d => d.DeployedAt).Take(5).ToArray();
}

public sealed class ScenarioService(FixtureStore store)
{
    public Task<IncidentScenario[]> GetScenariosAsync(CancellationToken cancellationToken = default) =>
        store.ReadAsync<IncidentScenario>("scenarios.json", cancellationToken);
}
