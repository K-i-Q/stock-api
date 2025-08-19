namespace StockApi.Messaging;

public interface IMessageBus
{
    Task PublishAsync<T>(string exchange, string routingKey, T payload, CancellationToken ct = default);
}
