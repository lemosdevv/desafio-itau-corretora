using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ItauCorretora.Desafio.Models
{
    [Table("Actions")]
    public class Action
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(10)]
        public string Code { get; set; } = string.Empty;

        [StringLength(100)]
        public string? Name { get; set; }

        [StringLength(20)]
        public string? Type { get; set; } // ON, PN, etc.

        // Relationship 1:N with Cotacoes
        public ICollection<Quote> Quotes { get; set; } = new List<Quote>();

        // N:N Relationship via WalletRecommendedItem
        public ICollection<WalletRecommendedItem> WalletItems { get; set; } = new List<WalletRecommendedItem>();

        // Relationship 1:N with Ordens
        public ICollection<Order> Orders { get; set; } = new List<Order>();

        // Relationship 1:N with PosicaoCliente
        public ICollection<CustomerPosition> ClientPositions { get; set; } = new List<CustomerPosition>();
    }
}