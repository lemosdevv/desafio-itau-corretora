namespace ItauCorretora.Desafio.DTOs;

public class ClientSubscriptionRequest
{
    public string Name { get; set; } = string.Empty;
    public string CPF { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public decimal MonthlyValue { get; set; }
}