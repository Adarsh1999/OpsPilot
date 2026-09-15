using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpsPilot.Web.Services;

namespace OpsPilot.Tests;

public sealed class DemoFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Testing")
        .ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AZURE_OPENAI_ENDPOINT"] = null, ["AZURE_OPENAI_DEPLOYMENT"] = null, ["AZURE_TENANT_ID"] = null
        }));
}

public class HostAndFixtureTests : IClassFixture<DemoFactory>
{
    private readonly DemoFactory factory;
    public HostAndFixtureTests(DemoFactory factory) => this.factory = factory;

    [Fact]
    public async Task Dashboard_renders_all_three_scenarios_without_Azure()
    {
        using var client = factory.CreateClient();
        var html = await client.GetStringAsync("/");
        Assert.Contains("AI Incident Copilot", html);
        Assert.Contains("Database connections exhausted", html);
        Assert.Contains("Checkout fails after release", html);
        Assert.Contains("Valid sign-ins are rejected", html);
        Assert.Contains("Analyze incident", html);
        Assert.DoesNotContain("AZURE_OPENAI_ENDPOINT", html);
    }

    [Fact]
    public async Task Health_is_available_without_Azure()
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/health");
        response.EnsureSuccessStatusCode();
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Fixtures_cover_three_scenarios_and_unknown_service_has_no_context()
    {
        var scenarios = await factory.Services.GetRequiredService<ScenarioService>().GetScenariosAsync();
        var runbooks = factory.Services.GetRequiredService<IRunbookService>();
        var deployments = factory.Services.GetRequiredService<IDeploymentHistoryService>();
        Assert.Equal(3, scenarios.Length);
        foreach (var scenario in scenarios)
        {
            Assert.Empty(IncidentRequestValidator.Validate(new(scenario.ServiceName, scenario.Environment, scenario.Logs)));
            Assert.NotNull(await runbooks.GetRunbookAsync(scenario.ServiceName));
            Assert.NotEmpty(await deployments.GetRecentDeploymentsAsync(scenario.ServiceName));
        }
        Assert.Null(await runbooks.GetRunbookAsync("../../appsettings"));
        Assert.Empty(await deployments.GetRecentDeploymentsAsync("unknown"));
        Assert.Equal("v2.4", (await deployments.GetRecentDeploymentsAsync("CHECKOUT-API"))[0].Version);
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runbooks.GetRunbookAsync("checkout-api", cancel.Token));
    }

    [Fact]
    public void Analysis_limiter_caps_concurrency_and_requests()
    {
        using var gate = new AnalysisGate();
        using (var first = gate.Acquire())
        using (var second = gate.Acquire())
            Assert.Throws<AnalysisRateLimitException>(() => gate.Acquire());
        for (var i = 0; i < 8; i++) gate.Acquire().Dispose();
        Assert.Throws<AnalysisRateLimitException>(() => gate.Acquire());
    }
}
