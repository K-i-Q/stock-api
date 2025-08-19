namespace StockApi.Dtos;

public record ProductCreateRequest(string Name, string Description, decimal Price);
public record ProductUpdateRequest(string Name, string Description, decimal Price);