using System.Text.Json;
using CodeAlta.Agent;
using CodeAlta.Agent.Copilot;
using GitHub.Copilot.SDK;

namespace CodeAlta.Tests;

[TestClass]
public sealed class CopilotAgentMapperTests
{
    [TestMethod]
    public void ToSessionConfig_MapsCoreOptions()
    {
        var toolSchema = JsonDocument.Parse("""{"type":"object","properties":{"value":{"type":"string"}}}""").RootElement;
        var options = new AgentSessionCreateOptions
        {
            Model = "gpt-5",
            WorkingDirectory = @"C:\repo",
            Streaming = true,
            ReasoningEffort = AgentReasoningEffort.High,
            SystemMessage = "System guidance",
            DeveloperInstructions = "Developer guidance",
            OnPermissionRequest = static (_, _) => Task.FromResult(new AgentPermissionDecision(AgentPermissionDecisionKind.AllowOnce)),
            Tools =
            [
                new AgentToolDefinition(
                    new AgentToolSpec("echo_tool", "Echoes input", toolSchema),
                    static (_, _) => Task.FromResult<AgentToolResult>(
                        new(
                            true,
                            [new AgentToolResultItem.Text("ok")])))
            ]
        };

        var config = CopilotAgentMapper.ToSessionConfig(options);

        Assert.AreEqual("gpt-5", config.Model);
        Assert.AreEqual(@"C:\repo", config.WorkingDirectory);
        Assert.IsTrue(config.Streaming);
        Assert.AreEqual("high", config.ReasoningEffort);
        Assert.IsNotNull(config.SystemMessage);
        Assert.AreEqual(SystemMessageMode.Append, config.SystemMessage.Mode);
        Assert.IsTrue(config.SystemMessage.Content!.Contains("System guidance", StringComparison.Ordinal));
        Assert.IsTrue(config.SystemMessage.Content.Contains("Developer guidance", StringComparison.Ordinal));
        Assert.IsNotNull(config.OnPermissionRequest);
        Assert.IsNotNull(config.Tools);
        Assert.AreEqual(1, config.Tools.Count);
    }

    [TestMethod]
    public void ToSessionConfig_SanitizesCopilotToolNames()
    {
        var toolSchema = JsonDocument.Parse("""{"type":"object","properties":{"value":{"type":"string"}}}""").RootElement;
        var options = new AgentSessionCreateOptions
        {
            OnPermissionRequest = static (_, _) => Task.FromResult(new AgentPermissionDecision(AgentPermissionDecisionKind.AllowOnce)),
            Tools =
            [
                new AgentToolDefinition(
                    new AgentToolSpec("codealta.tasks.create", "Creates a task", toolSchema),
                    static (_, _) => Task.FromResult<AgentToolResult>(
                        new(
                            true,
                            [new AgentToolResultItem.Text("ok")]))),
                new AgentToolDefinition(
                    new AgentToolSpec("codealta_tasks_create", "Creates another task", toolSchema),
                    static (_, _) => Task.FromResult<AgentToolResult>(
                        new(
                            true,
                            [new AgentToolResultItem.Text("ok")]))),
            ]
        };

        var config = CopilotAgentMapper.ToSessionConfig(options);
        var tools = config.Tools!.ToArray();

        Assert.IsNotNull(config.Tools);
        Assert.AreEqual(2, tools.Length);
        Assert.AreEqual("codealta_tasks_create", tools[0].Name);
        Assert.AreEqual("codealta_tasks_create_2", tools[1].Name);
    }

    [TestMethod]
    public void ToAgentModelInfo_MapsReasoningEffortMetadata()
    {
        var model = new ModelInfo
        {
            Id = "gpt-5",
            Name = "GPT-5",
            SupportedReasoningEfforts = ["minimal", "xhigh", "unknown"],
            DefaultReasoningEffort = "medium"
        };

        var mapped = CopilotAgentMapper.ToAgentModelInfo(model);

        Assert.AreEqual("gpt-5", mapped.Id);
        Assert.AreEqual("GPT-5", mapped.DisplayName);
        Assert.IsNull(mapped.Description);
        Assert.AreEqual(AgentReasoningEffort.Medium, mapped.DefaultReasoningEffort);
        CollectionAssert.AreEqual(
            new[] { AgentReasoningEffort.Minimal, AgentReasoningEffort.XHigh },
            mapped.SupportedReasoningEfforts!.ToArray());
    }

