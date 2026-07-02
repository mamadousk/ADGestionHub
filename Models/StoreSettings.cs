using System.ComponentModel.DataAnnotations;

namespace AdGestionHub.Models
{
    public class StoreSettings
    {
        public int Id { get; set; }

        [Required]
        public int BoutiqueId { get; set; }

        [Display(Name = "Nom du magasin")]
        public string StoreName { get; set; }  // <- Nom de la boutique

        [Display(Name = "Adresse")]
        public string Address { get; set; }

        [Display(Name = "Téléphone")]
        public string Phone { get; set; }

        [Display(Name = "Email de contact")]
        public string ContactEmail { get; set; }

        [Display(Name = "Description")]
        public string Description { get; set; }

        [Display(Name = "Message de pied de page")]
        public string FooterMessage { get; set; }

        [Display(Name = "Boutique active")]
        public bool IsActive { get; set; }

        // Relation avec Boutique
        public Boutique Boutique { get; set; }
    }
}