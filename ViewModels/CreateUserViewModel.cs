using System.ComponentModel.DataAnnotations;

namespace AdGestionHub.Models.ViewModels
{
    public class CreateUserViewModel
    {
        [Required(ErrorMessage = "L'email est obligatoire.")]
        [EmailAddress(ErrorMessage = "Email invalide.")]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Le nom d'utilisateur est obligatoire.")]
        [Display(Name = "Nom d'utilisateur")]
        public string UserName { get; set; }

        [Required(ErrorMessage = "Le mot de passe est obligatoire.")]
        [DataType(DataType.Password)]
        [StringLength(100, ErrorMessage = "Le {0} doit comporter au moins {2} caractères.", MinimumLength = 6)]
        [Display(Name = "Mot de passe")]
        public string Password { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirmer le mot de passe")]
        [Compare("Password", ErrorMessage = "Les mots de passe ne correspondent pas.")]
        public string ConfirmPassword { get; set; }

        [Display(Name = "Rôle")]
        [Required(ErrorMessage = "Veuillez sélectionner un rôle.")]
        public string Role { get; set; }

        // Vous pouvez ajouter des champs supplémentaires (Nom, Prénom, Téléphone, etc.)
    }
}