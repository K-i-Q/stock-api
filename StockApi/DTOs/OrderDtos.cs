using System.ComponentModel.DataAnnotations;

namespace StockApi.Dtos;

public class OrderItemRequest
{
    [Required] public int ProductId { get; set; }
    [Range(1, int.MaxValue)] public int Quantity { get; set; }
}

public class OrderRequest
{
    [Required, StringLength(50)] public string CustomerDocument { get; set; } = default!;
    [Required, StringLength(200)] public string SellerName { get; set; } = default!;
    [MinLength(1)] public List<OrderItemRequest> Items { get; set; } = new();
}