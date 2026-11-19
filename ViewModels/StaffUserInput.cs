using System.ComponentModel.DataAnnotations;
using AdGestionHub.Services;

namespace AdGestionHub.Models.ViewModels;

public abstract class StaffUserInput
{
    [Required(ErrorMessage = "L’e-mail est obligatoire.")]
    [EmailAddress(ErrorMessage = "Adresse e-mail invalide.")]
    [StringLength(256)]
    [Display(Name = "E-mail")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "L’identifiant est obligatoire.")]
    [StringLength(100)]
    [Display(Name = "Identifiant")]
    public string UserName { get; set; } = string.Empty;

    [StringLength(150)]
    [Display(Name = "Nom complet")]
    public string? FullName { get; set; }

    // This form cannot grant Identity roles. An unexpected submitted role is rejected.
    public string Role { get; set; } = StaffAccessRules.EmployeeRole;
    public List<string> Permissions { get; set; } = [];
}
