namespace CodeAlta.Tests;

internal static class AsyncEnumerableTestExtensions
{
    public static async Task<T[]> ToArrayAsync<T>(this IAsyncEnumerable<T> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var results = new List<T>();
        await foreach (var item in source.ConfigureAwait(false))
        {
            results.Add(item);
        }

        return results.ToArray();
    }
}
