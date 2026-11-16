using System;
using System.ComponentModel.DataAnnotations;

namespace AdGestionHub.Models
{
    public class Client
    {
        public int Id { get; set; }

        public int BoutiqueId { get; set; }
        public Boutique? Boutique { get; set; }

        [Required(ErrorMessage = "Le nom du client est obligatoire.")]
        [StringLength(150)]
        public string Name { get; set; } = string.Empty;

        [StringLength(40)]
        public string? Phone { get; set; }

        [EmailAddress]
        [StringLength(256)]
        public string? Email { get; set; }

        [StringLength(250)]
        public string? Address { get; set; }

        public bool IsBusinessClient { get; set; } = false;

        /// <summary>
        /// Identifiant fiscal du client professionnel (NIF, NCC, NINEA, TIN). Pertinent en mode Formalisé.
        /// </summary>
        [StringLength(100)]
        public string? TaxIdentificationNumber { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
