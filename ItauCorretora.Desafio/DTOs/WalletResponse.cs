namespace ItauCorretora.Desafio.DTOs;

public class WalletResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DeactivatedAt { get; set; }
    public List<WalletItemResponse> Items { get; set; } = new();
}

public class WalletItemResponse
{
    public string Ticker { get; set; } = string.Empty;
    public decimal Percentual { get; set; }
}