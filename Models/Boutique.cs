using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AdGestionHub.Models
{
    public class Boutique
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Le nom est obligatoire.")]
        [Display(Name = "Nom de la boutique")]
        public string Nom { get; set; }

        [Display(Name = "Adresse")]
        public string Adresse { get; set; }

        [Display(Name = "Téléphone")]
        public string Telephone { get; set; }

        [EmailAddress(ErrorMessage = "Email invalide.")]
        [Display(Name = "Email")]
        public string Email { get; set; }

        // --- NOUVELLES PROPRIÉTÉS pour la compatibilité avec les vues existantes ---
        // Propriété Name (alias de Nom) – utilisée dans certaines vues
        [Display(Name = "Nom")]
        public string Name => Nom;

        // Propriété OwnerEmail (email du propriétaire) – on suppose que c'est l'email de la boutique
        // Si vous voulez un vrai propriétaire, ajoutez un champ OwnerId (string) et une relation
        [Display(Name = "Email du propriétaire")]
        public string OwnerEmail => Email;

        // Propriété CreatedAt (date de création) – ajoutée avec valeur par défaut
        [Display(Name = "Date de création")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // --- RELATIONS ---
        public ICollection<ApplicationUser> Users { get; set; }
        public ICollection<Product> Products { get; set; }
        public ICollection<Sale> Sales { get; set; }
        public ICollection<StoreSettings> StoreSettings { get; set; }
        public ICollection<Expense> Expenses { get; set; }
        public ICollection<Debt> Debts { get; set; }
        public ICollection<SystemLog> SystemLogs { get; set; }
        public ICollection<ErrorLog> ErrorLogs { get; set; }
        public bool IsActive { get; set; } = true;
    }
}