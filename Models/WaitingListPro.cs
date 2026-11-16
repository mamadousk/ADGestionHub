using System;
using System.ComponentModel.DataAnnotations;

namespace AdGestionHub.Models
{
    public class WaitingListPro
    {
        [Key]
        public int Id { get; set; }

        [Required, EmailAddress, StringLength(256)]
        public string Email { get; set; } = string.Empty;

        public DateTime DateInscription { get; set; } = DateTime.Now;
    }
}
