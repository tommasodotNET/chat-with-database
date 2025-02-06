using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.ChatCompletion;
using DBChat.ApiService.Model;
using System.Reflection;
using Microsoft.SemanticKernel.Plugins.OpenApi;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace DBChat.ApiService;

public class TravelAgentChat
{
    private Kernel _kernel;

    private AgentGroupChat chat;

    public TravelAgentChat(Kernel kernel)
    {
        _kernel = kernel;
        InitializeScenario();
    }

    public async IAsyncEnumerable<string?> ExecuteScenarioStreaming(string prompt)
    {
        chat.AddChatMessage(new ChatMessageContent(AuthorRole.User, prompt));
        await foreach (var content in chat.InvokeStreamingAsync())
        {
            Console.WriteLine();
            Console.WriteLine($"# {content.Role} - {content.AuthorName ?? "*"}: '{content.Content}'");
            Console.WriteLine();
            yield return content.Content;
        }

        Console.WriteLine($"# IS COMPLETE: {chat.IsComplete}");
    }

    public void InitializeScenario()
    {
        string promptPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "semantic-kernel",
            "prompts"
        );

        string offeringExpertName = "TravelOfferingsExpert";
        string offeringExpertInstructions = File.ReadAllText(Path.Combine(promptPath, "offering_expert_instructions.txt"));

        string travelAgentName = "TravelAgent";
        string travelAgentInstructions = File.ReadAllText(Path.Combine(promptPath, "travel_agent_instructions.txt"));

        string travelManagerName = "TravelManager";
        string travelManagerInstructions = File.ReadAllText(Path.Combine(promptPath, "travel_manager_instructions.txt"));

        ChatCompletionAgent travelAgent = new ChatCompletionAgent
        {
            Name = travelAgentName,
            Instructions = travelAgentInstructions,
            Kernel = _kernel
        };

        ChatCompletionAgent travelManager = new ChatCompletionAgent
        {
            Name = travelManagerName,
            Instructions = travelManagerInstructions,
            Kernel = _kernel
        };

        var _settings = new OpenAIPromptExecutionSettings()
        {
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
            Temperature = 0.1,
            MaxTokens = 500,
        };

        ChatCompletionAgent travelOfferingAgent = new ChatCompletionAgent
        {
            Name = offeringExpertName,
            Instructions = offeringExpertInstructions,
            Kernel = _kernel,
            Arguments = new(_settings)
        };

        travelOfferingAgent.Kernel.ImportPluginFromOpenApiAsync(
            pluginName: "graphql_post",
            filePath: Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "semantic-kernel", "plugins", "dab.yaml"),
            executionParameters: new OpenApiFunctionExecutionParameters()
            {
                ServerUrlOverride = new Uri(Environment.GetEnvironmentVariable("services__dab__http__0")!),
                EnableDynamicPayload = true,
                IgnoreNonCompliantErrors = true,
                EnablePayloadNamespacing = true,
            }).GetAwaiter().GetResult();

        KernelFunction terminateFunction = KernelFunctionFactory.CreateFromPrompt(
            $$$"""
            Determine if the travel plan has been approved by {{{travelManagerName}}}. If so, respond with a single word: yes.

            History:

            {{$history}}
            """
            );

        KernelFunction selectionFunction = KernelFunctionFactory.CreateFromPrompt(
            $$$"""
            Your job is to determine which participant takes the next turn in a conversation according to the action of the most recent participant.
            State only the name of the participant to take the next turn.

            Choose only from these participants:
            - {{{travelManagerName}}}
            - {{{offeringExpertName}}}
            - {{{travelAgentName}}}

            Always follow these steps when selecting the next participant:
            1) After user input, it is {OFFERINGEXPERT_NAME}'s turn.
            2) After {{{offeringExpertName}}} replies, it's {{{travelAgentName}}}'s turn to generate a plan for the given trip.
            3) After {{{travelAgentName}}} replies, it's {{{travelManagerName}}}'s turn to review the plan.
            4) If the plan is approved, the conversation ends.
            5) If the plan is approved, it's again {TRAVELAGENT_NAME}'s turn to provide an improved plan.
            6) Steps 3, 4, 5 and 6 are repeated until the plan is finally approved.
        
            History:
            {{$history}}
            """     
            );

        chat = new(travelManager, travelAgent, travelOfferingAgent)
        {
            ExecutionSettings = new()
            {
                TerminationStrategy = new KernelFunctionTerminationStrategy(terminateFunction, _kernel)
                {
                    Agents = [travelManager],
                    ResultParser = (result) => result.GetValue<string>()?.Contains("yes", StringComparison.OrdinalIgnoreCase) ?? false,
                    HistoryVariableName = "history",
                    MaximumIterations = 10
                },
                SelectionStrategy = new KernelFunctionSelectionStrategy(selectionFunction, _kernel)
                {
                    AgentsVariableName = "agents",
                    HistoryVariableName = "history"
                }
            }
        };
    }
}