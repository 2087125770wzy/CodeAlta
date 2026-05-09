using CodeAlta.Plugins.Abstractions;

[Plugin("background-task", DisplayName = "Background Task", Description = "Starts tracked background work.")]
public sealed class BackgroundTaskPlugin : PluginBase
{
    public override ValueTask OnActivatedAsync(CancellationToken cancellationToken = default)
    {
        _ = Tasks.Run("sample-background-loop", async token =>
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(30), token).ConfigureAwait(false);
            }
        });
        return ValueTask.CompletedTask;
    }

    public override IEnumerable<PluginCommandContribution> GetCommands()
    {
        yield return Command.Prompt("background-task-status", "Show tracked sample background task state.", static (context, _) =>
        {
            var count = context.Services.Tasks.RunningTaskCount;
            return ValueTask.FromResult(PluginCommandResult.Message($"Background task sample has {count} tracked task(s) running."));
        });
    }
}
