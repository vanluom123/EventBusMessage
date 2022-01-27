using System;
using RabbitMQ.Client;

namespace RabbitMQServiceBus;

public interface IRabbitMQPersistentConnection
    : IDisposable
{
    bool IsConnected { get; }

    bool TryConnect();

    IModel CreateModel();
}
