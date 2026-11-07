using System.Collections.Concurrent;

namespace Arbitrator.Tests.Support;

/// <summary>
/// Records what test handlers and behaviors observe, in the order they observe it.
/// </summary>
public sealed class Journal
{
    private readonly ConcurrentQueue<string> _entries = new();
    private readonly ConcurrentQueue<object> _captures = new();

    /// <summary>
    /// Gets the recorded entries, oldest first.
    /// </summary>
    public IReadOnlyList<string> Entries => [.. _entries];

    /// <summary>
    /// Records an event.
    /// </summary>
    /// <param name="entry">A short description of the event.</param>
    public void Record(string entry) => _entries.Enqueue(entry);

    /// <summary>
    /// Captures an object, such as a request, response, token, or component instance, for later assertions.
    /// </summary>
    /// <param name="value">The object to capture.</param>
    public void Capture(object value) => _captures.Enqueue(value);

    /// <summary>
    /// Gets the captured objects of a type, oldest first.
    /// </summary>
    /// <typeparam name="T">The type of the captured objects.</typeparam>
    /// <returns>The captured objects of the type.</returns>
    public IReadOnlyList<T> Captured<T>() => [.. _captures.OfType<T>()];
}
