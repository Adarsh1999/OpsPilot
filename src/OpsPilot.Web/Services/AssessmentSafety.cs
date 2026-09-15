using System.Text.RegularExpressions;
using OpsPilot.Web.Models;

namespace OpsPilot.Web.Services;

public static partial class AssessmentSafety
{
    public static IncidentAssessment Fallback() => new(
        IncidentSeverity.High,
        "AI assessment unavailable. High is a conservative review priority, not an inferred severity. Human review is required.",
        [], [],
        ["Review the supplied logs with an operator before making changes.",
         "Check local model setup and access, then retry. No remediation has been performed."],
        true,
        "Automated assessment is unavailable. Impact and root cause remain unverified. Human review is required; no remediation has been performed.");

    public static bool IsGrounded(IncidentAssessment? assessment, IncidentRequest request)
    {
        if (assessment is null || !Enum.IsDefined(assessment.Severity) || !TextValid(assessment.Summary) ||
            !TextValid(assessment.StakeholderUpdate) || !ArrayValid(assessment.Evidence) ||
            !ArrayValid(assessment.LikelyCauses) || !ArrayValid(assessment.RecommendedActions) ||
            assessment.RecommendedActions.Length == 0) return false;

        var validLines = IncidentRequestValidator.LogLines(request.Logs)
            .Select((line, i) => $"[L{i + 1}] {line}").ToHashSet(StringComparer.Ordinal);
        if (assessment.Evidence.Any(e => !validLines.Contains(e))) return false;
        var citedIds = assessment.Evidence.Select(e => e[..(e.IndexOf(']') + 1)]).ToHashSet();
        return assessment.LikelyCauses.All(cause =>
        {
            var citations = Citation().Matches(cause);
            return citations.Count > 0 && citations.All(citation => citedIds.Contains(citation.Value));
        });
    }

    // Verifies citation integrity, not semantic truth. Operator review remains mandatory.
    private static bool TextValid(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 1200;
    private static bool ArrayValid(string[]? values) => values is { Length: <= 8 } && values.All(TextValid);

    [GeneratedRegex(@"\[L\d+\]", RegexOptions.CultureInvariant)]
    private static partial Regex Citation();
}
