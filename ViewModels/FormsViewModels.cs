using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AdGestionHub.Models.ViewModels
{
    public class ProductFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Le nom du produit est obligatoire.")]
        [StringLength(200, ErrorMessage = "Le nom ne peut pas dépasser 200 caractères.")]
        [Display(Name = "Nom du produit")]
        public string Name { get; set; } = string.Empty;

        [StringLength(60)]
        public string? Sku { get; set; }

        [Range(0, 100000000000, ErrorMessage = "Le prix d'achat doit être positif ou nul.")]
        public decimal PurchasePrice { get; set; }

        [Range(0, 100000000000, ErrorMessage = "Le prix de vente doit être positif ou nul.")]
        public decimal SalePrice { get; set; }

        [Range(0, 100, ErrorMessage = "Le taux de TVA doit être compris entre 0 et 100 %.")]
        public decimal? VatRate { get; set; }

        [Range(0, 10000000, ErrorMessage = "La quantité doit être positive ou nulle.")]
        public int StockQuantity { get; set; }

        [Range(0, 1000000, ErrorMessage = "Le seuil d'alerte doit être positif ou nul.")]
        public int StockAlertThreshold { get; set; } = 5;

        [StringLength(200)]
        public string? Variations { get; set; }
    }

    public class ExpenseFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "La description est obligatoire.")]
        [StringLength(250, ErrorMessage = "La description ne peut pas dépasser 250 caractères.")]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Le montant est obligatoire.")]
        [Range(0.01, 100000000000, ErrorMessage = "Le montant doit être supérieur à 0.")]
        public decimal Amount { get; set; }

        public DateTime Date { get; set; } = DateTime.Now;

        [Required(ErrorMessage = "La catégorie est obligatoire.")]
        [StringLength(100)]
        public string Category { get; set; } = "Autre";
    }

    public class DebtCreateViewModel
    {
        public int? ClientId { get; set; }

        [Required(ErrorMessage = "Le nom du client est obligatoire.")]
        [StringLength(150)]
        public string ClientName { get; set; } = string.Empty;

        [StringLength(40)]
        public string? ClientPhone { get; set; }

        [StringLength(100)]
        public string? ClientTaxId { get; set; }

        [Required(ErrorMessage = "Le montant est obligatoire.")]
        [Range(0.01, 100000000000, ErrorMessage = "Le montant doit être supérieur à 0.")]
        public decimal Amount { get; set; }

        public DateTime? DueDate { get; set; }

        [StringLength(500)]
        public string? Note { get; set; }
    }

    public class ClientFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Le nom du client est obligatoire.")]
        [StringLength(150)]
        public string Name { get; set; } = string.Empty;

        [StringLength(40)]
        public string? Phone { get; set; }

        [EmailAddress(ErrorMessage = "Email invalide.")]
        [StringLength(256)]
        public string? Email { get; set; }

        [StringLength(250)]
        public string? Address { get; set; }

        public bool IsBusinessClient { get; set; }

        [StringLength(100)]
        public string? TaxIdentificationNumber { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }
    }

    public class QuoteCreateViewModel
    {
        public int? ClientId { get; set; }

        [Required(ErrorMessage = "Le nom du client est obligatoire.")]
        [StringLength(150)]
        public string CustomerName { get; set; } = string.Empty;

        [StringLength(40)]
        public string? CustomerPhone { get; set; }

        [StringLength(100)]
        public string? CustomerTaxId { get; set; }

        public DateTime? ValidUntil { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public List<SaleCreateItemViewModel> Items { get; set; } = new();
    }

    public class SettingsUpdateViewModel
    {
        public int Id { get; set; }
        public int BoutiqueId { get; set; }

        [Required(ErrorMessage = "Le nom de l'établissement est obligatoire.")]
        [StringLength(150)]
        public string StoreName { get; set; } = string.Empty;

        [StringLength(250)]
        public string? Address { get; set; }

        [StringLength(40)]
        public string? Phone { get; set; }

        [EmailAddress(ErrorMessage = "Email invalide.")]
        [StringLength(256)]
        public string? ContactEmail { get; set; }

        [StringLength(1000)]
        public string? Description { get; set; }

        [StringLength(300)]
        public string? FooterMessage { get; set; }

        // Localisation
        [Required]
        [StringLength(10)]
        public string CountryCode { get; set; } = "CD";

        [Required]
        [StringLength(10)]
        public string PreferredLanguage { get; set; } = "fr";

        // Devises & Taux
        [Required]
        [StringLength(10)]
        public string PrimaryCurrencyCode { get; set; } = "CDF";

        public bool SecondaryCurrencyEnabled { get; set; }

        [StringLength(10)]
        public string? SecondaryCurrencyCode { get; set; } = "USD";

        [Range(0.000001, 1000000000)]
        public decimal SecondaryExchangeRate { get; set; } = 2850m;

        // Fiscalité & Documents
        public bool IsFormalized { get; set; }

        [StringLength(100)]
        public string? TaxIdentificationNumber { get; set; }

        [StringLength(100)]
        public string? TradeRegisterNumber { get; set; }

        public bool IsVatEnabled { get; set; }

        [Range(0, 100)]
        public decimal DefaultVatRate { get; set; } = 16m;

        public string? ActiveTab { get; set; }
    }

    public class SaleEditMetadataViewModel
    {
        public int Id { get; set; }

        [StringLength(150)]
        public string? CustomerName { get; set; }

        [StringLength(50)]
        public string? PaymentMethod { get; set; }
    }
}
