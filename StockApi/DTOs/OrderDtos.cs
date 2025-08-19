namespace StockApi.Dtos;

public record OrderItemRequest(Guid ProductId, int Quantity);
public record CreateOrderRequest(string CustomerDocument, string SellerName, List<OrderItemRequest> Items);