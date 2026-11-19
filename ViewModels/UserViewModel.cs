using AdGestionHub.Services;

namespace AdGestionHub.Models.ViewModels;

public sealed class UserViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public StaffPermission Permissions { get; set; }
}
