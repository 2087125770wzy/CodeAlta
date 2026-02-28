namespace CodeAlta.Persistence;

/// <summary>
/// Provides durable operations for agents and agent sessions.
/// </summary>
public sealed class AgentRepository
{
    private readonly CodeAltaDb _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentRepository"/> class.
    /// </summary>
    /// <param name="db">Database accessor.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="db"/> is <see langword="null"/>.</exception>
    public AgentRepository(CodeAltaDb db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    /// <summary>
    /// Upserts an agent registration record.
    /// </summary>
    /// <param name="record">Agent record data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The upserted record.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="record"/> is <see langword="null"/>.</exception>
    public Task<AgentRecord> UpsertAgentAsync(
        AgentRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        return _db.ExecuteWriteAsync(
            async (connection, ct) =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    INSERT INTO agents(agent_id, role, scope_kind, scope_id, backend_id, created_at)
                    VALUES ($agent_id, $role, $scope_kind, $scope_id, $backend_id, $created_at)
                    ON CONFLICT(agent_id) DO UPDATE SET
                        role = excluded.role,
                        scope_kind = excluded.scope_kind,
                        scope_id = excluded.scope_id,
                        backend_id = excluded.backend_id;
                    """;
                command.Parameters.AddWithValue("$agent_id", record.AgentId.ToString());
                command.Parameters.AddWithValue("$role", record.Role);
                command.Parameters.AddWithValue("$scope_kind", record.ScopeKind);
                command.Parameters.AddWithValue("$scope_id", (object?)record.ScopeId ?? DBNull.Value);
                command.Parameters.AddWithValue("$backend_id", record.BackendId);
                command.Parameters.AddWithValue("$created_at", record.CreatedAt.ToString("O"));
                await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                return record;
            },
            cancellationToken);
    }

    /// <summary>
    /// Gets an agent by identifier.
    /// </summary>
    /// <param name="agentId">Agent identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The agent record when found; otherwise <see langword="null"/>.</returns>
    public Task<AgentRecord?> GetAgentAsync(
        AgentId agentId,
        CancellationToken cancellationToken = default)
    {
        return _db.ExecuteReadAsync(
            async (connection, ct) =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    SELECT agent_id, role, scope_kind, scope_id, backend_id, created_at
                    FROM agents
                    WHERE agent_id = $agent_id;
                    """;
                command.Parameters.AddWithValue("$agent_id", agentId.ToString());

                await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
                if (!await reader.ReadAsync(ct).ConfigureAwait(false))
                {
                    return null;
                }

                return new AgentRecord
                {
                    AgentId = AgentId.Parse(reader.GetString(0)),
                    Role = reader.GetString(1),
                    ScopeKind = reader.GetString(2),
                    ScopeId = reader.IsDBNull(3) ? null : reader.GetString(3),
                    BackendId = reader.GetString(4),
                    CreatedAt = DateTimeOffset.Parse(reader.GetString(5), provider: null),
                };
            },
            cancellationToken);
    }

    /// <summary>
    /// Lists registered agents.
    /// </summary>
    /// <param name="limit">Maximum number of returned agents.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The matching agent records ordered by creation timestamp descending.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is not positive.</exception>
    public Task<IReadOnlyList<AgentRecord>> ListAgentsAsync(
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be positive.");
        }

        return _db.ExecuteReadAsync<IReadOnlyList<AgentRecord>>(
            async (connection, ct) =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    SELECT agent_id, role, scope_kind, scope_id, backend_id, created_at
                    FROM agents
                    ORDER BY created_at DESC
                    LIMIT $limit;
                    """;
                command.Parameters.AddWithValue("$limit", limit);

                var results = new List<AgentRecord>();
                await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
                while (await reader.ReadAsync(ct).ConfigureAwait(false))
                {
                    results.Add(
                        new AgentRecord
                        {
                            AgentId = AgentId.Parse(reader.GetString(0)),
                            Role = reader.GetString(1),
                            ScopeKind = reader.GetString(2),
                            ScopeId = reader.IsDBNull(3) ? null : reader.GetString(3),
                            BackendId = reader.GetString(4),
                            CreatedAt = DateTimeOffset.Parse(reader.GetString(5), provider: null),
                        });
                }

                return results;
            },
            cancellationToken);
    }

    /// <summary>
    /// Upserts an agent session record.
    /// </summary>
    /// <param name="record">Session record data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The upserted session.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="record"/> is <see langword="null"/>.</exception>
    public Task<AgentSessionRecord> UpsertSessionAsync(
        AgentSessionRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        return _db.ExecuteWriteAsync(
            async (connection, ct) =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    INSERT INTO agent_sessions(session_id, agent_id, backend_session_id, created_at, last_used_at)
                    VALUES ($session_id, $agent_id, $backend_session_id, $created_at, $last_used_at)
                    ON CONFLICT(session_id) DO UPDATE SET
                        backend_session_id = excluded.backend_session_id,
                        last_used_at = excluded.last_used_at;
                    """;
                command.Parameters.AddWithValue("$session_id", record.SessionId);
                command.Parameters.AddWithValue("$agent_id", record.AgentId.ToString());
                command.Parameters.AddWithValue("$backend_session_id", (object?)record.BackendSessionId ?? DBNull.Value);
                command.Parameters.AddWithValue("$created_at", record.CreatedAt.ToString("O"));
                command.Parameters.AddWithValue("$last_used_at", record.LastUsedAt.ToString("O"));
                await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                return record;
            },
            cancellationToken);
    }

    /// <summary>
    /// Lists sessions for an agent.
    /// </summary>
    /// <param name="agentId">Agent identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The matching session records.</returns>
    public Task<IReadOnlyList<AgentSessionRecord>> ListSessionsAsync(
        AgentId agentId,
        CancellationToken cancellationToken = default)
    {
        return _db.ExecuteReadAsync<IReadOnlyList<AgentSessionRecord>>(
            async (connection, ct) =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    SELECT session_id, agent_id, backend_session_id, created_at, last_used_at
                    FROM agent_sessions
                    WHERE agent_id = $agent_id
                    ORDER BY last_used_at DESC;
                    """;
                command.Parameters.AddWithValue("$agent_id", agentId.ToString());

                var results = new List<AgentSessionRecord>();
                await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
                while (await reader.ReadAsync(ct).ConfigureAwait(false))
                {
                    results.Add(
                        new AgentSessionRecord
                        {
                            SessionId = reader.GetString(0),
                            AgentId = AgentId.Parse(reader.GetString(1)),
                            BackendSessionId = reader.IsDBNull(2) ? null : reader.GetString(2),
                            CreatedAt = DateTimeOffset.Parse(reader.GetString(3), provider: null),
                            LastUsedAt = DateTimeOffset.Parse(reader.GetString(4), provider: null),
                        });
                }

                return results;
            },
            cancellationToken);
    }
}
