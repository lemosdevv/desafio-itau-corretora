using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ItauCorretora.Desafio.Models
{
    [Table("RecommendedWallets")]
    public class RecommendedWallet
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        public DateTime DateStart { get; set; }

        public DateTime? DateEnd { get; set; } 

        public ICollection<WalletRecommendedItem> Itens { get; set; } = new List<WalletRecommendedItem>();
    }
}