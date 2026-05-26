namespace CodeAlta.Agent;

/// <summary>
/// Provides read-only model-provider listing and runtime lookup.
/// </summary>
public interface IModelProviderRegistry
{
    /// <summary>
    /// Lists configured model providers.
    /// </summary>
    /// <param name="includeDisabled">Whether disabled providers should be included.</param>
    /// <returns>The configured provider descriptors.</returns>
    IReadOnlyList<ModelProviderDescriptor> ListProviders(bool includeDisabled = false);

    /// <summary>
    /// Attempts to get a configured provider descriptor.
    /// </summary>
    /// <param name="providerId">The provider identifier.</param>
    /// <param name="descriptor">The descriptor when found.</param>
    /// <returns><see langword="true" /> when the provider exists; otherwise <see langword="false" />.</returns>
    bool TryGetProvider(ModelProviderId providerId, out ModelProviderDescriptor descriptor);

    /// <summary>
    /// Gets or creates the runtime for a configured provider.
    /// </summary>
    /// <param name="providerId">The provider identifier.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The provider runtime.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="providerId" /> is empty.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when <paramref name="providerId" /> is not registered.</exception>
    ValueTask<IModelProviderRuntime> GetOrCreateRuntimeAsync(
        ModelProviderId providerId,
        CancellationToken cancellationToken = default);
}
