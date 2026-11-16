using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AdGestionHub.Models
{
    public class Quote
    {
        public int Id { get; set; }

        public int BoutiqueId { get; set; }
        public virtual Boutique? Boutique { get; set; }

        [Required]
        [StringLength(40)]
        public string DocumentNumber { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? ValidUntil { get; set; }

        public int? ClientId { get; set; }
        public virtual Client? Client { get; set; }

        [Required]
        [StringLength(150)]
        public string CustomerName { get; set; } = string.Empty;

        [StringLength(40)]
        public string? CustomerPhone { get; set; }

        [StringLength(100)]
        public string? CustomerTaxId { get; set; }

        /// <summary>
        /// "Pending", "Converted", "Expired"
        /// </summary>
        [StringLength(20)]
        public string Status { get; set; } = "Pending";

        public int? ConvertedSaleId { get; set; }

        // --- INSTANTANÉ FISCAL & DEVISE ---
        public bool IsVatEnabledSnapshot { get; set; } = false;

        [StringLength(10)]
        public string CurrencyCode { get; set; } = "CDF";

        [StringLength(15)]
        public string CurrencySymbol { get; set; } = "FC";

        public int CurrencyDecimals { get; set; } = 2;

        [StringLength(10)]
        public string? SecondaryCurrencyCode { get; set; }

        public decimal? ExchangeRateToSecondary { get; set; }

        [StringLength(100)]
        public string? StoreLegalTaxIdSnapshot { get; set; }

        [StringLength(30)]
        public string? StoreTaxIdLabelSnapshot { get; set; }

        [StringLength(100)]
        public string? StoreRccmSnapshot { get; set; }

        [StringLength(30)]
        public string? StoreRccmLabelSnapshot { get; set; }

        public decimal SubtotalNet { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal TotalAmount { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public virtual ICollection<QuoteItem> Items { get; set; } = new List<QuoteItem>();
    }
}
