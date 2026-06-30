using System;
using System.ComponentModel.DataAnnotations;

namespace AdGestionHub.Models
{
    public class GlobalNotification
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Message { get; set; } = string.Empty;

        public bool IsCritical { get; set; }

        // Ajouté pour la gestion d'affichage
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime ExpiryDate { get; set; }
    }
}