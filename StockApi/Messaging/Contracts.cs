namespace StockApi.Messaging;

public record OrderItemMessage(Guid ProductId, int Quantity, decimal UnitPrice);

public record OrderCreated(
    Guid OrderId,
    DateTime CreatedAt,
    IReadOnlyCollection<OrderItemMessage> Items
);
