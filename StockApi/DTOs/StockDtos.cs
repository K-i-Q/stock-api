namespace StockApi.Dtos;

public record StockEntryRequest(Guid ProductId, int Quantity, string InvoiceNumber);