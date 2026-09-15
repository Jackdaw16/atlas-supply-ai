using AtlasSupply.Application;
using Microsoft.Extensions.Configuration;
using OpenAI.Responses;

#pragma warning disable OPENAI001 // The installed SDK marks its Responses surface as experimental.

namespace AtlasSupply.Infrastructure.Agents;

public sealed class OpenAIAgentLanguageModel : IAgentLanguageModel
{
    private readonly ResponsesClient _responsesClient;
    private readonly string _model;
    private readonly ResponseReasoningOptions? _reasoningOptions;

    public OpenAIAgentLanguageModel(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var apiKey = configuration["OpenAI:ApiKey"] ?? configuration["OPENAI_API_KEY"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "OpenAI API key is missing. Configure OpenAI:ApiKey or OPENAI_API_KEY.");
        }

        var model = configuration["OpenAI:ChatModel"];
        if (string.IsNullOrWhiteSpace(model))
        {
            throw new InvalidOperationException("OpenAI chat model is missing. Configure OpenAI:ChatModel.");
        }

        _model = model;
        _responsesClient = new ResponsesClient(apiKey);
        _reasoningOptions = CreateReasoningOptions(configuration["OpenAI:ReasoningEffort"]);
    }

    public async Task<AgentLanguageModelResponse> CompleteAsync(
        AgentLanguageModelRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var options = new CreateResponseOptions
        {
            Model = _model,
            Instructions = request.SystemInstructions,
            ParallelToolCallsEnabled = false,
            ReasoningOptions = _reasoningOptions
        };
        foreach (var tool in request.Tools)
        {
            options.Tools.Add(ResponseTool.CreateFunctionTool(
                tool.Name,
                BinaryData.FromString(tool.InputSchema.GetRawText()),
                null,
                tool.Description));
        }

        foreach (var inputItem in CreateInputItems(request))
        {
            options.InputItems.Add(inputItem);
        }

        var response = (await _responsesClient.CreateResponseAsync(options, cancellationToken)).Value;

        var toolCalls = response.OutputItems
            .OfType<FunctionCallResponseItem>()
            .Select(toolCall => new AgentToolCall(
                toolCall.CallId,
                toolCall.FunctionName,
                toolCall.FunctionArguments.ToString()))
            .ToArray();

        return new AgentLanguageModelResponse(response.GetOutputText(), toolCalls);
    }

    private static List<ResponseItem> CreateInputItems(AgentLanguageModelRequest request)
    {
        var items = new List<ResponseItem>();

        foreach (var message in request.Messages)
        {
            switch (message)
            {
                case AgentUserMessage userMessage:
                    items.Add(ResponseItem.CreateUserMessageItem(userMessage.Content));
                    break;
                case AgentAssistantMessage assistantMessage:
                    if (!string.IsNullOrEmpty(assistantMessage.Content))
                    {
                        items.Add(ResponseItem.CreateAssistantMessageItem(assistantMessage.Content));
                    }

                    foreach (var toolCall in assistantMessage.ToolCalls)
                    {
                        items.Add(new FunctionCallResponseItem(
                            toolCall.Id,
                            toolCall.Name,
                            BinaryData.FromString(toolCall.ArgumentsJson)));
                    }

                    break;
                case AgentToolMessage toolMessage:
                    items.Add(new FunctionCallOutputResponseItem(toolMessage.ToolCallId, toolMessage.Content));
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported agent message type '{message.GetType().Name}'.");
            }
        }

        return items;
    }

    private static ResponseReasoningOptions? CreateReasoningOptions(string? reasoningEffort)
    {
        if (string.IsNullOrWhiteSpace(reasoningEffort))
        {
            return null;
        }

        return new ResponseReasoningOptions
        {
            ReasoningEffortLevel = reasoningEffort.Trim().ToLowerInvariant() switch
            {
                "minimal" => ResponseReasoningEffortLevel.Minimal,
                "low" => ResponseReasoningEffortLevel.Low,
                "medium" => ResponseReasoningEffortLevel.Medium,
                "high" => ResponseReasoningEffortLevel.High,
                "none" => ResponseReasoningEffortLevel.None,
                _ => throw new InvalidOperationException(
                    "OpenAI:ReasoningEffort must be one of: minimal, low, medium, high, none.")
            }
        };
    }
}
