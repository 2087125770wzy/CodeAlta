using System.Text;
using System.Text.Json;
using GitHub.Copilot.SDK;
using Microsoft.Extensions.AI;

namespace CodeAlta.Agent.Copilot;

internal static class CopilotAgentMapper
{
    public static AgentModelInfo ToAgentModelInfo(ModelInfo model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var capabilities = new Dictionary<string, object?>
        {
            ["capabilities"] = model.Capabilities,
            ["policy"] = model.Policy,
            ["billing"] = model.Billing,
            ["supportedReasoningEfforts"] = model.SupportedReasoningEfforts,
            ["defaultReasoningEffort"] = model.DefaultReasoningEffort
        };
        var supportedReasoningEfforts = ToAgentReasoningEfforts(model.SupportedReasoningEfforts);

        return new AgentModelInfo(
            model.Id,
            DisplayName: model.Name,
            Description: null,
            Provider: null,
            DefaultReasoningEffort: ToAgentReasoningEffort(model.DefaultReasoningEffort),
            SupportedReasoningEfforts: supportedReasoningEfforts,
            Capabilities: capabilities);
    }

    public static AgentSessionMetadata ToAgentSessionMetadata(SessionMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        var context = metadata.Context is null
            ? null
            : new AgentSessionContext(
                metadata.Context.Cwd,
                metadata.Context.GitRoot,
                metadata.Context.Repository,
                metadata.Context.Branch);

        return new AgentSessionMetadata(
            SessionId: metadata.SessionId,
            CreatedAt: new DateTimeOffset(metadata.StartTime),
            UpdatedAt: new DateTimeOffset(metadata.ModifiedTime),
            Summary: metadata.Summary,
            Context: context,
            WorkspacePath: null);
    }

    public static SessionConfig ToSessionConfig(AgentSessionCreateOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var systemMessage = BuildSystemMessage(options.SystemMessage, options.DeveloperInstructions);
        var config = new SessionConfig
        {
            Model = options.Model,
            WorkingDirectory = options.WorkingDirectory,
            Streaming = options.Streaming,
            ReasoningEffort = ToCopilotReasoningEffort(options.ReasoningEffort),
            OnPermissionRequest = CreatePermissionHandler(options.OnPermissionRequest),
            OnUserInputRequest = CreateUserInputHandler(options.OnUserInputRequest),
            SystemMessage = systemMessage,
            Tools = ToCopilotTools(options.Tools)
        };

        return config;
    }

    public static ResumeSessionConfig ToResumeSessionConfig(AgentSessionResumeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var systemMessage = BuildSystemMessage(options.SystemMessage, options.DeveloperInstructions);
        var config = new ResumeSessionConfig
        {
            Model = options.Model,
            WorkingDirectory = options.WorkingDirectory,
            Streaming = options.Streaming,
            ReasoningEffort = ToCopilotReasoningEffort(options.ReasoningEffort),
            OnPermissionRequest = CreatePermissionHandler(options.OnPermissionRequest),
            OnUserInputRequest = CreateUserInputHandler(options.OnUserInputRequest),
            SystemMessage = systemMessage,
            Tools = ToCopilotTools(options.Tools)
        };

        return config;
    }

