namespace CodeNoesis.Agent;

/// <summary>
/// Describes an available model in an agent backend.
/// </summary>
/// <param name="Id">The backend model identifier.</param>
/// <param name="DisplayName">Optional display name.</param>
/// <param name="Provider">Optional provider identifier (e.g. "openai").</param>
/// <param name="Capabilities">
/// Optional backend-defined capabilities metadata. The shape is backend-specific.
/// </param>
public sealed record AgentModelInfo(
    string Id,
    string? DisplayName = null,
    string? Provider = null,
    IReadOnlyDictionary<string, object?>? Capabilities = null);

