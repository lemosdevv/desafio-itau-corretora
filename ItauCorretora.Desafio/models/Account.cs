using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ItauCorretora.Desafio.Models
{

    public enum AccountType{
        Master,
        Filhote
}

    [Table("Accounts")]
    public class Account
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Balance { get; set; }

        // Foreign key to Client
        [ForeignKey("Customer")]
        public int? CustomerId { get; set; }
        public Customer? Customer { get; set; } = null!;

        [Required]
        public AccountType Type { get; set; }

        public void Debit(decimal amount)
            {
                if (Balance < amount)
                    throw new InvalidOperationException("Insufficient funds.");
                Balance -= amount;
            }

            public void Credit(decimal amount)
            {
                Balance += amount;
            }

        // Account movements
        public ICollection<AccountMovement> Movements { get; set; } = new List<AccountMovement>();

        public ICollection<Order> Orders { get; set; } = new List<Order>();
    }
}