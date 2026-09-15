using OpsPilot.Web.Models;

namespace OpsPilot.Web.Services;

public record SimulationRecord(DateTimeOffset CreatedAt, string Message);

// No network, shell, infrastructure client, or persistence dependency. Text only.
public static class RemediationSimulation
{
    public static bool CanApprove(IncidentAssessment assessment, IncidentRequest request) =>
        assessment.RequiresHumanApproval && assessment.Evidence is { Length: > 0 } && AssessmentSafety.IsGrounded(assessment, request);

    public static string Describe(IncidentRequest request) =>
        request.ServiceName.Equals("checkout-api", StringComparison.OrdinalIgnoreCase) && request.Logs.Contains("version=v2.4", StringComparison.Ordinal)
            ? "Rollback request created for checkout-api-v2.4."
            : $"Remediation review request created for {request.ServiceName}.";

    public static SimulationRecord Create(IncidentRequest request, IncidentAssessment assessment)
    {
        if (!CanApprove(assessment, request)) throw new InvalidOperationException("A grounded assessment is required.");
        return new(DateTimeOffset.UtcNow, Describe(request));
    }
}
