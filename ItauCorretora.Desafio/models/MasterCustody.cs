using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ItauCorretora.Desafio.Models;

[Table("MasterCustodies")]
public class MasterCustody
{
    [Key]
    public int Id { get; set; }

    [Required]
    [ForeignKey("Stock")]
    public int StockId { get; set; }
    public Stock Stock { get; set; } = null!;

    [Required]
    public int Quantity { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal AveragePrice { get; set; } 
}