using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ItauCorretora.Desafio.Models;

[Table("customers")]
public class Customer
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(11, MinimumLength = 11)]
    public string CPF { get; set; } = string.Empty;

    [EmailAddress]
    [StringLength(200)]
    public string? Email { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal MonthlyValue { get; set; }

    public bool Active { get; set; } = true;

    public DateTime SubscriptionDate { get; set; } = DateTime.Now;

    // 1:1 Account Relationship (a customer has an account)
    public Account? Account { get; set; }

    // 1:N Relationship with Orders
    public ICollection<Order> Orders { get; set; } = new List<Order>();

    // 1:N Relationship with Positions
    public ICollection<CustomerPosition> Positions { get; set; } = new List<CustomerPosition>();

    // 1:N Relationship with TaxesIncome (if keeping historical records)
    public ICollection<IncomeTax> TaxesIncome { get; set; } = new List<IncomeTax>();
}