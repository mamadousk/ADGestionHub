using System.ComponentModel.DataAnnotations;

namespace AdGestionHub.Models
{
    public class WaitingListPro
    {
        [Key]
        public int Id { get; set; }
        [Required, EmailAddress]
        public string Email { get; set; }
        public DateTime DateInscription { get; set; } = DateTime.Now;
    }
}