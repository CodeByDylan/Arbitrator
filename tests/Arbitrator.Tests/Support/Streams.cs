namespace Arbitrator.Tests.Support;

/// <summary>
/// Reads streams in tests.
/// </summary>
public static class Streams
{
    /// <summary>
    /// Reads every item of a stream.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="stream">The stream to read.</param>
    /// <returns>The items, in order.</returns>
    public static Task<List<T>> ReadAllAsync<T>(IAsyncEnumerable<T> stream)
    {
        return ReadAllAsync(stream, CancellationToken.None);
    }

    /// <summary>
    /// Reads every item of a stream, supplying a token for the read.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="stream">The stream to read.</param>
    /// <param name="readToken">The token passed when the read begins.</param>
    /// <returns>The items, in order.</returns>
    public static async Task<List<T>> ReadAllAsync<T>(IAsyncEnumerable<T> stream, CancellationToken readToken)
    {
        List<T> items = [];

        await foreach (T item in stream.WithCancellation(readToken))
        {
            items.Add(item);
        }

        return items;
    }
}
