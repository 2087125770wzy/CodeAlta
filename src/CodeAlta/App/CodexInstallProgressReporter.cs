using CodeAlta.CodexSdk;

namespace CodeAlta.App;

internal sealed class CodexInstallProgressReporter : IProgress<CodexInstallProgress>
{
    private readonly object _syncRoot = new();
    private event Action<CodexInstallProgress>? ProgressChanged;

    public CodexInstallProgress? Latest { get; private set; }

    public void Report(CodexInstallProgress value)
    {
        ArgumentNullException.ThrowIfNull(value);

        Action<CodexInstallProgress>? handlers;
        lock (_syncRoot)
        {
            Latest = value;
            handlers = ProgressChanged;
        }

        handlers?.Invoke(value);
    }

    public IDisposable Subscribe(Action<CodexInstallProgress> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        lock (_syncRoot)
        {
            ProgressChanged += callback;
            if (Latest is { } latest)
            {
                callback(latest);
            }
        }

        return new Subscription(this, callback);
    }

    private void Unsubscribe(Action<CodexInstallProgress> callback)
    {
        lock (_syncRoot)
        {
            ProgressChanged -= callback;
        }
    }

    private sealed class Subscription : IDisposable
    {
        private readonly CodexInstallProgressReporter _owner;
        private Action<CodexInstallProgress>? _callback;

        public Subscription(CodexInstallProgressReporter owner, Action<CodexInstallProgress> callback)
        {
            _owner = owner;
            _callback = callback;
        }

        public void Dispose()
        {
            if (_callback is null)
            {
                return;
            }

            _owner.Unsubscribe(_callback);
            _callback = null;
        }
    }
}
