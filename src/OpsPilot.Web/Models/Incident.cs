using System.Text.Json.Serialization;

namespace OpsPilot.Web.Models;

[JsonConverter(typeof(JsonStringEnumConverter<IncidentSeverity>))]
public enum IncidentSeverity { Low, Medium, High, Critical }

public record IncidentRequest(string ServiceName, string Environment, string Logs);

public record IncidentAssessment(
    [property: JsonRequired] IncidentSeverity Severity,
    [property: JsonRequired] string Summary,
    [property: JsonRequired] string[] Evidence,
    [property: JsonRequired] string[] LikelyCauses,
    [property: JsonRequired] string[] RecommendedActions,
    [property: JsonRequired] bool RequiresHumanApproval,
    [property: JsonRequired] string StakeholderUpdate);
