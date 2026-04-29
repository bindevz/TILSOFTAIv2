using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;
using System.ClientModel;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Infrastructure.Llm;
using TILSOFTAI.Orchestration.AiRouting;
using TILSOFTAI.Orchestration.AiRouting.MicrosoftAgentFramework;
using TILSOFTAI.Orchestration.Caching;

namespace TILSOFTAI.Api.Extensions;

public static class AddTilsoftAiLocalAiExtensions
{
    public static IServiceCollection AddTilsoftAiLocalAi(this IServiceCollection services, IConfiguration configuration)
    {
        RegisterOfficialAgentChatClient(services, configuration);
        services.AddHttpClient<OpenAiEmbeddingClient>();
        services.AddSingleton<IEmbeddingClient>(sp => sp.GetRequiredService<OpenAiEmbeddingClient>());
        return services;
    }

    private static void RegisterOfficialAgentChatClient(IServiceCollection services, IConfiguration configuration)
    {
        var provider = ResolveOfficialAgentProvider(configuration);
        if (string.Equals(provider, "AzureOpenAI", StringComparison.OrdinalIgnoreCase)
            || string.Equals(provider, "OpenAiCompatible", StringComparison.OrdinalIgnoreCase)
            || string.Equals(provider, "OpenAiCompatibleLocal", StringComparison.OrdinalIgnoreCase)
            || string.Equals(provider, "OpenAI", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IChatClient>(CreateOfficialAgentChatClient);
        }
    }

    private static IChatClient CreateOfficialAgentChatClient(IServiceProvider services)
    {
        var options = services.GetRequiredService<IOptions<LlmOptions>>().Value;
        var localAiOptions = services.GetRequiredService<IOptions<LocalAiOptions>>().Value;
        var aiRoutingOptions = services.GetRequiredService<IOptions<AiRoutingOptions>>().Value;
        var provider = ResolveOfficialAgentProvider(aiRoutingOptions, options);
        var model = ResolveOfficialAgentModel(aiRoutingOptions, options, localAiOptions);

        if (string.Equals(provider, "AzureOpenAI", StringComparison.OrdinalIgnoreCase))
        {
            RequireLlmSetting(options.Endpoint, "Llm:Endpoint", provider);
            RequireLlmSetting(options.ApiKey, "Llm:ApiKey", provider);
            RequireLlmSetting(model, "AiRouting:Model or Llm:Model", provider);

            var azureClient = new AzureOpenAIClient(
                new Uri(options.Endpoint),
                new ApiKeyCredential(options.ApiKey));

            return new ChatClientBuilder(azureClient.GetChatClient(model).AsIChatClient())
                .UseFunctionInvocation()
                .Build();
        }

        if (string.Equals(provider, "OpenAiCompatible", StringComparison.OrdinalIgnoreCase)
            || string.Equals(provider, "OpenAiCompatibleLocal", StringComparison.OrdinalIgnoreCase)
            || string.Equals(provider, "OpenAI", StringComparison.OrdinalIgnoreCase))
        {
            var endpoint = string.Equals(provider, "OpenAiCompatibleLocal", StringComparison.OrdinalIgnoreCase)
                ? localAiOptions.BaseUrl
                : options.Endpoint;
            var apiKey = string.Equals(provider, "OpenAiCompatibleLocal", StringComparison.OrdinalIgnoreCase)
                ? Environment.GetEnvironmentVariable(localAiOptions.ApiKeyEnvironmentVariable) ?? "local-ai-no-key"
                : options.ApiKey;

            RequireLlmSetting(apiKey, "Llm:ApiKey or LocalAi:ApiKeyEnvironmentVariable", provider);
            RequireLlmSetting(model, "AiRouting:Model or Llm:Model", provider);

            var clientOptions = new OpenAIClientOptions();
            if (!string.IsNullOrWhiteSpace(endpoint))
            {
                clientOptions.Endpoint = new Uri(endpoint);
            }

            var chatClient = new OpenAI.Chat.ChatClient(
                model,
                new ApiKeyCredential(apiKey),
                clientOptions);

            return new ChatClientBuilder(chatClient.AsIChatClient())
                .UseFunctionInvocation()
                .Build();
        }

        throw new InvalidOperationException(
            "AgentFramework mode requires Llm:Provider or AiRouting:Provider to be AzureOpenAI, OpenAI, OpenAiCompatible, OpenAiCompatibleLocal, or an explicitly registered Microsoft.Extensions.AI provider.");
    }

    private static string ResolveOfficialAgentProvider(IConfiguration configuration)
    {
        var aiRouting = configuration.GetSection(ConfigurationSectionNames.AiRouting);
        var officialEnabled = aiRouting.GetValue<bool>("MicrosoftAgentFrameworkRoutingEnabled")
            || aiRouting.GetValue<bool>("UseOfficialMicrosoftAgentFramework");
        var aiRoutingProvider = aiRouting.GetValue<string>("Provider")?.Trim();
        if (officialEnabled && !string.IsNullOrWhiteSpace(aiRoutingProvider))
        {
            return aiRoutingProvider;
        }

        return configuration.GetSection(ConfigurationSectionNames.Llm).GetValue<string>("Provider")?.Trim() ?? string.Empty;
    }

    private static string ResolveOfficialAgentProvider(AiRoutingOptions aiRoutingOptions, LlmOptions llmOptions)
    {
        var officialEnabled = aiRoutingOptions.MicrosoftAgentFrameworkRoutingEnabled
            || aiRoutingOptions.UseOfficialMicrosoftAgentFramework;
        return officialEnabled && !string.IsNullOrWhiteSpace(aiRoutingOptions.Provider)
            ? aiRoutingOptions.Provider.Trim()
            : llmOptions.Provider.Trim();
    }

    private static string ResolveOfficialAgentModel(AiRoutingOptions aiRoutingOptions, LlmOptions llmOptions, LocalAiOptions? localAiOptions = null)
    {
        var officialEnabled = aiRoutingOptions.MicrosoftAgentFrameworkRoutingEnabled
            || aiRoutingOptions.UseOfficialMicrosoftAgentFramework;
        if (officialEnabled
            && string.Equals(aiRoutingOptions.Provider, "OpenAiCompatibleLocal", StringComparison.OrdinalIgnoreCase)
            && localAiOptions is not null
            && !string.IsNullOrWhiteSpace(localAiOptions.Model))
        {
            return localAiOptions.Model.Trim();
        }

        return officialEnabled && !string.IsNullOrWhiteSpace(aiRoutingOptions.Model)
            ? aiRoutingOptions.Model.Trim()
            : llmOptions.Model.Trim();
    }

    private static void RequireLlmSetting(string value, string settingName, string provider)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{settingName} is required for {provider} Agent Framework provider setup.");
        }
    }
}