    public static MessageOptions ToMessageOptions(AgentSendOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var attachments = new List<UserMessageDataAttachmentsItem>();
        var promptBuilder = new StringBuilder();

        foreach (var item in options.Input.Items)
        {
            switch (item)
            {
                case AgentInputItem.Text text:
                    AppendPromptLine(promptBuilder, text.Value);
                    break;

                case AgentInputItem.File file:
                    attachments.Add(new UserMessageDataAttachmentsItemFile
                    {
                        Path = file.Path,
                        DisplayName = file.DisplayName ?? file.Path,
                        LineRange = file.LineRange is null
                            ? null
                            : new UserMessageDataAttachmentsItemFileLineRange
                            {
                                Start = file.LineRange.StartLine,
                                End = file.LineRange.EndLine
                            }
                    });
                    break;

                case AgentInputItem.Directory directory:
                    attachments.Add(new UserMessageDataAttachmentsItemDirectory
                    {
                        Path = directory.Path,
                        DisplayName = directory.DisplayName ?? directory.Path,
                        LineRange = directory.LineRange is null
                            ? null
                            : new UserMessageDataAttachmentsItemDirectoryLineRange
                            {
                                Start = directory.LineRange.StartLine,
                                End = directory.LineRange.EndLine
                            }
                    });
                    break;

                case AgentInputItem.Selection selection:
                    attachments.Add(new UserMessageDataAttachmentsItemSelection
                    {
                        FilePath = selection.FilePath,
                        DisplayName = selection.DisplayName,
                        Text = selection.SelectedText,
                        Selection = new UserMessageDataAttachmentsItemSelectionSelection
                        {
                            Start = new UserMessageDataAttachmentsItemSelectionSelectionStart
                            {
                                Line = selection.Range.Start.Line,
                                Character = selection.Range.Start.Character
                            },
                            End = new UserMessageDataAttachmentsItemSelectionSelectionEnd
                            {
                                Line = selection.Range.End.Line,
                                Character = selection.Range.End.Character
                            }
                        }
                    });
                    break;

                case AgentInputItem.ImageUrl imageUrl:
                    AppendPromptLine(promptBuilder, $"[image-url] {imageUrl.Url}");
                    break;

                case AgentInputItem.LocalImage localImage:
                    AppendPromptLine(promptBuilder, $"[local-image] {localImage.Path}");
                    break;

                case AgentInputItem.Skill skill:
                    AppendPromptLine(promptBuilder, $"[skill] name={skill.Name} path={skill.Path}");
                    break;

                case AgentInputItem.Mention mention:
                    AppendPromptLine(promptBuilder, $"[mention] name={mention.Name} path={mention.Path}");
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(item), item, "Unsupported input item.");
            }
        }

