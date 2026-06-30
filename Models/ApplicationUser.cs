using Microsoft.AspNetCore.Identity;

namespace AdGestionHub.Models
{
    // On hérite de IdentityUser pour garder les fonctions de mot de passe/email
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; }

        public string StoreName { get; set; }
        // C'est cette ligne qui permet le Multi-Tenant (SaaS)
        public int? BoutiqueId { get; set; }
    }
}