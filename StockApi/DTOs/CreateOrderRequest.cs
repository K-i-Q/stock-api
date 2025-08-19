using System.ComponentModel.DataAnnotations;

namespace StockApi.Dtos;

public class CreateOrderRequest
{
    [Required] public string CustomerDocument { get; set; } = string.Empty;
    [Required] public string SellerName { get; set; } = string.Empty;
    [MinLength(1)] public List<CreateOrderItemRequest> Items { get; set; } = new();
}

public class CreateOrderItemRequest
{
    [Required] public Guid ProductId { get; set; }
    [Range(1, int.MaxValue)] public int Quantity { get; set; }
}
