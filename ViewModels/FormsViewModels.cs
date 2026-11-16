using System;
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

        [Range(0, 100000000, ErrorMessage = "Le prix d'achat doit être positif ou nul.")]
        [Display(Name = "Prix d'achat ($)")]
        public decimal PurchasePrice { get; set; }

        [Range(0, 100000000, ErrorMessage = "Le prix de vente doit être positif ou nul.")]
        [Display(Name = "Prix de vente ($)")]
        public decimal SalePrice { get; set; }

        [Range(0, 10000000, ErrorMessage = "La quantité doit être positive ou nulle.")]
        [Display(Name = "Quantité en stock")]
        public int StockQuantity { get; set; }

        [Range(0, 1000000, ErrorMessage = "Le seuil d'alerte doit être positif ou nul.")]
        [Display(Name = "Seuil d'alerte")]
        public int StockAlertThreshold { get; set; } = 5;

        [StringLength(200)]
        [Display(Name = "Variations (optionnel)")]
        public string? Variations { get; set; }
    }

    public class ExpenseFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "La description est obligatoire.")]
        [StringLength(250, ErrorMessage = "La description ne peut pas dépasser 250 caractères.")]
        [Display(Name = "Description")]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Le montant est obligatoire.")]
        [Range(0.01, 100000000, ErrorMessage = "Le montant doit être supérieur à 0.")]
        [Display(Name = "Montant ($)")]
        public decimal Amount { get; set; }

        [Display(Name = "Date de l'opération")]
        public DateTime Date { get; set; } = DateTime.Now;

        [Required(ErrorMessage = "La catégorie est obligatoire.")]
        [StringLength(100)]
        [Display(Name = "Catégorie")]
        public string Category { get; set; } = "Autre";
    }

    public class DebtCreateViewModel
    {
        [Required(ErrorMessage = "Le nom du client est obligatoire.")]
        [StringLength(150)]
        public string ClientName { get; set; } = string.Empty;

        [StringLength(40)]
        public string? ClientPhone { get; set; }

        [Required(ErrorMessage = "Le montant est obligatoire.")]
        [Range(0.01, 100000000, ErrorMessage = "Le montant doit être supérieur à 0.")]
        public decimal Amount { get; set; }

        public DateTime? DueDate { get; set; }

        [StringLength(500)]
        public string? Note { get; set; }
    }
}
