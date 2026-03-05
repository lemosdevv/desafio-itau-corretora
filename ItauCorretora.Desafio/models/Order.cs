using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ItauCorretora.Desafio.Models
{
    public enum OrderType
    {
        Purchase,
        Sale
    }

    public enum StatusOrder
    {
        Pending,
        Executed,
        Cancelled,
        Error,
        Rejected,
        PartiallyExecuted,
        WaitingForExecution
    }

    [Table("Orders")]
    public class Order
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("Customer")]
        public int? CustomerId { get; set; }
        public Customer? Customer { get; set; } = null!;

        [ForeignKey("Account")]
        public int? AccountId { get; set; }
        public Account? Account { get; set; }

        [Required]
        [ForeignKey("Stock")]
        public int StockId { get; set; }
        public Stock Stock { get; set; } = null!;

        [Required]
        public DateTime Date { get; set; }

        [Required]
        public OrderType Type { get; set; } // Purchase or Sale

        [Required]
        public int Quantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; } // unit price at the time of ordering

        [Required]
        public StatusOrder Status { get; set; }

        // reason for cancellation/error
        [StringLength(200)]
        public string? Reason { get; set; }
    }
}