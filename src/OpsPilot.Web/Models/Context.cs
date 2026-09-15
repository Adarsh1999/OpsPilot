namespace OpsPilot.Web.Models;

public record Runbook(string ServiceName, string Title, string[] Steps);
public record Deployment(string ServiceName, string Version, DateTimeOffset DeployedAt, string Change, string Status);
public record IncidentScenario(string Id, string Title, string Description, string ServiceName, string Environment, string Logs);
