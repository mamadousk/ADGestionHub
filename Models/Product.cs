using System.ComponentModel.DataAnnotations;

namespace AdGestionHub;

public class Product
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Le nom du produit est obligatoire.")]
    [StringLength(200, ErrorMessage = "Le nom ne peut pas dépasser 200 caractères.")]
    [Display(Name = "Nom du produit")]
    public string Name { get; set; } = string.Empty;

    [StringLength(60)]
    [Display(Name = "Référence / SKU")]
    public string? Sku { get; set; }

    [Range(0, 100000000000, ErrorMessage = "Le prix d'achat doit être positif ou nul.")]
    [Display(Name = "Prix d'achat")]
    public decimal PurchasePrice { get; set; }

    [Range(0, 100000000000, ErrorMessage = "Le prix de vente doit être positif ou nul.")]
    [Display(Name = "Prix de vente")]
    public decimal SalePrice { get; set; }

    /// <summary>
    /// Taux de TVA spécifique au produit (%). Si null et que la TVA est activée, le taux standard de la boutique s'applique.
    /// Ignoré lorsque la TVA est désactivée.
    /// </summary>
    [Range(0, 100, ErrorMessage = "Le taux de TVA doit être compris entre 0 et 100.")]
    public decimal? VatRate { get; set; }

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

    [StringLength(100)]
    [Display(Name = "Catégorie")]
    public string? Category { get; set; }

    [StringLength(1000)]
    public string? Description { get; set; }

    public bool IsArchived { get; set; }

    public int? BoutiqueId { get; set; }
    public string UserId { get; set; } = string.Empty;
}
