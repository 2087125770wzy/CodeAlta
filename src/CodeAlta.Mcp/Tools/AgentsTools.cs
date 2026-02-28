using System.ComponentModel;
using CodeAlta.Persistence;
using ModelContextProtocol.Server;

namespace CodeAlta.Mcp.Tools;

/// <summary>
/// MCP tools for agent registry operations.
/// </summary>
[McpServerToolType]
public sealed class AgentsTools
{
    private readonly AgentRepository _agentRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentsTools"/> class.
    /// </summary>
    /// <param name="agentRepository">Agent repository.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="agentRepository"/> is <see langword="null"/>.</exception>
    public AgentsTools(AgentRepository agentRepository)
    {
        ArgumentNullException.ThrowIfNull(agentRepository);
        _agentRepository = agentRepository;
    }

    /// <summary>
    /// Registers a new agent or updates an existing one.
    /// </summary>
    [McpServerTool(Name = "codealta.agents.register"), Description("Registers or updates an agent in the durable registry.")]
    public async Task<string> RegisterAsync(
        [Description("Agent role id.")] string role,
        [Description("Scope kind (global|workspace|project).")] string scopeKind,
        [Description("Backend id (codex|copilot|...).")] string backendId,
        [Description("Optional scope identifier for workspace/project scope.")] string? scopeId = null,
        [Description("Optional explicit agent identifier; generated when omitted.")] string? agentId = null,
        CancellationToken cancellationToken = default)
    {
        var parsedId = string.IsNullOrWhiteSpace(agentId)
            ? AgentId.NewVersion7()
            : AgentId.Parse(agentId);
        var now = DateTimeOffset.UtcNow;

        var upserted = await _agentRepository.UpsertAgentAsync(
            new AgentRecord
            {
                AgentId = parsedId,
                Role = role,
                ScopeKind = scopeKind,
                ScopeId = scopeId,
                BackendId = backendId,
                CreatedAt = now,
            },
            cancellationToken).ConfigureAwait(false);

        return McpToolJson.Serialize(ToContract(upserted));
    }

    /// <summary>
    /// Updates an existing agent registration.
    /// </summary>
    [McpServerTool(Name = "codealta.agents.update"), Description("Updates an existing agent registration.")]
    public async Task<string> UpdateAsync(
        [Description("Agent identifier.")] string agentId,
        [Description("Optional replacement role.")] string? role = null,
        [Description("Optional replacement scope kind.")] string? scopeKind = null,
        [Description("Optional replacement scope identifier.")] string? scopeId = null,
        [Description("Optional replacement backend id.")] string? backendId = null,
        CancellationToken cancellationToken = default)
    {
        var parsedId = AgentId.Parse(agentId);
        var existing = await _agentRepository.GetAgentAsync(parsedId, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            throw new InvalidOperationException($"Agent '{agentId}' was not found.");
        }

        var upserted = await _agentRepository.UpsertAgentAsync(
            new AgentRecord
            {
                AgentId = parsedId,
                Role = role ?? existing.Role,
                ScopeKind = scopeKind ?? existing.ScopeKind,
                ScopeId = scopeId ?? existing.ScopeId,
                BackendId = backendId ?? existing.BackendId,
                CreatedAt = existing.CreatedAt,
            },
            cancellationToken).ConfigureAwait(false);

        return McpToolJson.Serialize(ToContract(upserted));
    }

    /// <summary>
    /// Lists registered agents.
    /// </summary>
    [McpServerTool(Name = "codealta.agents.list"), Description("Lists registered agents.")]
    public async Task<string> ListAsync(
        [Description("Maximum number of returned agents.")] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var agents = await _agentRepository.ListAgentsAsync(limit, cancellationToken).ConfigureAwait(false);
        return McpToolJson.Serialize(agents.Select(ToContract).ToArray());
    }

    private static object ToContract(AgentRecord record)
    {
        return new
        {
            agentId = record.AgentId.ToString(),
            role = record.Role,
            scopeKind = record.ScopeKind,
            scopeId = record.ScopeId,
            backendId = record.BackendId,
            createdAt = record.CreatedAt,
        };
    }
}
