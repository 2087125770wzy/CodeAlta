namespace CodeAlta.Views;

internal sealed record ThreadTabHostController(Action<int> SelectTab)
{
    public static ThreadTabHostController Create(Action<int> selectTab)
    {
        ArgumentNullException.ThrowIfNull(selectTab);
        return new ThreadTabHostController(selectTab);
    }
}
