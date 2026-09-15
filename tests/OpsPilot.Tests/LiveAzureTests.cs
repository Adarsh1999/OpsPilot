using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using OpsPilot.Web.Services;

namespace OpsPilot.Tests;

// Explicit opt-in: these tests use the signed-in Azure identity and incur inference usage.
public sealed class LiveAzureFactAttribute : FactAttribute
{
    public LiveAzureFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("OPSPILOT_LIVE_TESTS") != "1")
            Skip = "Set OPSPILOT_LIVE_TESTS=1 to run against configured Azure user-secrets.";
    }
}

public class LiveAzureTests
{
    [LiveAzureFact]
    [Trait("Category", "LiveAzure")]
    public async Task All_three_fictional_scenarios_return_grounded_assessments()
    {
        var configuration = new ConfigurationBuilder().AddUserSecrets<Program>().AddEnvironmentVariables().Build();
        using var client = AzureChatClientFactory.Create(configuration);
        using var gate = new AnalysisGate();
        var store = new FixtureStore(new HostEnvironment
        {
            ContentRootPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/OpsPilot.Web"))
        });
        var logger = new CapturingLogger<FoundryIncidentAnalyzer>();
        var analyzer = new FoundryIncidentAnalyzer(client, new JsonRunbookService(store), new JsonDeploymentHistoryService(store),
            new(), gate, logger);
        foreach (var scenario in await new ScenarioService(store).GetScenariosAsync())
        {
            var request = new OpsPilot.Web.Models.IncidentRequest(scenario.ServiceName, scenario.Environment, scenario.Logs);
            var result = await analyzer.AnalyzeAsync(request);
            Assert.True(result.Evidence.Length > 0, $"Scenario {scenario.Id} returned fallback. {string.Join("; ", logger.Entries)}");
            Assert.NotEmpty(result.LikelyCauses);
            Assert.True(result.RequiresHumanApproval);
            Assert.True(AssessmentSafety.IsGrounded(result, request));
        }
    }

    private sealed class HostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "OpsPilot.Web";
        public string ContentRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
