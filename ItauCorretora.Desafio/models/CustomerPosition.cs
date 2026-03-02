using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ItauCorretora.Desafio.Models
{
    [Table("CustomerPositions")]
    public class CustomerPosition
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("Customer")]
        public int CustomerId { get; set; }
        public Customer Customer { get; set; } = null!;

        [Required]
        [ForeignKey("Action")]
        public int ActionId { get; set; }
        public Action Action { get; set; } = null!;

        [Required]
        public int Quantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal AveragePrice { get; set; } 

        // Pode incluir data da última atualização
    }
}