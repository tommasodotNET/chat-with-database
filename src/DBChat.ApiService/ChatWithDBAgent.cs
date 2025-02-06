using System;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Plugins.OpenApi;

namespace DBChat.ApiService;

public class ChatWithDBAgent
{
    private string _promptPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "semantic-kernel",
            "prompts"
        );
    private ChatCompletionAgent _agent;
    private string _agentName = "TravelOfferingsExpert";

    public ChatWithDBAgent(Kernel kernel)
    {
        var _agentInstructions = File.ReadAllText(Path.Combine(_promptPath, "offering_expert_instructions.txt"));
        var _settings = new OpenAIPromptExecutionSettings()
        {
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
            Temperature = 0.1,
            MaxTokens = 500,
        };

        _agent = new()
            {
                Name = _agentName,
                Instructions = _agentInstructions,
                Kernel = kernel,
                Arguments = new(_settings)
            };

        _agent.Kernel.ImportPluginFromOpenApiAsync(
            pluginName: "dab_post",
            filePath: Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "semantic-kernel", "plugins", "dab.yaml"),
            executionParameters: new OpenApiFunctionExecutionParameters()
            {
                ServerUrlOverride = new Uri(Environment.GetEnvironmentVariable("services__dab__http__0")!),
                EnableDynamicPayload = true,
                IgnoreNonCompliantErrors = true,
                EnablePayloadNamespacing = true,
            }).GetAwaiter().GetResult();
    }

    public IAsyncEnumerable<StreamingChatMessageContent> InvokeStreamingAsync(ChatHistory history)
    {
        return _agent.InvokeStreamingAsync(history);
    }
}
