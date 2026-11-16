using System.ComponentModel.DataAnnotations;

namespace AdGestionHub.Models
{
    public class StoreSettings
    {
        public int Id { get; set; }

        [Required]
        public int BoutiqueId { get; set; }

        [Required(ErrorMessage = "Le nom du magasin est obligatoire.")]
        [StringLength(150)]
        [Display(Name = "Nom du magasin")]
        public string StoreName { get; set; } = string.Empty;

        [StringLength(250)]
        [Display(Name = "Adresse")]
        public string? Address { get; set; }

        [StringLength(40)]
        [Display(Name = "Téléphone")]
        public string? Phone { get; set; }

        [EmailAddress(ErrorMessage = "Email invalide.")]
        [StringLength(256)]
        [Display(Name = "Email de contact")]
        public string? ContactEmail { get; set; }

        [StringLength(1000)]
        [Display(Name = "Description")]
        public string? Description { get; set; }

        [StringLength(300)]
        [Display(Name = "Message de pied de page")]
        public string? FooterMessage { get; set; }

        [Display(Name = "Boutique active")]
        public bool IsActive { get; set; } = true;

        public Boutique? Boutique { get; set; }
    }
}
