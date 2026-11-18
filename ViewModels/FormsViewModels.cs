using AdGestionHub.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AdGestionHub.Models.ViewModels
{
    public class ProductFormViewModel
    {
        [Required] [StringLength(10)] public string CatalogCurrencyCode { get; set; } = "CDF";

        [Required(ErrorMessage = "Le nom du produit est obligatoire.")]
        [StringLength(200, ErrorMessage = "Le nom ne peut pas dépasser 200 caractères.")]
        [Display(Name = "Nom du produit")]
        public string Name { get; set; } = string.Empty;

        [StringLength(60)]
        public string? Sku { get; set; }

        [Range(typeof(decimal), "0", Money.MaximumText, ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true, ErrorMessage = "Le prix d'achat doit être positif ou nul.")]
        public decimal PurchasePrice { get; set; }

        [Range(typeof(decimal), "0", Money.MaximumText, ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true, ErrorMessage = "Le prix de vente doit être positif ou nul.")]
        public decimal SalePrice { get; set; }

        [Range(0, 10000000, ErrorMessage = "La quantité doit être positive ou nulle.")]
        public int StockQuantity { get; set; }

        [Range(0, 1000000, ErrorMessage = "Le seuil d'alerte doit être positif ou nul.")]
        public int StockAlertThreshold { get; set; } = 5;

        [StringLength(100)] public string? Category { get; set; }
        [StringLength(1000)] public string? Description { get; set; }
    }

    public class ExpenseFormViewModel
    {
        [Required(ErrorMessage = "La description est obligatoire.")]
        [StringLength(250, ErrorMessage = "La description ne peut pas dépasser 250 caractères.")]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Le montant est obligatoire.")]
        [Range(typeof(decimal), "0.01", Money.MaximumText, ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true, ErrorMessage = "Le montant doit être supérieur à 0.")]
        public decimal Amount { get; set; }

        public DateTime Date { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "La catégorie est obligatoire.")]
        [StringLength(100)]
        public string Category { get; set; } = "Autre";

        [Required] [StringLength(10)] public string CurrencyCode { get; set; } = "CDF";
        [Required] [StringLength(50)] public string PaymentMethod { get; set; } = "Espèces";
    }

}
