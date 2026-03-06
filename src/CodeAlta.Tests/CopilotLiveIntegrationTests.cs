using System.Text.Json;
using CodeAlta.Agent;
using CodeAlta.Agent.Copilot;

namespace CodeAlta.Tests;

[TestClass]
public sealed class CopilotLiveIntegrationTests
{
    private const string LiveCopilotTestsEnvironmentVariable = "CODEALTA_RUN_LIVE_COPILOT_TESTS";

    [TestMethod]
    [TestCategory("LiveCopilot")]
    public async Task CopilotAgentBackend_LivePrompt_WithDottedTool_ProducesAssistantContent()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(LiveCopilotTestsEnvironmentVariable), "1", StringComparison.Ordinal))
        {
            Assert.Inconclusive(
                $"Set {LiveCopilotTestsEnvironmentVariable}=1 to run live Copilot integration tests.");
        }

        await using var backend = new CopilotAgentBackend(new CopilotAgentBackendOptions());
        var toolSchema = JsonDocument.Parse("""{"type":"object","properties":{"value":{"type":"string"}}}""").RootElement.Clone();
        IAgentSession session;
        try
        {
            session = await backend.CreateSessionAsync(
                    new AgentSessionCreateOptions
                    {
                        Streaming = true,
                        WorkingDirectory = Environment.CurrentDirectory,
                        Tools =
                        [
                            new AgentToolDefinition(
                                new AgentToolSpec("codealta.tasks.create", "Creates a task", toolSchema),
                                static (_, _) => Task.FromResult<AgentToolResult>(
                                    new(
                                        true,
                                        [new AgentToolResultItem.Text("ok")])))
                        ],
                        OnPermissionRequest = static (_, _) =>
                            Task.FromResult(new AgentPermissionDecision(AgentPermissionDecisionKind.AllowOnce)),
                    })
                .ConfigureAwait(false);
        }
        catch (FileNotFoundException ex)
        {
            Assert.Inconclusive($"Copilot executable was not found: {ex.Message}");
            return;
        }

        await using var asyncSession = session;

        var assistantContent = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var errorEvent = new TaskCompletionSource<AgentErrorEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = asyncSession.Subscribe(@event =>
        {
            switch (@event)
            {
                case AgentAssistantMessageEvent message when !string.IsNullOrWhiteSpace(message.Content):
                    assistantContent.TrySetResult(message.Content);
                    break;
                case AgentErrorEvent error:
                    errorEvent.TrySetResult(error);
                    break;
            }
        });

        _ = await asyncSession.SendAsync(
                new AgentSendOptions
                {
                    Input = AgentInput.Text("Reply with exactly the word pong.")
                })
            .ConfigureAwait(false);

        var timeoutTask = Task.Delay(TimeSpan.FromMinutes(2));
        var completedTask = await Task.WhenAny(assistantContent.Task, errorEvent.Task, timeoutTask).ConfigureAwait(false);

        if (completedTask == assistantContent.Task)
        {
            var content = await assistantContent.Task.ConfigureAwait(false);
            Assert.IsFalse(string.IsNullOrWhiteSpace(content));
            return;
        }

        if (completedTask == errorEvent.Task)
        {
            var error = await errorEvent.Task.ConfigureAwait(false);
            Assert.Fail($"Copilot returned an error event instead of content: {error.Message}");
        }

        Assert.Fail("No assistant content was received from Copilot within the timeout.");
    }
}
