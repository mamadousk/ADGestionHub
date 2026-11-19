using System.ComponentModel.DataAnnotations;

namespace AdGestionHub.Models.ViewModels;

public sealed class CreateUserViewModel : StaffUserInput
{
    [Required(ErrorMessage = "Le mot de passe est obligatoire.")]
    [DataType(DataType.Password)]
    [StringLength(100, ErrorMessage = "Le mot de passe doit comporter au moins {2} caractères.", MinimumLength = 8)]
    [Display(Name = "Mot de passe")]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Display(Name = "Confirmer le mot de passe")]
    [Compare(nameof(Password), ErrorMessage = "Les mots de passe ne correspondent pas.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