        return new MessageOptions
        {
            Prompt = promptBuilder.ToString(),
            Attachments = attachments.Count == 0 ? null : attachments,
            Mode = options.Mode
        };
    }

    public static AgentEvent ToAgentEvent(string sessionId, SessionEvent sessionEvent)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        ArgumentNullException.ThrowIfNull(sessionEvent);

        return sessionEvent switch
        {
            AssistantMessageDeltaEvent delta => new AgentAssistantMessageDeltaEvent(
                AgentBackendIds.Copilot,
                sessionId,
                delta.Timestamp,
                new AgentRunId(delta.Data.MessageId),
                delta.Data.DeltaContent),

            AssistantMessageEvent message => new AgentAssistantMessageEvent(
                AgentBackendIds.Copilot,
                sessionId,
                message.Timestamp,
                new AgentRunId(message.Data.MessageId),
                message.Data.Content),

            SessionIdleEvent idle => new AgentSessionIdleEvent(
                AgentBackendIds.Copilot,
                sessionId,
                idle.Timestamp),

            SessionErrorEvent error => new AgentErrorEvent(
                AgentBackendIds.Copilot,
                sessionId,
                error.Timestamp,
                error.Data.Message),

            _ => new AgentRawEvent(
                AgentBackendIds.Copilot,
                sessionId,
                sessionEvent.Timestamp,
                sessionEvent.Type,
                ToRawElement(sessionEvent),
                TryGetRunId(sessionEvent))
        };
    }

    public static IReadOnlyList<AgentEvent> ToHistoryEvents(string sessionId, IReadOnlyList<SessionEvent> events)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        ArgumentNullException.ThrowIfNull(events);

        return events.Select(sessionEvent => ToAgentEvent(sessionId, sessionEvent)).ToArray();
    }

    private static PermissionRequestHandler CreatePermissionHandler(AgentPermissionRequestHandler handler)
    {
        return async (request, invocation) =>
        {
            var rawRequest = ToPermissionRequest(invocation.SessionId, request);
            var decision = await handler(rawRequest, CancellationToken.None).ConfigureAwait(false);
            return ToPermissionResult(decision);
        };
    }

    private static UserInputHandler? CreateUserInputHandler(AgentUserInputRequestHandler? handler)
    {
        if (handler is null)
            return null;

        return async (request, invocation) =>
        {
            var mappedRequest = new AgentUserInputRequest(
                AgentBackendIds.Copilot,
                invocation.SessionId,
                [new AgentUserInputQuestion("answer", request.Question, request.Choices, request.AllowFreeform ?? true)]);

            var response = await handler(mappedRequest, CancellationToken.None).ConfigureAwait(false);
            var answer = response.Answers.TryGetValue("answer", out var value)
                ? value
                : response.Answers.Values.FirstOrDefault() ?? string.Empty;

            var wasFreeform = request.Choices is { Count: > 0 } choices && !choices.Contains(answer, StringComparer.Ordinal);
            return new UserInputResponse
            {
                Answer = answer,
                WasFreeform = wasFreeform
            };
        };
    }

    private static PermissionRequestResult ToPermissionResult(AgentPermissionDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);

        var kind = decision.Kind switch
        {
            AgentPermissionDecisionKind.AllowOnce => "approved",
            AgentPermissionDecisionKind.AllowForSession => "approved",
            AgentPermissionDecisionKind.Deny => "denied-interactively-by-user",
            AgentPermissionDecisionKind.Cancel => "denied-interactively-by-user",
            _ => "denied-interactively-by-user"
        };

        return new PermissionRequestResult
        {
            Kind = kind
        };
    }

    private static AgentPermissionRequest ToPermissionRequest(string sessionId, PermissionRequest request)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("kind", request.Kind);
            if (request.ToolCallId is not null)
                writer.WriteString("toolCallId", request.ToolCallId);

            if (request.ExtensionData is not null)
            {
                foreach (var pair in request.ExtensionData)
                {
                    switch (pair.Value)
                    {
                        case JsonElement element:
                            writer.WritePropertyName(pair.Key);
                            element.WriteTo(writer);
                            break;
                        case null:
                            writer.WriteNull(pair.Key);
                            break;
                        default:
                            writer.WriteString(pair.Key, pair.Value.ToString());
                            break;
                    }
                }
            }

            writer.WriteEndObject();
        }

        using var document = JsonDocument.Parse(stream.ToArray());
        return new AgentPermissionRequest(
            AgentBackendIds.Copilot,
            sessionId,
            request.Kind,
            document.RootElement.Clone());
    }

    private static SystemMessageConfig? BuildSystemMessage(string? systemMessage, string? developerInstructions)
    {
        if (string.IsNullOrWhiteSpace(systemMessage) && string.IsNullOrWhiteSpace(developerInstructions))
            return null;

        var contentBuilder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(systemMessage))
        {
            contentBuilder.AppendLine(systemMessage.Trim());
        }

        if (!string.IsNullOrWhiteSpace(developerInstructions))
        {
            if (contentBuilder.Length > 0)
                contentBuilder.AppendLine();

            contentBuilder.Append(developerInstructions.Trim());
        }

        return new SystemMessageConfig
        {
            Mode = SystemMessageMode.Append,
            Content = contentBuilder.ToString()
        };
    }

    private static ICollection<AIFunction>? ToCopilotTools(IReadOnlyList<AgentToolDefinition>? tools)
    {
        if (tools is not { Count: > 0 })
            return null;

        var usedNames = new HashSet<string>(StringComparer.Ordinal);
        return tools
            .Select(tool => ToCopilotTool(tool, GetCopilotToolName(tool.Spec.Name, usedNames)))
            .ToArray();
    }

    private static AIFunction ToCopilotTool(AgentToolDefinition tool, string registeredToolName)
    {
        ArgumentNullException.ThrowIfNull(tool);
        ArgumentException.ThrowIfNullOrWhiteSpace(registeredToolName);

        return AIFunctionFactory.Create(
            async (JsonElement arguments, AIFunctionArguments rawArgs) =>
            {
                var invocation = GetInvocation(rawArgs, tool.Spec.Name, arguments);
                var result = await tool.Handler(invocation, CancellationToken.None).ConfigureAwait(false);

                var text = string.Join(
                    Environment.NewLine,
                    result.Items.Select(static item => item switch
                    {
                        AgentToolResultItem.Text value => value.Value,
                        AgentToolResultItem.ImageUrl value => $"[image-url] {value.Url}",
                        _ => string.Empty
                    }).Where(static line => !string.IsNullOrEmpty(line)));

                var payload = new ToolResultObject
                {
                    ResultType = result.Success ? "success" : "failure",
                    TextResultForLlm = text,
                    Error = result.Error
                };

                return new ToolResultAIContent(payload);
            },
            registeredToolName,
            tool.Spec.Description);
    }

    internal static string GetCopilotToolName(string toolName, ISet<string>? usedNames = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);

        Span<char> buffer = stackalloc char[toolName.Length];
        var length = 0;
        var lastWasSeparator = false;

        foreach (var ch in toolName)
        {
            if (char.IsLetterOrDigit(ch) || ch is '_' or '-')
            {
                buffer[length++] = ch;
                lastWasSeparator = false;
                continue;
            }

            if (!lastWasSeparator)
            {
                buffer[length++] = '_';
                lastWasSeparator = true;
            }
        }

        var candidate = length == 0
            ? "tool"
            : new string(buffer[..length]).Trim('_');
        if (candidate.Length == 0)
        {
            candidate = "tool";
        }

        const int maxToolNameLength = 64;
        if (candidate.Length > maxToolNameLength)
        {
            candidate = candidate[..maxToolNameLength];
        }

        if (usedNames is null)
        {
            return candidate;
        }

        if (usedNames.Add(candidate))
        {
            return candidate;
        }

        for (var suffix = 2; ; suffix++)
        {
            var suffixText = $"_{suffix}";
            var baseLength = Math.Min(candidate.Length, maxToolNameLength - suffixText.Length);
            var uniqueCandidate = string.Concat(candidate.AsSpan(0, baseLength), suffixText);
            if (usedNames.Add(uniqueCandidate))
            {
                return uniqueCandidate;
            }
        }
    }

    private static AgentToolInvocation GetInvocation(
        AIFunctionArguments rawArgs,
        string toolName,
        JsonElement arguments)
    {
        if (rawArgs.Context is not null &&
            rawArgs.Context.TryGetValue(typeof(ToolInvocation), out var value) &&
            value is ToolInvocation invocation)
        {
            return new AgentToolInvocation(
                AgentBackendIds.Copilot,
                invocation.SessionId,
                invocation.ToolCallId,
                toolName,
                arguments);
        }

        return new AgentToolInvocation(
            AgentBackendIds.Copilot,
            string.Empty,
            string.Empty,
            toolName,
            arguments);
    }

    private static string? ToCopilotReasoningEffort(AgentReasoningEffort? effort)
    {
        return effort switch
        {
            null => null,
            AgentReasoningEffort.None => "none",
            AgentReasoningEffort.Minimal => "minimal",
            AgentReasoningEffort.Low => "low",
            AgentReasoningEffort.Medium => "medium",
            AgentReasoningEffort.High => "high",
            AgentReasoningEffort.XHigh => "xhigh",
            _ => null
        };
    }

    private static AgentReasoningEffort? ToAgentReasoningEffort(string? effort)
    {
        if (string.IsNullOrWhiteSpace(effort))
            return null;

        return effort.Trim().ToLowerInvariant() switch
        {
            "none" => AgentReasoningEffort.None,
            "minimal" => AgentReasoningEffort.Minimal,
            "low" => AgentReasoningEffort.Low,
            "medium" => AgentReasoningEffort.Medium,
            "high" => AgentReasoningEffort.High,
            "xhigh" => AgentReasoningEffort.XHigh,
            _ => null
        };
    }

    private static IReadOnlyList<AgentReasoningEffort>? ToAgentReasoningEfforts(IReadOnlyList<string>? efforts)
    {
        if (efforts is not { Count: > 0 })
            return null;

        var values = new List<AgentReasoningEffort>(efforts.Count);
        foreach (var effort in efforts)
        {
            var mapped = ToAgentReasoningEffort(effort);
            if (mapped is not { } value)
                continue;

            if (!values.Contains(value))
                values.Add(value);
        }

        return values;
    }

    private static JsonElement ToRawElement(SessionEvent sessionEvent)
    {
        ArgumentNullException.ThrowIfNull(sessionEvent);

        try
        {
            var json = sessionEvent.ToJson();
            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return CreateRawFallback(sessionEvent, ex.Message);
        }
    }

    private static JsonElement CreateRawFallback(SessionEvent sessionEvent, string serializationError)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("type", sessionEvent.Type);
            writer.WriteString("eventClass", sessionEvent.GetType().FullName);
            writer.WriteString("serializationError", serializationError);
            writer.WriteEndObject();
        }

        using var document = JsonDocument.Parse(stream.ToArray());
        return document.RootElement.Clone();
    }

    private static AgentRunId? TryGetRunId(SessionEvent sessionEvent)
    {
        return sessionEvent switch
        {
            AssistantMessageEvent message => new AgentRunId(message.Data.MessageId),
            AssistantMessageDeltaEvent delta => new AgentRunId(delta.Data.MessageId),
            _ => null
        };
    }

    private static void AppendPromptLine(StringBuilder builder, string line)
    {
        if (builder.Length > 0)
            builder.AppendLine();

        builder.Append(line);
    }
}
