namespace CodeAlta.Agent;

/// <summary>
/// Well-known model provider identifiers.
/// </summary>
public static class ModelProviderIds
{
    /// <summary>
    /// GitHub Copilot endpoint provider.
    /// </summary>
    public static readonly ModelProviderId Copilot = new("copilot");

    /// <summary>
    /// Codex endpoint provider.
    /// </summary>
    public static readonly ModelProviderId Codex = new("codex");

    /// <summary>
    /// OpenAI-compatible chat/completions provider type.
    /// </summary>
    public static readonly ModelProviderId OpenAIChat = new("openai-chat");

    /// <summary>
    /// OpenAI Responses provider type.
    /// </summary>
    public static readonly ModelProviderId OpenAIResponses = new("openai-responses");

    /// <summary>
    /// Anthropic Messages provider type.
    /// </summary>
    public static readonly ModelProviderId AnthropicMessages = new("anthropic-messages");

    /// <summary>
    /// Google GenAI provider type.
    /// </summary>
    public static readonly ModelProviderId GoogleGenAI = new("google-genai");
}
