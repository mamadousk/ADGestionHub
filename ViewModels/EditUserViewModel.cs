using System.ComponentModel.DataAnnotations;

namespace AdGestionHub.Models.ViewModels
{
    public class EditUserViewModel
    {
        public string Id { get; set; }

        [Required(ErrorMessage = "L'email est obligatoire.")]
        [EmailAddress(ErrorMessage = "Email invalide.")]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Le nom d'utilisateur est obligatoire.")]
        [Display(Name = "Nom d'utilisateur")]
        public string UserName { get; set; }

        [Display(Name = "Rôle")]
        [Required(ErrorMessage = "Veuillez sélectionner un rôle.")]
        public string Role { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Nouveau mot de passe (laisser vide pour ne pas changer)")]
        [StringLength(100, ErrorMessage = "Le {0} doit comporter au moins {2} caractères.", MinimumLength = 6)]
        public string NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirmer le nouveau mot de passe")]
        [Compare("NewPassword", ErrorMessage = "Les mots de passe ne correspondent pas.")]
        public string ConfirmNewPassword { get; set; }
    }
}