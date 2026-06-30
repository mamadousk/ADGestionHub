using System;
using System.ComponentModel.DataAnnotations;

namespace AdGestionHub.Models
{
    public class Debt
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Le nom du client est obligatoire")]
        public string ClientName { get; set; }

        public string ClientPhone { get; set; }

        [Required]
        public decimal Amount { get; set; }

        public DateTime DateCreated { get; set; } = DateTime.Now;

        public DateTime? DueDate { get; set; }

        public bool IsPaid { get; set; } = false;

        public string? Note { get; set; }

        // CHAMPS POUR LE MULTI-BOUTIQUE ET L'ARCHIVAGE
        public int BoutiqueId { get; set; }
        public bool IsArchived { get; set; } = false;
    }
}