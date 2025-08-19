using System.ComponentModel.DataAnnotations;

namespace StockApi.Dtos;

public class ProductUpsertRequest
{
    [Required, StringLength(200)] public string Name { get; set; } = default!;
    [Required, StringLength(2000)] public string Description { get; set; } = default!;
    [Range(0.01, double.MaxValue)] public decimal Price { get; set; }
}
