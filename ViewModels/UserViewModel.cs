namespace AdGestionHub.Models.ViewModels
{
    public class UserViewModel
    {
        public string Id { get; set; }
        public string Email { get; set; }
        public string UserName { get; set; }
        public string FullName { get; set; } // si vous avez un champ nom/prénom
        public string Role { get; set; }
        public bool IsActive { get; set; } // si vous avez un champ actif
        public DateTime? CreatedAt { get; set; }
    }
}