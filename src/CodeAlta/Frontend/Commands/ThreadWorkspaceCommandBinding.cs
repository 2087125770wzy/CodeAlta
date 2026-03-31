namespace CodeAlta.Frontend.Commands;

internal sealed class ThreadWorkspaceCommandBinding
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public ThreadWorkspaceCommandBinding(
        ShellCommandMetadata metadata,
        Action execute,
        Func<bool>? canExecute = null)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(execute);

        Metadata = metadata;
        _execute = execute;
        _canExecute = canExecute;
    }

    public ShellCommandMetadata Metadata { get; }

    public void Execute()
        => _execute();

    public bool CanExecute()
        => _canExecute?.Invoke() ?? true;
}
