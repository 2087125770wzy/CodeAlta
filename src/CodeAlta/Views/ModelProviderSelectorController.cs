namespace CodeAlta.Views;

internal sealed record ModelProviderSelectorController(
    Action<int> SelectProvider,
    Action<int> SelectModel,
    Action<int> SelectReasoning,
    Action CompactThread)
{
    public static ModelProviderSelectorController Create(
        Action<int> selectProvider,
        Action<int> selectModel,
        Action<int> selectReasoning,
        Action compactThread)
    {
        ArgumentNullException.ThrowIfNull(selectProvider);
        ArgumentNullException.ThrowIfNull(selectModel);
        ArgumentNullException.ThrowIfNull(selectReasoning);
        ArgumentNullException.ThrowIfNull(compactThread);
        return new ModelProviderSelectorController(selectProvider, selectModel, selectReasoning, compactThread);
    }
}
