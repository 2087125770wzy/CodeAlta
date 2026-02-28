using Microsoft.Extensions.DependencyInjection;

namespace CodeAlta.Mcp;

/// <summary>
/// Extension methods for registering CodeAlta MCP infrastructure.
/// </summary>
public static class CodeAltaMcpServiceCollectionExtensions
{
    /// <summary>
    /// Registers CodeAlta MCP server infrastructure.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configure">Optional MCP options configuration callback.</param>
    /// <returns><paramref name="services"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddCodeAltaMcp(
        this IServiceCollection services,
        Action<CodeAltaMcpOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new CodeAltaMcpOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<McpSessionRegistry>();
        services.AddSingleton<CodeAltaMcpServerFactory>();
        return services;
    }
}
