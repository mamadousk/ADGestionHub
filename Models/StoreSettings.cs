using System.ComponentModel.DataAnnotations;

namespace AdGestionHub.Models
{
    public class StoreSettings
    {
        public int Id { get; set; }

        [Required]
        public int BoutiqueId { get; set; }

        [Required(ErrorMessage = "Le nom du magasin est obligatoire.")]
        [StringLength(150)]
        [Display(Name = "Nom du magasin")]
        public string StoreName { get; set; } = string.Empty;

        [StringLength(250)]
        [Display(Name = "Adresse")]
        public string? Address { get; set; }

        [StringLength(40)]
        [Display(Name = "Téléphone")]
        public string? Phone { get; set; }

        [EmailAddress(ErrorMessage = "Email invalide.")]
        [StringLength(256)]
        [Display(Name = "Email de contact")]
        public string? ContactEmail { get; set; }

        [StringLength(1000)]
        [Display(Name = "Description")]
        public string? Description { get; set; }

        [StringLength(300)]
        [Display(Name = "Message de pied de page")]
        public string? FooterMessage { get; set; }

        [Display(Name = "Boutique active")]
        public bool IsActive { get; set; } = true;

        // --- LOCALISATION & DEVISES ---
        [Required]
        [StringLength(10)]
        public string CountryCode { get; set; } = "CD";

        [Required]
        [StringLength(10)]
        public string PreferredLanguage { get; set; } = "fr";

        [Required]
        [StringLength(10)]
        public string PrimaryCurrencyCode { get; set; } = "CDF";

        public bool SecondaryCurrencyEnabled { get; set; } = false;

        [StringLength(10)]
        public string? SecondaryCurrencyCode { get; set; } = "USD";

        /// <summary>
        /// Taux de change : 1 unité de devise secondaire = X unités de devise principale (ex. 1 USD = 2850 CDF).
        /// </summary>
        public decimal SecondaryExchangeRate { get; set; } = 2850m;

        [Required]
        [StringLength(10)]
        public string PhoneDialCode { get; set; } = "+243";

        [Required]
        [StringLength(20)]
        public string DateFormat { get; set; } = "dd/MM/yyyy";

        // --- STATUT FISCAL & TVA ---
        public bool IsFormalized { get; set; } = false;

        [StringLength(100)]
        public string? TaxIdentificationNumber { get; set; }

        [StringLength(100)]
        public string? TradeRegisterNumber { get; set; }

        public bool IsVatEnabled { get; set; } = false;

        public decimal DefaultVatRate { get; set; } = 16m;

        // --- COMPTEURS SÉQUENTIELS PAR BOUTIQUE ---
        public int NextReceiptNumber { get; set; } = 1;
        public int NextInvoiceNumber { get; set; } = 1;
        public int NextQuoteNumber { get; set; } = 1;

        public Boutique? Boutique { get; set; }
    }
}
