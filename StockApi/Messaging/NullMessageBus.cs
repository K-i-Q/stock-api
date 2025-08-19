using System.Threading;
using System.Threading.Tasks;

namespace StockApi.Messaging;

public class NullMessageBus : IMessageBus
{
    public Task PublishAsync<T>(string exchange, string routingKey, T payload, CancellationToken ct = default)
        => Task.CompletedTask;
}
