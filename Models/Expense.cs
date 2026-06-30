using System;
using System.ComponentModel.DataAnnotations;

namespace AdGestionHub.Models // <--- C'est cette ligne qui permet à la vue de te trouver
{
    public class Expense
    {
        public int Id { get; set; }

        [Required]
        public string Description { get; set; }

        [Required]
        public decimal Amount { get; set; }

        public DateTime Date { get; set; } = DateTime.Now;

        public string Category { get; set; }
        public int? BoutiqueId { get; set; }
        public string UserId { get; set; } = string.Empty;
    }
}