using System.Text;
using Microsoft.AspNetCore.Http;
using OpsPilot.Web.Services;

namespace OpsPilot.Tests;

public class ExceptionHandlerTests
{
    [Fact]
    public async Task Global_handler_hides_details_but_preserves_status_and_trace()
    {
        const string sensitive = "credential=private endpoint=internal.example";
        var logger = new CapturingLogger<SafeExceptionHandler>();
        var context = new DefaultHttpContext { TraceIdentifier = "test-trace" };
        using var body = new MemoryStream();
        context.Response.Body = body;
        var handled = await new SafeExceptionHandler(logger).TryHandleAsync(context, new InvalidOperationException(sensitive), CancellationToken.None);
        Assert.True(handled);
        Assert.Equal(500, context.Response.StatusCode);
        var response = Encoding.UTF8.GetString(body.ToArray());
        Assert.Contains("test-trace", response);
        Assert.DoesNotContain(sensitive, response);
        Assert.DoesNotContain(sensitive, string.Join('\n', logger.Entries));
    }
}
