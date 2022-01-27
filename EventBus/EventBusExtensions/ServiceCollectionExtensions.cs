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

    public static IServiceCollection AddEventBus(this IServiceCollection services, bool IsServiceBus = false)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        // Get service provider from the provided IServiceCollection
        IServiceProvider serviceProvider = services.BuildServiceProvider();

        // Get Event Bus service
        IEventBus eventBus = serviceProvider.GetRequiredService<IEventBus>();

        // Event Bus has not been registered yet
        if (eventBus == null)
        {
            if (IsServiceBus)
            {
                AddAzureServiceBus(services);
            }
            else
            {
                AddRabbitMQServiceBus(services);
            }
        }

        return services;
    }

    private static void AddAzureServiceBus(IServiceCollection services)
    {
        services.AddOptions<AzureServiceBusOptions>();

        services.AddSingleton<IServiceBusPersisterConnection>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<AzureServiceBusOptions>>().Value;
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
            var settings = sp.GetRequiredService<IOptions<AzureServiceBusOptions>>().Value;
            string subscriptionName = settings.SubscriptionClientName;

            return new EventBusServiceBus(serviceBusPersisterConnection, logger,
                eventBusSubcriptionsManager, iLifetimeScope, subscriptionName);
        });
    }

    private static void AddRabbitMQServiceBus(IServiceCollection services)
    {
        services.AddOptions<RabbitMQServiceBusOptions>();

        services.AddSingleton<IRabbitMQPersistentConnection>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<RabbitMQServiceBusOptions>>().Value;
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
            var settings = sp.GetRequiredService<IOptions<RabbitMQServiceBusOptions>>().Value;
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
    }
}
