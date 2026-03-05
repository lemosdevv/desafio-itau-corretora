namespace ItauCorretora.Desafio.Services.Interfaces;

public interface IRebalancementService
{
    Task RebalanceAllAsync();
    Task<RebalancementResult> RebalanceCustomerAsync(int customerId);
    Task RebalanceByWalletChangeAsync(int newWalletId);
}

public class RebalancementResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<GeneratedOrder> BuyOrders { get; set; } = new();
    public List<GeneratedOrder> SellOrders { get; set; } = new();
    public decimal TotalCost { get; set; }
}