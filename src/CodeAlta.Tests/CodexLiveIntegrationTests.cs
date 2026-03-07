using CodeAlta.Agent;
using CodeAlta.Agent.Codex;

namespace CodeAlta.Tests;

[TestClass]
public sealed class CodexLiveIntegrationTests
{
    private const string LiveCodexTestsEnvironmentVariable = "CODEALTA_RUN_LIVE_CODEX_TESTS";

    [TestMethod]
    [TestCategory("LiveCodex")]
    public async Task CodexAgentBackend_LivePrompt_ProducesAssistantContent()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(LiveCodexTestsEnvironmentVariable), "1", StringComparison.Ordinal))
        {
            Assert.Inconclusive(
                $"Set {LiveCodexTestsEnvironmentVariable}=1 to run live Codex integration tests.");
        }

        await using var backend = new CodexAgentBackend(new CodexAgentBackendOptions());
        IAgentSession session;
        try
        {
            session = await backend.CreateSessionAsync(
                    new AgentSessionCreateOptions
                    {
                        Streaming = true,
                        WorkingDirectory = Environment.CurrentDirectory,
                        OnPermissionRequest = static (_, _) =>
                            Task.FromResult(new AgentPermissionDecision(AgentPermissionDecisionKind.AllowOnce)),
                    })
                .ConfigureAwait(false);
        }
        catch (FileNotFoundException ex)
        {
            Assert.Inconclusive($"Codex executable was not found: {ex.Message}");
            return;
        }

        await using var asyncSession = session;

        var assistantContent = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var errorEvent = new TaskCompletionSource<AgentErrorEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = asyncSession.Subscribe(@event =>
        {
            switch (@event)
            {
                case AgentContentDeltaEvent delta when delta.Kind == AgentContentKind.Assistant && !string.IsNullOrWhiteSpace(delta.Delta):
                    assistantContent.TrySetResult(delta.Delta);
                    break;
                case AgentContentCompletedEvent message when message.Kind == AgentContentKind.Assistant && !string.IsNullOrWhiteSpace(message.Content):
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
            Assert.Fail($"Codex returned an error event instead of content: {error.Message}");
        }

        Assert.Fail("No assistant content was received from Codex within the timeout.");
    }

    [TestMethod]
    [TestCategory("LiveCodex")]
    public async Task CodexAgentBackend_LivePrompt_CanInspectExternalProjectFromDifferentWorkingDirectory()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(LiveCodexTestsEnvironmentVariable), "1", StringComparison.Ordinal))
        {
            Assert.Inconclusive(
                $"Set {LiveCodexTestsEnvironmentVariable}=1 to run live Codex integration tests.");
        }

        const string externalProjectPath = @"C:\code\Tomlyn";
        if (!Directory.Exists(externalProjectPath))
        {
            Assert.Inconclusive($"External project path '{externalProjectPath}' does not exist.");
        }

        var codeAltaProjectPath = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "CodeAlta"));

        await using var backend = new CodexAgentBackend(new CodexAgentBackendOptions());
        IAgentSession session;
        try
        {
            session = await backend.CreateSessionAsync(
                    new AgentSessionCreateOptions
                    {
                        Streaming = true,
                        WorkingDirectory = codeAltaProjectPath,
                        OnPermissionRequest = static (_, _) =>
                            Task.FromResult(new AgentPermissionDecision(AgentPermissionDecisionKind.AllowOnce)),
                    })
                .ConfigureAwait(false);
        }
        catch (FileNotFoundException ex)
        {
            Assert.Inconclusive($"Codex executable was not found: {ex.Message}");
            return;
        }

        await using var asyncSession = session;

        var assistantContent = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var activityEvent = new TaskCompletionSource<AgentActivityEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        var errorEvent = new TaskCompletionSource<AgentErrorEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = asyncSession.Subscribe(@event =>
        {
            switch (@event)
            {
                case AgentContentDeltaEvent delta when delta.Kind == AgentContentKind.Assistant && !string.IsNullOrWhiteSpace(delta.Delta):
                    assistantContent.TrySetResult(delta.Delta);
                    break;
                case AgentContentCompletedEvent message when message.Kind == AgentContentKind.Assistant && !string.IsNullOrWhiteSpace(message.Content):
                    assistantContent.TrySetResult(message.Content);
                    break;
                case AgentActivityEvent activity when activity.Kind != AgentActivityKind.Turn:
                    activityEvent.TrySetResult(activity);
                    break;
                case AgentErrorEvent error:
                    errorEvent.TrySetResult(error);
                    break;
            }
        });

        _ = await asyncSession.SendAsync(
                new AgentSendOptions
                {
                    Input = AgentInput.Text($"Could you tell me a bit more about the project `{externalProjectPath}`?")
                })
            .ConfigureAwait(false);

        var timeoutTask = Task.Delay(TimeSpan.FromMinutes(2));
        var completedTask = await Task.WhenAny(
                assistantContent.Task,
                activityEvent.Task,
                errorEvent.Task,
                timeoutTask)
            .ConfigureAwait(false);

        if (completedTask == errorEvent.Task)
        {
            var error = await errorEvent.Task.ConfigureAwait(false);
            Assert.Fail($"Codex returned an error event instead of inspecting the external project: {error.Message}");
        }

        if (completedTask == timeoutTask)
        {
            Assert.Fail("Codex did not inspect the external project within the timeout.");
        }

        await Task.WhenAll(activityEvent.Task, assistantContent.Task).ConfigureAwait(false);

        Assert.AreEqual(AgentActivityKind.CommandExecution, activityEvent.Task.Result.Kind);
        Assert.IsFalse(string.IsNullOrWhiteSpace(assistantContent.Task.Result));
    }
}
