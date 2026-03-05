namespace ItauCorretora.Desafio.DTOs;

public class CreateWalletRequest
{
    public string Name { get; set; } = string.Empty;
    public List<WalletItemDto> Items { get; set; } = new();
}