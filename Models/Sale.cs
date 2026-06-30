using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AdGestionHub.Models
{
    public class Sale
    {
        public int Id { get; set; }

        [Required]
        public DateTime SaleDate { get; set; } = DateTime.Now;

        public string? CustomerName { get; set; }
        public string? PaymentMethod { get; set; }

        // Ajoute bien cette ligne, elle est utilisée partout !
        public decimal FinalPrice { get; set; }

        public int BoutiqueId { get; set; }
        public virtual Boutique? Boutique { get; set; }

        // Initialisation cruciale pour éviter les erreurs "null"
        public virtual ICollection<SaleItem> Items { get; set; } = new List<SaleItem>();
    }
}