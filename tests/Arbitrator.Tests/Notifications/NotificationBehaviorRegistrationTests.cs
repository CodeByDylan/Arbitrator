using Arbitrator.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Arbitrator.Tests.Notifications;

/// <summary>
/// Specifies how notification behaviors are registered through the existing builder methods, under the existing
/// rules.
/// </summary>
public sealed class NotificationBehaviorRegistrationTests
{
    [Test]
    public async Task Closed_behavior_with_request_and_notification_contracts_wraps_both()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddBehavior<PingAndAnnouncementBehavior>()
            .AddHandler<PingHandler>()
            .AddNotificationHandler<FirstAnnouncementHandler>());

        await host.Sender.SendAsync(new Ping("hello"));
        await host.Publisher.PublishAsync(new Announcement("hello"));

        await Expect.EntriesAsync(host.Journal, "around ping", "handler", "around announcement", "first");
    }

    [Test]
    public async Task Notification_behaviors_share_positions_with_request_behaviors()
    {
        ArbitratorConfigurationException exception = RegisterAndExpectFailure(arbitrator => arbitrator
            .AddOpenBehavior(typeof(FirstBehavior<,>))
            .AddOpenBehavior(typeof(FirstNotificationBehavior<>))
            .AddOpenBehavior(typeof(FirstNotificationBehavior<>)));

        await Assert.That(exception.Message).Contains("already registered at position 2");
    }

    [Test]
    [Arguments(typeof(TwoParameterNotificationBehavior<,>))]
    [Arguments(typeof(BothKindsBehavior<>))]
    public async Task Open_definition_without_exactly_one_behavior_shape_is_rejected(Type openBehaviorType)
    {
        ArbitratorConfigurationException exception = RegisterAndExpectFailure(
            arbitrator => arbitrator.AddOpenBehavior(openBehaviorType));

        await Assert.That(exception.Message).Contains("INotificationBehavior<TNotification> with one");
    }

    [Test]
    public async Task Closed_behavior_already_produced_by_an_open_behavior_is_rejected()
    {
        ArbitratorConfigurationException exception = RegisterAndExpectFailure(arbitrator => arbitrator
            .AddOpenBehavior(typeof(FirstNotificationBehavior<>))
            .AddNotificationHandler<FirstAnnouncementHandler>()
            .AddBehavior<FirstNotificationBehavior<Announcement>>());

        await Assert.That(exception.Message).Contains($"would run twice for notification '{Names.Of<Announcement>()}'");
    }

    [Test]
    public async Task Conflict_is_reported_when_the_first_handler_of_the_notification_arrives()
    {
        ArbitratorConfigurationException exception = RegisterAndExpectFailure(arbitrator => arbitrator
            .AddBehavior<FirstNotificationBehavior<Announcement>>()
            .AddOpenBehavior(typeof(FirstNotificationBehavior<>))
            .AddNotificationHandler<FirstAnnouncementHandler>());

        await Assert.That(exception.Message).Contains("would run twice for notification");
    }

    [Test]
    public async Task Closed_notification_behavior_without_handlers_fails_the_first_resolution()
    {
        var services = new ServiceCollection();
        services.AddArbitrator(arbitrator => arbitrator
            .AddBehavior<FirstNotificationBehavior<Announcement>>()
            .AddHandler<PingHandler>());
        await using ServiceProvider provider = services.BuildServiceProvider();

        ArbitratorConfigurationException exception = Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => provider.GetRequiredService<ArbitratorRegistrations>());

        await Assert.That(exception.Message).Contains($"implements INotificationBehavior<{Names.Of<Announcement>()}>");
        await Assert.That(exception.Message).Contains("none of the types it applies to has a registered handler");
    }

    [Test]
    public async Task Container_validation_reports_a_notification_behavior_with_an_unresolvable_dependency()
    {
        var services = new ServiceCollection();
        services.AddSingleton<Journal>();
        services.AddArbitrator(arbitrator => arbitrator
            .AddOpenBehavior(typeof(BehaviorWithMissingDependency<>))
            .AddNotificationHandler<FirstAnnouncementHandler>());

        AggregateException exception = Expect.ThrowsExactly<AggregateException>(
            () => services.BuildServiceProvider(TestHost.StrictValidation));

        await Assert.That(exception.Message).Contains("BehaviorWithMissingDependency");
    }

    [Test]
    public async Task Behavior_registered_after_the_provider_was_built_is_reported()
    {
        var services = new ServiceCollection();
        services.AddSingleton<Journal>();
        services.AddArbitrator(arbitrator => arbitrator.AddNotificationHandler<FirstAnnouncementHandler>());
        await using ServiceProvider provider = services.BuildServiceProvider();
        services.AddArbitrator(arbitrator => arbitrator.AddOpenBehavior(typeof(FirstNotificationBehavior<>)));

        ArbitratorConfigurationException exception = Expect.ThrowsExactly<ArbitratorConfigurationException>(
            () => provider.GetRequiredService<ArbitratorRegistrations>());

        await Assert.That(exception.Message).Contains("registered after this service provider was built");
    }

    [Test]
    public async Task Class_may_be_a_notification_handler_and_a_notification_behavior()
    {
        await using TestHost host = TestHost.Create(arbitrator => arbitrator
            .AddNotificationHandler<HandlerAndBehavior>()
            .AddBehavior<HandlerAndBehavior>()
            .AddNotificationHandler<SecondAnnouncementHandler>());

        await host.Publisher.PublishAsync(new Announcement("hello"));

        await Expect.EntriesAsync(host.Journal, "wrapping", "handling", "wrapping", "second");
    }

    private static ArbitratorConfigurationException RegisterAndExpectFailure(Action<ArbitratorBuilder> configure)
    {
        var services = new ServiceCollection();
        return Expect.ThrowsExactly<ArbitratorConfigurationException>(() => services.AddArbitrator(configure));
    }

    public sealed class PingAndAnnouncementBehavior(Journal journal)
        : IPipelineBehavior<Ping, Pong>, INotificationBehavior<Announcement>
    {
        public Task<Pong> HandleAsync(Ping request, RequestContinuation<Pong> next, CancellationToken cancellationToken)
        {
            journal.Record("around ping");
            return next(cancellationToken);
        }

        public Task HandleAsync(
            Announcement notification,
            Type handlerType,
            NotificationContinuation next,
            CancellationToken cancellationToken)
        {
            journal.Record("around announcement");
            return next(cancellationToken);
        }
    }

    public sealed class TwoParameterNotificationBehavior<TNotification, TOther> : INotificationBehavior<TNotification>
        where TNotification : INotification
    {
        public Task HandleAsync(
            TNotification notification,
            Type handlerType,
            NotificationContinuation next,
            CancellationToken cancellationToken) => next(cancellationToken);
    }

    public sealed class BothKindsBehavior<TMessage> : INotificationBehavior<TMessage>, IPipelineBehavior<TMessage, Unit>
        where TMessage : INotification, IRequest<Unit>
    {
        public Task HandleAsync(
            TMessage notification,
            Type handlerType,
            NotificationContinuation next,
            CancellationToken cancellationToken) => next(cancellationToken);

        public Task<Unit> HandleAsync(
            TMessage request,
            RequestContinuation<Unit> next,
            CancellationToken cancellationToken) => next(cancellationToken);
    }

    public interface IMissingDependency;

    public sealed class BehaviorWithMissingDependency<TNotification>(IMissingDependency dependency)
        : INotificationBehavior<TNotification>
        where TNotification : INotification
    {
        public Task HandleAsync(
            TNotification notification,
            Type handlerType,
            NotificationContinuation next,
            CancellationToken cancellationToken)
        {
            GC.KeepAlive(dependency);
            return next(cancellationToken);
        }
    }

    public sealed class HandlerAndBehavior(Journal journal)
        : INotificationHandler<Announcement>, INotificationBehavior<Announcement>
    {
        public Task HandleAsync(Announcement notification, CancellationToken cancellationToken)
        {
            journal.Record("handling");
            return Task.CompletedTask;
        }

        public Task HandleAsync(
            Announcement notification,
            Type handlerType,
            NotificationContinuation next,
            CancellationToken cancellationToken)
        {
            journal.Record("wrapping");
            return next(cancellationToken);
        }
    }
}
