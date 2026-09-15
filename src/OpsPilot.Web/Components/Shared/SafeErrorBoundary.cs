using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace OpsPilot.Web.Components.Shared;

public sealed class SafeErrorBoundary : ErrorBoundary
{
    [Inject] public ILogger<SafeErrorBoundary> Logger { get; set; } = default!;
    protected override Task OnErrorAsync(Exception exception)
    {
        Logger.LogError("UI operation failed with {FailureType}", exception.GetType().Name);
        return Task.CompletedTask;
    }
}
