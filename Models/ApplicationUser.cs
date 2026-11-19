using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace AdGestionHub.Models
{
    public class ApplicationUser : IdentityUser
    {
        [StringLength(150)]
        public string? FullName { get; set; }

        public int? BoutiqueId { get; set; }
        public Boutique? Boutique { get; set; }

        // Null identifies an employee predating configurable access. Zero means no business features.
        public int? StaffAccessMask { get; set; }
    }
}
