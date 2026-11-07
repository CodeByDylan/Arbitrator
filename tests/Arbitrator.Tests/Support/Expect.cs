using System.Diagnostics;
using TUnit.Assertions.Enums;

namespace Arbitrator.Tests.Support;

/// <summary>
/// Assertion shortcuts shared by the tests.
/// </summary>
public static class Expect
{
    /// <summary>
    /// Asserts that an asynchronous action fails with exactly the given exception type and returns the exception.
    /// </summary>
    /// <typeparam name="TException">The expected exception type, not a subclass.</typeparam>
    /// <param name="action">The action to run.</param>
    /// <returns>The thrown exception.</returns>
    public static async Task<TException> ThrowsExactlyAsync<TException>(Func<Task> action)
        where TException : Exception
    {
        TException? exception = await Assert.ThrowsExactlyAsync<TException>(action);
        return exception ?? throw new UnreachableException("The assertion fails when nothing is thrown.");
    }

    /// <summary>
    /// Asserts that a synchronous action fails with exactly the given exception type and returns the exception.
    /// </summary>
    /// <typeparam name="TException">The expected exception type, not a subclass.</typeparam>
    /// <param name="action">The action to run.</param>
    /// <returns>The thrown exception.</returns>
    public static TException ThrowsExactly<TException>(Action action)
        where TException : Exception
    {
        TException? exception = Assert.ThrowsExactly<TException>(action);
        return exception ?? throw new UnreachableException("The assertion fails when nothing is thrown.");
    }

    /// <summary>
    /// Asserts that a journal recorded exactly the given entries, in order.
    /// </summary>
    /// <param name="journal">The journal.</param>
    /// <param name="expectedEntries">The expected entries, oldest first.</param>
    /// <returns>A task that completes when the assertion has run.</returns>
    public static async Task EntriesAsync(Journal journal, params string[] expectedEntries)
    {
        await Assert.That(journal.Entries).IsEquivalentTo(expectedEntries, CollectionOrdering.Matching);
    }
}
