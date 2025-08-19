namespace StockApi.Events;

public record OrderCreatedEvent(
    Guid OrderId,
    string? CustomerDocument,
    string? SellerName,
    DateTime CreatedAt,
    IReadOnlyList<OrderItemPayload> Items);

public record OrderItemPayload(Guid ProductId, int Quantity, decimal UnitPrice);
