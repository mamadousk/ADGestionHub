using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AdGestionHub.Models
{
    public class SaleCreateViewModel
    {
        public int? ClientId { get; set; }

        [StringLength(150)]
        public string? CustomerName { get; set; }

        [StringLength(100)]
        public string? CustomerTaxId { get; set; }

        [StringLength(50)]
        public string? PaymentMethod { get; set; }

        [StringLength(10)]
        public string? PaidInCurrencyCode { get; set; }

        public List<SaleCreateItemViewModel> Items { get; set; } = new();
    }

    public class SaleCreateItemViewModel
    {
        [Range(1, int.MaxValue, ErrorMessage = "Produit invalide.")]
        public int ProductId { get; set; }

        [Range(1, 100000, ErrorMessage = "La quantité doit être comprise entre 1 et 100 000.")]
        public int Quantity { get; set; }

        public decimal? UnitPrice { get; set; }
    }
}
