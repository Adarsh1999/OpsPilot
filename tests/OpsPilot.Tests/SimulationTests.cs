using OpsPilot.Web.Models;
using OpsPilot.Web.Services;

namespace OpsPilot.Tests;

public class SimulationTests
{
    [Fact]
    public void Approval_creates_only_a_deterministic_fictional_record()
    {
        var request = new IncidentRequest("checkout-api", "demo", "HTTP 500\nTaxMapper failed\nversion=v2.4");
        var record = RemediationSimulation.Create(request, AnalyzerTests.Valid);
        Assert.Equal("Rollback request created for checkout-api-v2.4.", record.Message);
        Assert.InRange(record.CreatedAt, DateTimeOffset.UtcNow.AddSeconds(-5), DateTimeOffset.UtcNow);
    }

    [Fact]
    public void Fallback_and_changed_logs_cannot_be_approved()
    {
        var request = new IncidentRequest("checkout-api", "demo", "HTTP 500\nTaxMapper failed");
        Assert.False(RemediationSimulation.CanApprove(AssessmentSafety.Fallback(), request));
        Assert.Throws<InvalidOperationException>(() => RemediationSimulation.Create(request, AssessmentSafety.Fallback()));
        Assert.False(RemediationSimulation.CanApprove(AnalyzerTests.Valid, request with { Logs = "healthy" }));
        Assert.False(RemediationSimulation.CanApprove(AnalyzerTests.Valid with { RequiresHumanApproval = false }, request));
    }

    [Fact]
    public void Custom_service_gets_a_review_record_without_inventing_a_release()
    {
        var request = new IncidentRequest("custom-api", "demo", "HTTP 500\nTaxMapper failed");
        Assert.Equal("Remediation review request created for custom-api.", RemediationSimulation.Create(request, AnalyzerTests.Valid).Message);
        Assert.DoesNotContain("v2.4", RemediationSimulation.Describe(request with { ServiceName = "checkout-api" }));
    }
}
