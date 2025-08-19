namespace StockApi.Models;
public class StockEntry
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public string InvoiceNumber { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
