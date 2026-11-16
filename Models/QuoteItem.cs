using System.ComponentModel.DataAnnotations;

namespace AdGestionHub.Models
{
    public class QuoteItem
    {
        public int Id { get; set; }
        public int QuoteId { get; set; }
        public virtual Quote? Quote { get; set; }

        public int? ProductId { get; set; }

        [StringLength(200)]
        public string ProductName { get; set; } = string.Empty;

        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal VatRate { get; set; }
        public decimal LineNet { get; set; }
        public decimal LineTax { get; set; }
        public decimal LineTotal { get; set; }

        public int BoutiqueId { get; set; }
    }
}
