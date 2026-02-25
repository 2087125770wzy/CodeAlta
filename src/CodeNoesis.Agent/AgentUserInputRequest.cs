namespace CodeNoesis.Agent;

/// <summary>
/// Represents a user input request (e.g. "ask user") originating from a backend.
/// </summary>
/// <param name="BackendId">The backend identifier.</param>
/// <param name="SessionId">The session identifier.</param>
/// <param name="Questions">The questions to ask.</param>
public sealed record AgentUserInputRequest(
    AgentBackendId BackendId,
    string SessionId,
    IReadOnlyList<AgentUserInputQuestion> Questions);

/// <summary>
/// Represents a single user input question.
/// </summary>
/// <param name="Id">Stable identifier for mapping answers.</param>
/// <param name="Question">The question text.</param>
/// <param name="Choices">Optional list of choices.</param>
/// <param name="AllowFreeform">Whether freeform input is allowed.</param>
public sealed record AgentUserInputQuestion(
    string Id,
    string Question,
    IReadOnlyList<string>? Choices = null,
    bool AllowFreeform = true);

/// <summary>
/// Represents a user input response.
/// </summary>
/// <param name="Answers">Answers by question identifier.</param>
public sealed record AgentUserInputResponse(IReadOnlyDictionary<string, string> Answers);

/// <summary>
/// User input request handler delegate.
/// </summary>
/// <param name="request">The request.</param>
/// <param name="cancellationToken">A token to cancel the operation.</param>
public delegate Task<AgentUserInputResponse> AgentUserInputRequestHandler(
    AgentUserInputRequest request,
    CancellationToken cancellationToken);

