using Autofac;
using EventBus;
using EventBus.Abstractions;
using RabbitMQServiceBus;
using AzureEventBusServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Microsoft.AspNetCore.Builder;
using EventBus.Events;

namespace EventBus.Extensions;

public static class ServiceCollectionExtensions
{
    public static IApplicationBuilder ConfigServiceBus<TEvent, TEventHandler>(this IApplicationBuilder app)
        where TEvent : IntegrationEvent
        where TEventHandler : IIntegrationEventHandler<TEvent>
    {
        if (app == null)
        {
            throw new ArgumentNullException(nameof(app));
        }

        var eventBus = app.ApplicationServices.GetRequiredService<IEventBus>();
        eventBus.Subscribe<TEvent, TEventHandler>();
        return app;
    }

    public static IServiceCollection AddEventHandler(this IServiceCollection services)
    {
        services.Scan(selector =>
        {
            selector.FromCallingAssembly()
            .AddClasses(x => x.AssignableTo(typeof(IIntegrationEventHandler<>)))
            .AsSelf()
            .WithTransientLifetime();
        });

        return services;
    }

    private static bool IsExistingEventBusService(IServiceCollection services)
    {
        // Get service provider from the provided IServiceCollection
        IServiceProvider serviceProvider = services.BuildServiceProvider();

        // Get EventBus
        IEventBus eventBus = serviceProvider.GetService<IEventBus>();

        return eventBus != null;
    }

    public static IServiceCollection AddAzureServiceBus(this IServiceCollection services, Action<AzureServiceBusSettings> busOpts)
    {
        if (services == null)
        {
            throw new ArgumentException(nameof(services));
        }

        services.Configure(busOpts);

        if (IsExistingEventBusService(services))
        {
            return services;
        }

        services.AddSingleton<IServiceBusPersisterConnection>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<AzureServiceBusSettings>>().Value;
            var serviceBusConnection = settings.EventBusConnection;

            return new DefaultServiceBusPersisterConnection(serviceBusConnection);
        });

        services.AddSingleton<IEventBusSubscriptionsManager, InMemoryEventBusSubscriptionsManager>();

        services.AddSingleton<IEventBus, EventBusServiceBus>(sp =>
        {
            var serviceBusPersisterConnection = sp.GetRequiredService<IServiceBusPersisterConnection>();
            var iLifetimeScope = sp.GetRequiredService<ILifetimeScope>();
            var logger = sp.GetRequiredService<ILogger<EventBusServiceBus>>();
            var eventBusSubcriptionsManager = sp.GetRequiredService<IEventBusSubscriptionsManager>();
            var settings = sp.GetRequiredService<IOptions<AzureServiceBusSettings>>().Value;
            string subscriptionName = settings.SubscriptionClientName;

            return new EventBusServiceBus(serviceBusPersisterConnection, logger,
                eventBusSubcriptionsManager, iLifetimeScope, subscriptionName);
        });

        return services;
    }

    public static IServiceCollection AddRabbitMQServiceBus(this IServiceCollection services, Action<RabbitMQServiceBusSettings> busOpts)
    {
        services.Configure(busOpts);

        if (IsExistingEventBusService(services))
        {
            return services;
        }

        services.AddSingleton<IRabbitMQPersistentConnection>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<RabbitMQServiceBusSettings>>().Value;
            var logger = sp.GetRequiredService<ILogger<DefaultRabbitMQPersistentConnection>>();

            var factory = new ConnectionFactory()
            {
                HostName = settings.EventBusConnection,
                DispatchConsumersAsync = true
            };

            if (!string.IsNullOrEmpty(settings.EventBusUserName))
            {
                factory.UserName = settings.EventBusUserName;
            }

            if (!string.IsNullOrEmpty(settings.EventBusPassword))
            {
                factory.Password = settings.EventBusPassword;
            }

            var retryCount = 5;
            if (!string.IsNullOrEmpty(settings.EventBusRetryCount))
            {
                retryCount = int.Parse(settings.EventBusRetryCount);
            }

            return new DefaultRabbitMQPersistentConnection(factory, logger, retryCount);
        });

        services.AddSingleton<IEventBusSubscriptionsManager, InMemoryEventBusSubscriptionsManager>();

        services.AddSingleton<IEventBus, EventBusRabbitMQ>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<RabbitMQServiceBusSettings>>().Value;
            var subscriptionClientName = settings.SubscriptionClientName;
            var rabbitMQPersistentConnection = sp.GetRequiredService<IRabbitMQPersistentConnection>();
            var iLifetimeScope = sp.GetRequiredService<ILifetimeScope>();
            var logger = sp.GetRequiredService<ILogger<EventBusRabbitMQ>>();
            var eventBusSubcriptionsManager = sp.GetRequiredService<IEventBusSubscriptionsManager>();

            var retryCount = 5;
            if (!string.IsNullOrEmpty(settings.EventBusRetryCount))
            {
                retryCount = int.Parse(settings.EventBusRetryCount);
            }

            return new EventBusRabbitMQ(rabbitMQPersistentConnection, logger, iLifetimeScope, eventBusSubcriptionsManager, subscriptionClientName, retryCount);
        });

        return services;
    }
}
