using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace StockApi.Messaging;

public class RabbitMqMessageBus : IMessageBus, IDisposable
{
    private readonly IConnection _conn;
    private readonly IModel _ch;

    public RabbitMqMessageBus(string host, string user, string pass)
    {
        var factory = new ConnectionFactory
        {
            HostName = host,
            UserName = user,
            Password = pass,
            DispatchConsumersAsync = true
        };
        _conn = factory.CreateConnection("stockapi");
        _ch = _conn.CreateModel();

        _ch.ExchangeDeclare(exchange: "stock.events", type: ExchangeType.Topic, durable: true, autoDelete: false);
    }

    public Task PublishAsync<T>(string exchange, string routingKey, T payload, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(payload);
        var body = Encoding.UTF8.GetBytes(json);
        var props = _ch.CreateBasicProperties();
        props.ContentType = "application/json";
        props.DeliveryMode = 2;

        _ch.BasicPublish(exchange, routingKey, mandatory: false, basicProperties: props, body: body);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _ch?.Dispose();
        _conn?.Dispose();
    }
}
