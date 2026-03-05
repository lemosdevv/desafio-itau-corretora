namespace ItauCorretora.Desafio.Services.Interfaces;

public interface IConsolidatedPurchaseService
{
    Task<ConsolidatedPurchaseResult> ExecutePurchaseAsync(DateTime? referenceDate = null);
}

public class ConsolidatedPurchaseResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime ExecutionDate { get; set; }
    public int TotalClients { get; set; }
    public decimal TotalAmount { get; set; }
    public List<OrderSummary> Orders { get; set; } = new();
    public List<ClientDistribution> Distributions { get; set; } = new();
    public List<Residual> Residuals { get; set; } = new();
    public int IrEventsPublished { get; set; }
}

public class OrderSummary
{
    public string StockCode { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal TotalValue { get; set; }
    public List<OrderDetail> Details { get; set; } = new();
}

public class OrderDetail
{
    public string Tipo { get; set; } = string.Empty; // "LOTE" ou "FRACIONARIO"
    public string Ticker { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

public class ClientDistribution
{
    public int ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public List<AssetDistribution> Assets { get; set; } = new();
}

public class AssetDistribution
{
    public string Ticker { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

public class Residual
{
    public string Ticker { get; set; } = string.Empty;
    public int Quantity { get; set; }
}