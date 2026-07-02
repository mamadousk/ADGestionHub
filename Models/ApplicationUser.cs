using Microsoft.AspNetCore.Identity;

namespace AdGestionHub.Models
{
    public class ApplicationUser : IdentityUser
    {
        public int? BoutiqueId { get; set; }
        public Boutique Boutique { get; set; }
        // ajoutez d'autres propriétés si besoin
    }
}