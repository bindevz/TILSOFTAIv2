using System.Text;
using System.Text.Json;
using TILSOFTAI.Orchestration.Llm;

namespace TILSOFTAI.Infrastructure.Llm;

internal sealed class OpenAiStreamState
{
    public StringBuilder Content { get; } = new();
    public Dictionary<string, ToolCallBuilder> ToolCalls { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> Emitted { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<int, string> IndexMap { get; } = new();
    public bool ToolCallsFinalized { get; set; }
}

internal sealed class ToolCallBuilder
{
    public string? Name { get; set; }
    public StringBuilder Arguments { get; } = new();
}

internal static class OpenAiResponseParser
{
    public static LlmResponse ParseCompletion(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var response = new LlmResponse();

        if (!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0)
        {
            return response;
        }

        var message = choices[0].GetProperty("message");
        if (message.TryGetProperty("content", out var contentElement) && contentElement.ValueKind == JsonValueKind.String)
        {
            response.Content = contentElement.GetString();
        }

        if (message.TryGetProperty("tool_calls", out var toolCallsElement) && toolCallsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var toolCall in toolCallsElement.EnumerateArray())
            {
                if (!toolCall.TryGetProperty("function", out var functionElement))
                {
                    continue;
                }

                var name = functionElement.TryGetProperty("name", out var nameElement) && nameElement.ValueKind == JsonValueKind.String
                    ? nameElement.GetString()
                    : string.Empty;

                var args = "{}";
                if (functionElement.TryGetProperty("arguments", out var argsElement))
                {
                    args = argsElement.ValueKind == JsonValueKind.String
                        ? argsElement.GetString() ?? "{}"
                        : argsElement.GetRawText();
                }

                response.ToolCalls.Add(new LlmToolCall
                {
                    Name = name ?? string.Empty,
                    ArgumentsJson = args
                });
            }
        }

        if (root.TryGetProperty("usage", out var usageElement) && usageElement.ValueKind == JsonValueKind.Object)
        {
            response.Usage = new LlmUsage();
            
            if (usageElement.TryGetProperty("prompt_tokens", out var promptTokens))
            {
                response.Usage.InputTokens = promptTokens.GetInt32();
            }
            
            if (usageElement.TryGetProperty("completion_tokens", out var completionTokens))
            {
                response.Usage.OutputTokens = completionTokens.GetInt32();
            }
            
            if (usageElement.TryGetProperty("total_tokens", out var totalTokens))
            {
                response.Usage.TotalTokens = totalTokens.GetInt32();
            }
        }

        return response;
    }

    public static IEnumerable<LlmStreamEvent> HandleStreamChunk(OpenAiStreamState state, string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var choice in choices.EnumerateArray())
        {
            if (choice.TryGetProperty("delta", out var delta))
            {
                if (delta.TryGetProperty("content", out var contentElement) && contentElement.ValueKind == JsonValueKind.String)
                {
                    var deltaText = contentElement.GetString();
                    if (!string.IsNullOrEmpty(deltaText))
                    {
                        state.Content.Append(deltaText);
                        yield return LlmStreamEvent.Delta(deltaText);
                    }
                }

                if (delta.TryGetProperty("tool_calls", out var toolCallsElement) && toolCallsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var toolCall in toolCallsElement.EnumerateArray())
                    {
                        var key = ResolveToolCallKey(state, toolCall);
                        if (string.IsNullOrWhiteSpace(key))
                        {
                            continue;
                        }

                        if (!state.ToolCalls.TryGetValue(key, out var builder))
                        {
                            builder = new ToolCallBuilder();
                            state.ToolCalls[key] = builder;
                        }

                        if (toolCall.TryGetProperty("function", out var functionElement))
                        {
                            if (functionElement.TryGetProperty("name", out var nameElement) && nameElement.ValueKind == JsonValueKind.String)
                            {
                                builder.Name = nameElement.GetString();
                            }

                            if (functionElement.TryGetProperty("arguments", out var argsElement))
                            {
                                var argsPart = argsElement.ValueKind == JsonValueKind.String
                                    ? argsElement.GetString()
                                    : argsElement.GetRawText();

                                if (!string.IsNullOrEmpty(argsPart))
                                {
                                    builder.Arguments.Append(argsPart);
                                }
                            }
                        }
                    }
                }
            }

            if (choice.TryGetProperty("finish_reason", out var finishElement)
                && finishElement.ValueKind == JsonValueKind.String)
            {
                var reason = finishElement.GetString();
                if (string.Equals(reason, "tool_calls", StringComparison.OrdinalIgnoreCase))
                {
                    state.ToolCallsFinalized = true;
                }
            }
        }

        if (state.ToolCallsFinalized)
        {
            foreach (var evt in EmitToolCalls(state))
            {
                yield return evt;
            }
        }
    }

    public static IEnumerable<LlmStreamEvent> FinalizeStream(OpenAiStreamState state)
    {
        foreach (var evt in EmitToolCalls(state))
        {
            yield return evt;
        }

        yield return LlmStreamEvent.Final(state.Content.ToString());
    }

    private static IEnumerable<LlmStreamEvent> EmitToolCalls(OpenAiStreamState state)
    {
        foreach (var (key, builder) in state.ToolCalls)
        {
            if (state.Emitted.Contains(key))
            {
                continue;
            }

            state.Emitted.Add(key);
            yield return LlmStreamEvent.ToolCallEvent(new LlmToolCall
            {
                Name = builder.Name ?? string.Empty,
                ArgumentsJson = builder.Arguments.Length == 0 ? "{}" : builder.Arguments.ToString()
            });
        }
    }

    private static string ResolveToolCallKey(OpenAiStreamState state, JsonElement toolCall)
    {
        int? index = null;
        if (toolCall.TryGetProperty("index", out var indexElement) && indexElement.ValueKind == JsonValueKind.Number)
        {
            index = indexElement.GetInt32();
        }

        if (toolCall.TryGetProperty("id", out var idElement) && idElement.ValueKind == JsonValueKind.String)
        {
            var id = idElement.GetString();
            if (!string.IsNullOrWhiteSpace(id))
            {
                if (index.HasValue)
                {
                    state.IndexMap[index.Value] = id;
                }
                return id;
            }
        }

        if (index.HasValue && state.IndexMap.TryGetValue(index.Value, out var mappedId))
        {
            return mappedId;
        }

        if (index.HasValue)
        {
            return $"index:{index.Value}";
        }

        return string.Empty;
    }
}
