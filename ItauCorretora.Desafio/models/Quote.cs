using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ItauCorretora.Desafio.Models
{
    [Table("Quotes")]
    public class Quote
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("Action")]
        public int ActionId { get; set; }
        public Action Action { get; set; } = null!;

        [Required]
        public DateTime Date { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PriceOpening { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PriceClosing { get; set; }

        // Outros campos se necessário: minimo, maximo, volume
    }
}