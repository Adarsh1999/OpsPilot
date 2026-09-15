using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.AI;
using System.ClientModel.Primitives;

namespace OpsPilot.Web.Services;

public static class AzureChatClientFactory
{
    public static IChatClient Create(IConfiguration configuration)
    {
        var endpoint = configuration["AZURE_OPENAI_ENDPOINT"];
        var deployment = configuration["AZURE_OPENAI_DEPLOYMENT"];
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || string.IsNullOrWhiteSpace(deployment))
            return new UnconfiguredChatClient();

        var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
        {
            TenantId = configuration["AZURE_TENANT_ID"],
            Diagnostics = { IsLoggingEnabled = false, IsLoggingContentEnabled = false }
        });
        return new AzureOpenAIClient(uri, credential, new AzureOpenAIClientOptions
        {
            NetworkTimeout = TimeSpan.FromSeconds(55),
            RetryPolicy = new ClientRetryPolicy(maxRetries: 0),
            ClientLoggingOptions = new() { EnableLogging = false, EnableMessageContentLogging = false }
        }).GetChatClient(deployment).AsIChatClient();
    }

    private sealed class UnconfiguredChatClient : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Azure model configuration is missing or invalid.");
        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
