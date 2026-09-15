using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.AI;
using OpsPilot.Web.Components;
using OpsPilot.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// These framework categories can log raw exceptions; safe handlers below log type + trace only.
builder.Logging.AddFilter("Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware", LogLevel.None);
builder.Logging.AddFilter("Microsoft.AspNetCore.Components.Server.Circuits", LogLevel.None);
builder.Services.AddRazorComponents().AddInteractiveServerComponents(options => options.DetailedErrors = false)
    // Covers JSON-escaped Unicode within the validated 16,000-character log budget.
    .AddHubOptions(options => options.MaximumReceiveMessageSize = 128 * 1024);
builder.Services.AddSingleton<FixtureStore>();
builder.Services.AddSingleton<ScenarioService>();
builder.Services.AddSingleton<IRunbookService, JsonRunbookService>();
builder.Services.AddSingleton<IDeploymentHistoryService, JsonDeploymentHistoryService>();
builder.Services.AddSingleton<IncidentPromptBuilder>();
builder.Services.AddSingleton<AnalysisGate>();
builder.Services.AddSingleton<IChatClient>(_ => AzureChatClientFactory.Create(builder.Configuration));
builder.Services.AddScoped<IIncidentAnalyzer, FoundryIncidentAnalyzer>();
builder.Services.AddExceptionHandler<SafeExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    // One process-wide budget, no unbounded per-client partition dictionary.
    options.AddFixedWindowLimiter("page", limiter =>
    {
        limiter.PermitLimit = 120;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });
});

var app = builder.Build();
app.UseExceptionHandler();
if (!app.Environment.IsDevelopment()) app.UseHsts();
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAntiforgery();
app.MapHealthChecks("/health"); // Liveness only; never spends model tokens.
app.MapRazorComponents<App>().AddInteractiveServerRenderMode().RequireRateLimiting("page");
app.Run();

public partial class Program { }
