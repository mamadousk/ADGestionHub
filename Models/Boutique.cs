using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AdGestionHub.Models
{
    public class Boutique
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Le nom est obligatoire.")]
        [StringLength(150)]
        [Display(Name = "Nom de la boutique")]
        public string Nom { get; set; } = string.Empty;

        [StringLength(250)]
        [Display(Name = "Adresse")]
        public string Adresse { get; set; } = string.Empty;

        [StringLength(40)]
        [Display(Name = "Téléphone")]
        public string Telephone { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Email invalide.")]
        [StringLength(256)]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Nom")]
        public string Name => Nom;

        [Display(Name = "Email du propriétaire")]
        public string OwnerEmail => Email;

        [Display(Name = "Date de création")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public bool IsActive { get; set; } = true;
        public bool SetupPending { get; set; }

        // --- RELATIONS ---
        public ICollection<ApplicationUser> Users { get; set; } = new List<ApplicationUser>();
        public ICollection<Product> Products { get; set; } = new List<Product>();
        public ICollection<Client> Clients { get; set; } = new List<Client>();
        public ICollection<Sale> Sales { get; set; } = new List<Sale>();
        public ICollection<Quote> Quotes { get; set; } = new List<Quote>();
        public ICollection<StoreSettings> StoreSettings { get; set; } = new List<StoreSettings>();
        public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
        public ICollection<Debt> Debts { get; set; } = new List<Debt>();
        public ICollection<SystemLog> SystemLogs { get; set; } = new List<SystemLog>();
        public ICollection<ErrorLog> ErrorLogs { get; set; } = new List<ErrorLog>();
    }
}
