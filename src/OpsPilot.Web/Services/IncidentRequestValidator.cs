using System.Text.RegularExpressions;
using OpsPilot.Web.Models;

namespace OpsPilot.Web.Services;

public static partial class IncidentRequestValidator
{
    public const int MaxLogLength = 16_000;
    public const int MaxLogLines = 200;

    public static IReadOnlyList<string> Validate(IncidentRequest request)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(request.ServiceName) || request.ServiceName.Length > 80 || !Identifier().IsMatch(request.ServiceName))
            errors.Add("Enter a service name of 1–80 letters, numbers, dots, underscores or hyphens.");
        if (string.IsNullOrWhiteSpace(request.Environment) || request.Environment.Length > 40 || !Identifier().IsMatch(request.Environment))
            errors.Add("Enter an environment of 1–40 letters, numbers, dots, underscores or hyphens.");
        if (string.IsNullOrWhiteSpace(request.Logs))
            errors.Add("Add some fictional logs to analyze.");
        else if (request.Logs.Length > MaxLogLength || LogLines(request.Logs).Length > MaxLogLines)
            errors.Add("Logs must contain at most 16,000 characters and 200 lines.");
        return errors;
    }

    public static string[] LogLines(string logs) => logs.ReplaceLineEndings("\n").TrimEnd('\n').Split('\n');

    [GeneratedRegex(@"\A[a-zA-Z0-9][a-zA-Z0-9._-]*\z", RegexOptions.CultureInvariant)]
    private static partial Regex Identifier();
}
