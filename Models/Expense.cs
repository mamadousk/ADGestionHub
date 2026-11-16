using System;
using System.ComponentModel.DataAnnotations;

namespace AdGestionHub.Models
{
    public class Expense
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "La description est obligatoire.")]
        [StringLength(250, ErrorMessage = "La description ne peut pas dépasser 250 caractères.")]
        [Display(Name = "Description")]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Le montant est obligatoire.")]
        [Range(0.01, 100000000, ErrorMessage = "Le montant doit être supérieur à 0.")]
        [Display(Name = "Montant ($)")]
        public decimal Amount { get; set; }

        [Display(Name = "Date")]
        public DateTime Date { get; set; } = DateTime.Now;

        [Required(ErrorMessage = "La catégorie est obligatoire.")]
        [StringLength(100)]
        [Display(Name = "Catégorie")]
        public string Category { get; set; } = "Autre";

        public int? BoutiqueId { get; set; }
        public string UserId { get; set; } = string.Empty;
    }
}
