using System.ClientModel.Primitives;

namespace CodeAlta.Agent.OpenAI.CodexSubscription;

internal sealed class CodexSubscriptionHeadersPolicy : PipelinePolicy
{
    private readonly CodexSubscriptionHeaderContext _context;

    public CodexSubscriptionHeadersPolicy(CodexSubscriptionHeaderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        ApplyRequestHeaders(message);
        ProcessNext(message, pipeline, currentIndex);
        CaptureResponseHeaders(message);
    }

    public override async ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        ApplyRequestHeaders(message);
        await ProcessNextAsync(message, pipeline, currentIndex).ConfigureAwait(false);
        CaptureResponseHeaders(message);
    }

    private void ApplyRequestHeaders(PipelineMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var headers = message.Request.Headers;
        if (!string.IsNullOrWhiteSpace(_context.AccountId))
        {
            headers.Set("ChatGPT-Account-Id", _context.AccountId);
        }

        headers.Set("originator", "codealta");
        if (_context.SendResponsesBetaHeader)
        {
            headers.Set("OpenAI-Beta", "responses=experimental");
        }

        if (!string.IsNullOrWhiteSpace(_context.SessionId))
        {
            headers.Set("session_id", _context.SessionId);
        }

        if (_context.IsFedRamp)
        {
            headers.Set("X-OpenAI-Fedramp", "true");
        }

        if (_context.TurnState.TryGetCapturedState(out var turnState))
        {
            headers.Set("x-codex-turn-state", turnState);
        }
    }

    private void CaptureResponseHeaders(PipelineMessage message)
    {
        if (message.Response?.Headers.TryGetValue("x-codex-turn-state", out var turnState) == true &&
            !string.IsNullOrWhiteSpace(turnState))
        {
            _context.TurnState.Capture(turnState);
        }
    }
}

internal sealed record CodexSubscriptionHeaderContext(
    string? AccountId,
    string? SessionId,
    bool IsFedRamp,
    bool SendResponsesBetaHeader,
    CodexTurnState TurnState);
