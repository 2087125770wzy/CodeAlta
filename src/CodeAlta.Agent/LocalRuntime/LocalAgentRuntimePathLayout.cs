namespace CodeAlta.Agent.LocalRuntime;

/// <summary>
/// Provides the provider-first filesystem layout for local raw-API sessions.
/// </summary>
public sealed class LocalAgentRuntimePathLayout
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LocalAgentRuntimePathLayout"/> class.
    /// </summary>
    /// <param name="rootPath">Machine-scoped root path, typically <c>~/.codealta/local/agents</c>.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="rootPath" /> is empty.</exception>
    public LocalAgentRuntimePathLayout(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        RootPath = rootPath;
    }

    /// <summary>
    /// Gets the machine-scoped agents root path.
    /// </summary>
    public string RootPath { get; }

    /// <summary>
    /// Gets the provider root path.
    /// </summary>
    /// <param name="protocolFamily">Protocol family.</param>
    /// <param name="providerKey">Provider key.</param>
    /// <returns>The provider root path.</returns>
    public string GetProviderRootPath(string protocolFamily, string providerKey)
        => Path.Combine(RootPath, NormalizeSegment(protocolFamily), NormalizeSegment(providerKey));

    /// <summary>
    /// Gets the provider descriptor path.
    /// </summary>
    /// <param name="protocolFamily">Protocol family.</param>
    /// <param name="providerKey">Provider key.</param>
    /// <returns>The provider descriptor path.</returns>
    public string GetProviderDescriptorPath(string protocolFamily, string providerKey)
        => Path.Combine(GetProviderRootPath(protocolFamily, providerKey), "provider.json");

    /// <summary>
    /// Gets the sessions root path for a provider.
    /// </summary>
    /// <param name="protocolFamily">Protocol family.</param>
    /// <param name="providerKey">Provider key.</param>
    /// <returns>The provider sessions root path.</returns>
    public string GetProviderSessionsRootPath(string protocolFamily, string providerKey)
        => Path.Combine(GetProviderRootPath(protocolFamily, providerKey), "sessions");

    /// <summary>
    /// Gets the session root path.
    /// </summary>
    /// <param name="protocolFamily">Protocol family.</param>
    /// <param name="providerKey">Provider key.</param>
    /// <param name="sessionId">Local session identifier.</param>
    /// <param name="createdAt">Creation timestamp used for date sharding.</param>
    /// <returns>The session root path.</returns>
    public string GetSessionRootPath(
        string protocolFamily,
        string providerKey,
        string sessionId,
        DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        return Path.Combine(
            GetProviderSessionsRootPath(protocolFamily, providerKey),
            createdAt.UtcDateTime.ToString("yyyy", System.Globalization.CultureInfo.InvariantCulture),
            createdAt.UtcDateTime.ToString("MM", System.Globalization.CultureInfo.InvariantCulture),
            createdAt.UtcDateTime.ToString("dd", System.Globalization.CultureInfo.InvariantCulture),
            NormalizeSegment(sessionId));
    }

    /// <summary>
    /// Gets the session summary path.
    /// </summary>
    public string GetSessionSummaryPath(string sessionRootPath)
        => Path.Combine(sessionRootPath, "session.json");

    /// <summary>
    /// Gets the canonical events log path.
    /// </summary>
    public string GetSessionEventsPath(string sessionRootPath)
        => Path.Combine(sessionRootPath, "events.jsonl");

    /// <summary>
    /// Gets the session state path.
    /// </summary>
    public string GetSessionStatePath(string sessionRootPath)
        => Path.Combine(sessionRootPath, "state.json");

    /// <summary>
    /// Gets the attachments directory path.
    /// </summary>
    public string GetAttachmentsDirectoryPath(string sessionRootPath)
        => Path.Combine(sessionRootPath, "attachments");

    private static string NormalizeSegment(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var trimmed = value.Trim();
        return string.Concat(trimmed.Select(static c =>
            Path.GetInvalidFileNameChars().Contains(c) || c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar
                ? '_'
                : c));
    }
}
