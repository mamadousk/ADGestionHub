using System;
using System.ComponentModel.DataAnnotations;

namespace AdGestionHub.Models
{
    public class Debt
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Le nom du client est obligatoire.")]
        [StringLength(150)]
        [Display(Name = "Nom du client")]
        public string ClientName { get; set; } = string.Empty;

        [StringLength(40)]
        [Display(Name = "Téléphone")]
        public string? ClientPhone { get; set; }

        [Required(ErrorMessage = "Le montant est obligatoire.")]
        [Range(0.01, 100000000, ErrorMessage = "Le montant doit être supérieur à 0.")]
        [Display(Name = "Montant ($)")]
        public decimal Amount { get; set; }

        public DateTime DateCreated { get; set; } = DateTime.Now;

        [Display(Name = "Échéance")]
        public DateTime? DueDate { get; set; }

        public bool IsPaid { get; set; } = false;

        [StringLength(500)]
        public string? Note { get; set; }

        public int BoutiqueId { get; set; }
        public bool IsArchived { get; set; } = false;
    }
}
