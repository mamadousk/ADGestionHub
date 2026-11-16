using System.ComponentModel.DataAnnotations;

namespace AdGestionHub.Models.ViewModels
{
    public class CreateUserViewModel
    {
        [Required(ErrorMessage = "L'email est obligatoire.")]
        [EmailAddress(ErrorMessage = "Email invalide.")]
        [StringLength(256)]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Le nom d'utilisateur est obligatoire.")]
        [StringLength(100)]
        [Display(Name = "Nom d'utilisateur")]
        public string UserName { get; set; } = string.Empty;

        [StringLength(150)]
        [Display(Name = "Nom complet")]
        public string? FullName { get; set; }

        [Required(ErrorMessage = "Le mot de passe est obligatoire.")]
        [DataType(DataType.Password)]
        [StringLength(100, ErrorMessage = "Le {0} doit comporter au moins {2} caractères.", MinimumLength = 8)]
        [Display(Name = "Mot de passe")]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Confirmer le mot de passe")]
        [Compare("Password", ErrorMessage = "Les mots de passe ne correspondent pas.")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Display(Name = "Rôle")]
        [Required(ErrorMessage = "Veuillez sélectionner un rôle.")]
        public string Role { get; set; } = "Employé";
    }
}
