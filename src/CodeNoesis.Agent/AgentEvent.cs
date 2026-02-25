using System.Text.Json;

namespace CodeNoesis.Agent;

/// <summary>
/// Base type for a normalized agent event.
/// </summary>
/// <param name="BackendId">The backend identifier.</param>
/// <param name="SessionId">The session identifier.</param>
/// <param name="Timestamp">Event timestamp.</param>
/// <param name="RunId">Optional run identifier.</param>
public abstract record AgentEvent(
    AgentBackendId BackendId,
    string SessionId,
    DateTimeOffset Timestamp,
    AgentRunId? RunId = null);

/// <summary>
/// A raw, backend-specific event emitted when no normalized mapping exists.
/// </summary>
/// <param name="BackendId">The backend identifier.</param>
/// <param name="SessionId">The session identifier.</param>
/// <param name="Timestamp">Event timestamp.</param>
/// <param name="BackendEventType">Backend event type identifier.</param>
/// <param name="Raw">Raw backend payload.</param>
/// <param name="RunId">Optional run identifier.</param>
public sealed record AgentRawEvent(
    AgentBackendId BackendId,
    string SessionId,
    DateTimeOffset Timestamp,
    string BackendEventType,
    JsonElement Raw,
    AgentRunId? RunId = null)
    : AgentEvent(BackendId, SessionId, Timestamp, RunId);

/// <summary>
/// Streaming delta of assistant message content.
/// </summary>
/// <param name="BackendId">The backend identifier.</param>
/// <param name="SessionId">The session identifier.</param>
/// <param name="Timestamp">Event timestamp.</param>
/// <param name="RunId">Run identifier.</param>
/// <param name="Delta">Delta content.</param>
public sealed record AgentAssistantMessageDeltaEvent(
    AgentBackendId BackendId,
    string SessionId,
    DateTimeOffset Timestamp,
    AgentRunId? RunId,
    string Delta)
    : AgentEvent(BackendId, SessionId, Timestamp, RunId);

/// <summary>
/// Final assistant message content event.
/// </summary>
/// <param name="BackendId">The backend identifier.</param>
/// <param name="SessionId">The session identifier.</param>
/// <param name="Timestamp">Event timestamp.</param>
/// <param name="RunId">Run identifier.</param>
/// <param name="Content">Assistant content.</param>
public sealed record AgentAssistantMessageEvent(
    AgentBackendId BackendId,
    string SessionId,
    DateTimeOffset Timestamp,
    AgentRunId? RunId,
    string Content)
    : AgentEvent(BackendId, SessionId, Timestamp, RunId);

/// <summary>
/// Indicates that the session is idle (the backend finished processing).
/// </summary>
/// <param name="BackendId">The backend identifier.</param>
/// <param name="SessionId">The session identifier.</param>
/// <param name="Timestamp">Event timestamp.</param>
public sealed record AgentSessionIdleEvent(
    AgentBackendId BackendId,
    string SessionId,
    DateTimeOffset Timestamp)
    : AgentEvent(BackendId, SessionId, Timestamp);

/// <summary>
/// Represents an error event.
/// </summary>
/// <param name="BackendId">The backend identifier.</param>
/// <param name="SessionId">The session identifier.</param>
/// <param name="Timestamp">Event timestamp.</param>
/// <param name="Message">Error message.</param>
/// <param name="Exception">Optional exception.</param>
/// <param name="RunId">Optional run identifier.</param>
public sealed record AgentErrorEvent(
    AgentBackendId BackendId,
    string SessionId,
    DateTimeOffset Timestamp,
    string Message,
    Exception? Exception = null,
    AgentRunId? RunId = null)
    : AgentEvent(BackendId, SessionId, Timestamp, RunId);