    [TestMethod]
    public void ToMessageOptions_MapsAttachmentsAndPromptFallbacks()
    {
        var options = new AgentSendOptions
        {
            Mode = "edit",
            Input = new AgentInput(
            [
                new AgentInputItem.Text("Review these files"),
                new AgentInputItem.File("Program.cs", "Program.cs", new AgentLineRange(2, 8)),
                new AgentInputItem.Directory("src", "source"),
                new AgentInputItem.Selection(
                    "App.cs",
                    "Selection",
                    "Console.WriteLine(\"hello\");",
                    new AgentSelectionRange(
                        new AgentPosition(1, 3),
                        new AgentPosition(4, 1))),
                new AgentInputItem.ImageUrl("https://example.com/image.png"),
                new AgentInputItem.LocalImage(@"C:\img\local.png"),
                new AgentInputItem.Skill("linter", "/skills/linter"),
                new AgentInputItem.Mention("workspace", "/mentions/workspace")
            ])
        };

        var mapped = CopilotAgentMapper.ToMessageOptions(options);

        Assert.AreEqual("edit", mapped.Mode);
        Assert.IsNotNull(mapped.Attachments);
        Assert.AreEqual(3, mapped.Attachments.Count);

        Assert.IsInstanceOfType<UserMessageDataAttachmentsItemFile>(mapped.Attachments[0]);
        Assert.IsInstanceOfType<UserMessageDataAttachmentsItemDirectory>(mapped.Attachments[1]);
        Assert.IsInstanceOfType<UserMessageDataAttachmentsItemSelection>(mapped.Attachments[2]);

        var fileAttachment = (UserMessageDataAttachmentsItemFile)mapped.Attachments[0];
        Assert.AreEqual("Program.cs", fileAttachment.Path);
        Assert.IsNotNull(fileAttachment.LineRange);
        Assert.AreEqual(2d, fileAttachment.LineRange.Start);
        Assert.AreEqual(8d, fileAttachment.LineRange.End);

        Assert.IsTrue(mapped.Prompt.Contains("Review these files", StringComparison.Ordinal));
        Assert.IsTrue(mapped.Prompt.Contains("[image-url] https://example.com/image.png", StringComparison.Ordinal));
        Assert.IsTrue(mapped.Prompt.Contains(@"[local-image] C:\img\local.png", StringComparison.Ordinal));
        Assert.IsTrue(mapped.Prompt.Contains("[skill] name=linter path=/skills/linter", StringComparison.Ordinal));
        Assert.IsTrue(mapped.Prompt.Contains("[mention] name=workspace path=/mentions/workspace", StringComparison.Ordinal));
    }

    [TestMethod]
    public void ToAgentEvent_MapsKnownAndUnknownSessionEvents()
    {
        var timestamp = DateTimeOffset.Parse("2026-02-25T12:00:00+00:00");
        var deltaEvent = new AssistantMessageDeltaEvent
        {
            Timestamp = timestamp,
            Data = new AssistantMessageDeltaData
            {
                MessageId = "msg-1",
                DeltaContent = "delta"
            }
        };

        var mappedDelta = CopilotAgentMapper.ToAgentEvent("session-1", deltaEvent);
        Assert.IsInstanceOfType<AgentAssistantMessageDeltaEvent>(mappedDelta);
        var normalizedDelta = (AgentAssistantMessageDeltaEvent)mappedDelta;
        Assert.AreEqual("delta", normalizedDelta.Delta);
        Assert.AreEqual("msg-1", normalizedDelta.RunId?.Value);

        var messageEvent = new AssistantMessageEvent
        {
            Timestamp = timestamp,
            Data = new AssistantMessageData
            {
                MessageId = "msg-2",
                Content = "final message"
            }
        };

        var mappedMessage = CopilotAgentMapper.ToAgentEvent("session-1", messageEvent);
        Assert.IsInstanceOfType<AgentAssistantMessageEvent>(mappedMessage);
        var normalizedMessage = (AgentAssistantMessageEvent)mappedMessage;
        Assert.AreEqual("final message", normalizedMessage.Content);
        Assert.AreEqual("msg-2", normalizedMessage.RunId?.Value);

        var unknownEvent = new SessionInfoEvent
        {
            Timestamp = timestamp,
            Data = new SessionInfoData
            {
                InfoType = "note",
                Message = "raw event"
            }
        };

        var mappedUnknown = CopilotAgentMapper.ToAgentEvent("session-1", unknownEvent);
        Assert.IsInstanceOfType<AgentRawEvent>(mappedUnknown);
        var raw = (AgentRawEvent)mappedUnknown;
        Assert.AreEqual("session.info", raw.BackendEventType);
    }

    [TestMethod]
    public void ToAgentEvent_FallsBackWhenCopilotSdkCannotSerializeRawEvent()
    {
        var timestamp = DateTimeOffset.Parse("2026-02-25T12:00:00+00:00");
        using var quotaSnapshot = JsonDocument.Parse("""{"remaining":1}""");
        var usageEvent = new AssistantUsageEvent
        {
            Timestamp = timestamp,
            Data = new AssistantUsageData
            {
                Model = "gpt-5",
                QuotaSnapshots = new Dictionary<string, object>
                {
                    ["sample"] = quotaSnapshot.RootElement.Clone()
                }
            }
        };

        var mapped = CopilotAgentMapper.ToAgentEvent("session-1", usageEvent);

        Assert.IsInstanceOfType<AgentRawEvent>(mapped);
        var raw = (AgentRawEvent)mapped;
        Assert.AreEqual("assistant.usage", raw.BackendEventType);
        Assert.AreEqual("assistant.usage", raw.Raw.GetProperty("type").GetString());
        Assert.AreEqual(typeof(AssistantUsageEvent).FullName, raw.Raw.GetProperty("eventClass").GetString());
        Assert.IsTrue(raw.Raw.TryGetProperty("serializationError", out var serializationError));
        Assert.IsFalse(string.IsNullOrWhiteSpace(serializationError.GetString()));
    }
}
