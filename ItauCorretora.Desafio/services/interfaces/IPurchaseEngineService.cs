namespace ItauCorretora.Desafio.Services.Interfaces;

public interface IPurchaseEngineService
{
    Task<PurchaseResult> ProcessPurchaseAsync(int customerId, decimal amount);
}

public class PurchaseResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<GeneratedOrder> Orders { get; set; } = new();
    public decimal TotalInvested { get; set; }
}

public class GeneratedOrder
{
    public int StockId { get; set; }
    public string StockCode { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal TotalValue { get; set; }
}