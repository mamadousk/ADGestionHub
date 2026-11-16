using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AdGestionHub.Models
{
    public class SaleItem
    {
        public int Id { get; set; }
        public int SaleId { get; set; }
        public virtual Sale? Sale { get; set; }

        public int? ProductId { get; set; }

        [StringLength(200)]
        public string ProductName { get; set; } = string.Empty;

        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }

        /// <summary>
        /// Coût d'achat unitaire figé au moment de la vente.
        /// </summary>
        public decimal UnitCost { get; set; }

        // --- INSTANTANÉ DE CALCUL PAR LIGNE ---
        public decimal VatRate { get; set; }
        public decimal LineNet { get; set; }
        public decimal LineTax { get; set; }
        public decimal LineTotal { get; set; }

        public int BoutiqueId { get; set; }

        [NotMapped]
        public decimal EffectiveLineNet => LineNet > 0m ? LineNet : UnitPrice * Quantity;

        [NotMapped]
        public decimal EffectiveLineTotal => LineTotal > 0m ? LineTotal : (UnitPrice * Quantity) + LineTax;
    }
}
