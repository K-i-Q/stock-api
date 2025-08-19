using System.ComponentModel.DataAnnotations;

namespace StockApi.Dtos;

public class ProductUpdateRequest
{
    [Required] public Guid Id { get; set; }

    [Required] public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Range(0.01, double.MaxValue)]
    public decimal Price { get; set; }
}
