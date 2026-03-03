using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ItauCorretora.Desafio.Models;

[Table("Quotes")]
public class Quote
{
    [Key]
    public int Id { get; set; }

    public int StockId { get; set; }
    public Stock Stock { get; set; } = null!;

    public DateTime Date { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal OpenPrice { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal ClosePrice { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal HighPrice { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal LowPrice { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal Volume { get; set; } 
}