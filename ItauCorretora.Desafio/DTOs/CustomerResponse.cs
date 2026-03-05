

namespace ItauCorretora.Desafio.DTOs;

public class CustomerResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CPF { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public decimal MonthlyValue { get; set; }
    public bool Active { get; set; }
    public DateTime SubscriptionDate { get; set; }
    public AccountGraphicsResponse? AccountGraphics { get; set; }
}

public class AccountGraphicsResponse
{
    public int Id { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; }
}