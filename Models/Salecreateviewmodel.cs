using System.ComponentModel.DataAnnotations;

namespace AdGestionHub.Models
{
    // Ce que le navigateur a le droit d'envoyer pour créer une vente.
    // Tout le reste (boutique, date, total, nom du produit) est décidé par le serveur.
    public class SaleCreateViewModel
    {
        [StringLength(100)]
        public string? CustomerName { get; set; }

        [StringLength(30)]
        public string? PaymentMethod { get; set; }

        public List<SaleCreateItemViewModel> Items { get; set; } = new();
    }

    public class SaleCreateItemViewModel
    {
        [Range(1, int.MaxValue, ErrorMessage = "Produit invalide.")]
        public int ProductId { get; set; }

        [Range(1, 10000, ErrorMessage = "La quantité doit être comprise entre 1 et 10 000.")]
        public int Quantity { get; set; }

        // Utilisé uniquement si l'utilisateur est Admin (remise ou prix négocié). Ignoré pour les employés.
        public decimal? UnitPrice { get; set; }
    }
}