using System.ComponentModel.DataAnnotations;

namespace AdGestionHub.Models.ViewModels;

public sealed class EditUserViewModel : StaffUserInput
{
    [Required]
    public string Id { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Display(Name = "Nouveau mot de passe")]
    [StringLength(100, ErrorMessage = "Le mot de passe doit comporter au moins {2} caractères.", MinimumLength = 8)]
    public string? NewPassword { get; set; }

    [DataType(DataType.Password)]
    [Display(Name = "Confirmer le nouveau mot de passe")]
    [Compare(nameof(NewPassword), ErrorMessage = "Les mots de passe ne correspondent pas.")]
    public string? ConfirmNewPassword { get; set; }
}
