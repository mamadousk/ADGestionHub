namespace AdGestionHub;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal PurchasePrice { get; set; } // Caché aux employés
    public decimal SalePrice { get; set; }
    public int StockQuantity { get; set; }
    public int LowStockThreshold { get; set; } // Pour l'alerte
    public string? Variations { get; set; } // Ex: "Rouge, XL"
    public int StockAlertThreshold { get; set; } = 5; // Seuil d'alerte (par défaut 5)
    public int? BoutiqueId { get; set; } // Le "?" permet d'accepter des produits sans boutique si besoin
    public string UserId { get; set; } = string.Empty;

}