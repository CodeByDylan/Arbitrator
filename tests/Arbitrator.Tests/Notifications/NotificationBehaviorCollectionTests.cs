using Arbitrator.Tests.Scanning;
using Arbitrator.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Arbitrator.Tests.Notifications;

/// <summary>
/// Specifies how notification behaviors apply to scanned handlers and to copied service collections.
/// </summary>
public sealed class NotificationBehaviorCollectionTests
{
    [Test]
    public async Task Behavior_registered_before_a_scan_wraps_every_scanned_handler()
    {
        StubAssembly assembly = StubAssembly.Defining(
            "Contoso.Notices",
            typeof(SecondAnnouncementHandler),
            typeof(FirstAnnouncementHandler));

        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddOpenBehavior(typeof(FirstNotificationBehavior<>))
            .AddHandlersFromAssembly(assembly));
        await host.Publisher.PublishAsync(new Announcement("hello"));

        await Expect.EntriesAsync(
            host.Journal,
            "NoticeFirst.before FirstAnnouncementHandler",
            "first",
            "NoticeFirst.after FirstAnnouncementHandler",
            "NoticeFirst.before SecondAnnouncementHandler",
            "second",
            "NoticeFirst.after SecondAnnouncementHandler");
    }

    [Test]
    public async Task Behavior_added_to_a_copied_collection_wraps_only_the_copys_handlers()
    {
        var original = new ServiceCollection();
        original.AddSingleton<Journal>();
        original.AddArbitrator(arbitrator => arbitrator.AddNotificationHandler<FirstAnnouncementHandler>());
        IServiceCollection copy = new ServiceCollection();

        foreach (ServiceDescriptor descriptor in original)
        {
            copy.Add(descriptor);
        }

        copy.AddArbitrator(arbitrator => arbitrator.AddOpenBehavior(typeof(FirstNotificationBehavior<>)));

        Journal fromOriginal = await PublishAsync(original);
        Journal fromCopy = await PublishAsync(copy);

        await Expect.EntriesAsync(fromOriginal, "first");
        await Expect.EntriesAsync(
            fromCopy,
            "NoticeFirst.before FirstAnnouncementHandler",
            "first",
            "NoticeFirst.after FirstAnnouncementHandler");
    }

    private static async Task<Journal> PublishAsync(IServiceCollection services)
    {
        await using ServiceProvider provider = services.BuildServiceProvider(TestHost.StrictValidation);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<IPublisher>().PublishAsync(new Announcement("hello"));
        return provider.GetRequiredService<Journal>();
    }
}
