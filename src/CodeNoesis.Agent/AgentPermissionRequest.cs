using System.Text.Json;

namespace CodeNoesis.Agent;

/// <summary>
/// Represents a permission/approval request originating from the backend.
/// </summary>
/// <param name="BackendId">The backend identifier.</param>
/// <param name="SessionId">The session identifier.</param>
/// <param name="Kind">The backend-defined request kind.</param>
/// <param name="Raw">The raw request payload.</param>
public sealed record AgentPermissionRequest(
    AgentBackendId BackendId,
    string SessionId,
    string Kind,
    JsonElement Raw);

/// <summary>
/// Represents the decision for a permission/approval request.
/// </summary>
/// <param name="Kind">The decision kind.</param>
/// <param name="ExecPolicyAmendment">
/// Optional exec policy amendment. This is currently Codex-specific and may be ignored by other backends.
/// </param>
public sealed record AgentPermissionDecision(
    AgentPermissionDecisionKind Kind,
    IReadOnlyList<string>? ExecPolicyAmendment = null);

/// <summary>
/// The kind of permission decision.
/// </summary>
public enum AgentPermissionDecisionKind
{
    /// <summary>
    /// Allow the action once.
    /// </summary>
    AllowOnce,

    /// <summary>
    /// Allow the action for the remainder of the session.
    /// </summary>
    AllowForSession,

    /// <summary>
    /// Deny the action.
    /// </summary>
    Deny,

    /// <summary>
    /// Cancel the request (distinct from deny when the backend supports it).
    /// </summary>
    Cancel,
}

/// <summary>
/// Permission request handler delegate.
/// </summary>
/// <param name="request">The permission request.</param>
/// <param name="cancellationToken">A token to cancel the operation.</param>
public delegate Task<AgentPermissionDecision> AgentPermissionRequestHandler(
    AgentPermissionRequest request,
    CancellationToken cancellationToken);

