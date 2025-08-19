using System.ComponentModel.DataAnnotations;

namespace StockApi.Dtos;

public class StockEntryRequest
{
    [Required] public Guid ProductId { get; set; }
    [Range(1, int.MaxValue)] public int Quantity { get; set; }
    [Required, StringLength(100)] public string InvoiceNumber { get; set; } = string.Empty;
}
