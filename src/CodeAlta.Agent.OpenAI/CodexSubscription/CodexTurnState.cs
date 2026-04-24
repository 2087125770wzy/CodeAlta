namespace CodeAlta.Agent.OpenAI.CodexSubscription;

internal sealed class CodexTurnState
{
    private string? _capturedState;

    public bool TryGetCapturedState(out string state)
    {
        if (string.IsNullOrWhiteSpace(_capturedState))
        {
            state = string.Empty;
            return false;
        }

        state = _capturedState;
        return true;
    }

    public void Capture(string state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(state);
        _capturedState ??= state;
    }

    public void Clear() => _capturedState = null;
}
