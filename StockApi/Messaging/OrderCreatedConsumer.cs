using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using StockApi.Events;
using System.Diagnostics.CodeAnalysis;

namespace StockApi.Messaging;

[ExcludeFromCodeCoverage]
public class OrderCreatedConsumer : BackgroundService
{
    private readonly ILogger<OrderCreatedConsumer> _logger;
    private readonly IConnection _conn;
    private readonly IModel _ch;

    public OrderCreatedConsumer(ILogger<OrderCreatedConsumer> logger)
    {
        _logger = logger;

        var host = Environment.GetEnvironmentVariable("RabbitMq__Host") ?? "localhost";
        var user = Environment.GetEnvironmentVariable("RabbitMq__User") ?? "guest";
        var pass = Environment.GetEnvironmentVariable("RabbitMq__Pass") ?? "guest";

        var factory = new ConnectionFactory { HostName = host, UserName = user, Password = pass, DispatchConsumersAsync = true };
        _conn = factory.CreateConnection("stockapi-consumer");
        _ch = _conn.CreateModel();

        _ch.ExchangeDeclare("stock.events", ExchangeType.Topic, durable: true);

        _ch.QueueDeclare("stock.orders.created", durable: true, exclusive: false, autoDelete: false);
        _ch.QueueBind("stock.orders.created", "stock.events", "orders.created");
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var consumer = new AsyncEventingBasicConsumer(_ch);
        consumer.Received += (_, ea) =>
        {
            try
            {
                var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                var evt = JsonSerializer.Deserialize<OrderCreatedEvent>(json);
                _logger.LogInformation("OrderCreated received: {@Event}", evt);
                _ch.BasicAck(ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process OrderCreated");
                _ch.BasicNack(ea.DeliveryTag, multiple: false, requeue: true);
            }

            return Task.CompletedTask;
        };

        _ch.BasicQos(0, prefetchCount: 10, global: false);
        _ch.BasicConsume("stock.orders.created", autoAck: false, consumer);

        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _ch?.Dispose();
        _conn?.Dispose();
        base.Dispose();
    }
}
