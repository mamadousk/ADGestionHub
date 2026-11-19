using System.ComponentModel.DataAnnotations;
using AdGestionHub.Services;

namespace AdGestionHub.Models.ViewModels;

public sealed class PublicPopupInput
{
    [Required(ErrorMessage = "Saisissez un titre.")]
    [StringLength(PublicPopup.MaxTitleLength, ErrorMessage = "Le titre ne doit pas dépasser 120 caractères.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Saisissez le message du pop-up.")]
    [StringLength(PublicPopupHtml.MaxInputLength, ErrorMessage = "Le message ne doit pas dépasser 6 000 caractères.")]
    public string MessageHtml { get; set; } = string.Empty;

    [Range(1, 720, ErrorMessage = "Choisissez une durée de 1 à 720 heures.")]
    public int HoursActive { get; set; } = 24;
}

public sealed record ManagePopupsViewModel(PublicPopupInput Input, IReadOnlyList<PublicPopup> History, string? PreviewHtml);
