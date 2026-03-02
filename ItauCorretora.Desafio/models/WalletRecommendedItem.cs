using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ItauCorretora.Desafio.Models
{
    [Table("WalletRecommendedItems")]
    public class WalletRecommendedItem
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("RecommendedWallet")]
        public int RecommendedWalletId { get; set; }
        public RecommendedWallet RecommendedWallet { get; set; } = null!;

        [Required]
        [ForeignKey("Action")]
        public int ActionId { get; set; }
        public Action Action { get; set; } = null!;

        [Required]
        [Column(TypeName = "decimal(5,2)")] 
        public decimal Weight { get; set; } 
    }
}