using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AdGestionHub.Models
{
    public class Sale
    {
        public int Id { get; set; }

        [Required]
        public DateTime SaleDate { get; set; } = DateTime.Now;

        public int? ClientId { get; set; }
        public virtual Client? Client { get; set; }

        public string? CustomerName { get; set; }

        [StringLength(100)]
        public string? CustomerTaxId { get; set; }

        public string? PaymentMethod { get; set; }

        // --- INSTANTANÉ DOCUMENTAIRE & FISCAL (NON-RÉTROACTIVITÉ) ---
        [StringLength(40)]
        public string DocumentNumber { get; set; } = string.Empty;

        /// <summary>
        /// "Invoice" (facture commerciale) ou "Receipt" pour un reçu historique.
        /// </summary>
        [StringLength(20)]
        public string DocumentType { get; set; } = "Receipt";

        public bool IsFormalizedSnapshot { get; set; } = false;
        public bool IsVatEnabledSnapshot { get; set; } = false;

        [StringLength(10)]
        public string CurrencyCode { get; set; } = "USD";

        [StringLength(15)]
        public string CurrencySymbol { get; set; } = "$";

        public int CurrencyDecimals { get; set; } = 2;

        [StringLength(10)]
        public string? SecondaryCurrencyCode { get; set; }

        public decimal? ExchangeRateToSecondary { get; set; }

        [StringLength(10)]
        public string? PaidInCurrencyCode { get; set; }

        [StringLength(100)]
        public string? StoreLegalTaxIdSnapshot { get; set; }

        [StringLength(30)]
        public string? StoreTaxIdLabelSnapshot { get; set; }

        [StringLength(100)]
        public string? StoreRccmSnapshot { get; set; }

        [StringLength(30)]
        public string? StoreRccmLabelSnapshot { get; set; }

        // --- MOTEUR DE CALCUL UNIFORME (NET HT + TAXE = TOTAL) ---
        public decimal SubtotalNet { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal FinalPrice { get; set; }
        public decimal Discount { get; set; }
        public decimal? CdfPerUsd { get; set; }
        public List<Payment> Payments { get; set; } = [];

        [NotMapped] public string Number => string.IsNullOrWhiteSpace(DocumentNumber) ? $"#{Id}" : DocumentNumber;
        [NotMapped] public int StockWarnings { get; set; }
        [NotMapped] public decimal Paid => Payments.Sum(p => p.AppliedAmount);
        [NotMapped] public decimal Balance => IsCancelled ? 0m : FinalPrice - Paid;
        [NotMapped] public string PaymentStatus => IsCancelled ? "Annulée" : Balance == 0 ? "Réglée" : Paid == 0 ? "Impayée" : "Partiellement réglée";

        public bool IsCancelled { get; set; } = false;
        public DateTime? CancelledAt { get; set; }

        [StringLength(300)]
        public string? CancellationReason { get; set; }

        public int BoutiqueId { get; set; }
        public virtual Boutique? Boutique { get; set; }

        public virtual ICollection<SaleItem> Items { get; set; } = new List<SaleItem>();

        [NotMapped]
        public decimal EffectiveSubtotalNet => (SubtotalNet == 0m && TaxAmount == 0m && FinalPrice > 0m)
            ? FinalPrice
            : SubtotalNet;
    }
}
