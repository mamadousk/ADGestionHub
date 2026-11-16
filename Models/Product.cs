using System.ComponentModel.DataAnnotations;

namespace AdGestionHub;

public class Product
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Le nom du produit est obligatoire.")]
    [StringLength(200, ErrorMessage = "Le nom ne peut pas dépasser 200 caractères.")]
    [Display(Name = "Nom du produit")]
    public string Name { get; set; } = string.Empty;

    [Range(0, 100000000, ErrorMessage = "Le prix d'achat doit être positif ou nul.")]
    [Display(Name = "Prix d'achat")]
    public decimal PurchasePrice { get; set; }

    [Range(0, 100000000, ErrorMessage = "Le prix de vente doit être positif ou nul.")]
    [Display(Name = "Prix de vente")]
    public decimal SalePrice { get; set; }

    [Range(0, 10000000, ErrorMessage = "La quantité doit être positive ou nulle.")]
    [Display(Name = "Quantité en stock")]
    public int StockQuantity { get; set; }

    [Range(0, 1000000, ErrorMessage = "Le seuil d'alerte doit être positif ou nul.")]
    [Display(Name = "Seuil d'alerte")]
    public int LowStockThreshold { get; set; } = 5;

    [StringLength(200)]
    [Display(Name = "Variations")]
    public string? Variations { get; set; }

    [Range(0, 1000000, ErrorMessage = "Le seuil d'alerte doit être positif ou nul.")]
    [Display(Name = "Seuil d'alerte")]
    public int StockAlertThreshold { get; set; } = 5;

    public int? BoutiqueId { get; set; }
    public string UserId { get; set; } = string.Empty;
}
