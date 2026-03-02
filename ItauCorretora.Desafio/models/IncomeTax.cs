using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ItauCorretora.Desafio.Models
{
    [Table("IncomeTaxes")]
    public class IncomeTax
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("Customer")]
        public int CustomerId { get; set; }
        public Customer Customer { get; set; } = null!;

        [Required]
        public DateTime ReferenceDate { get; set; } // mês/ano

        [Column(TypeName = "decimal(18,2)")]
        public decimal AmountDue { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? PaidValue { get; set; }
    }
}