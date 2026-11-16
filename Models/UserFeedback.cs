using System;
using System.ComponentModel.DataAnnotations;

namespace AdGestionHub.Models
{
    public class UserFeedback
    {
        public int Id { get; set; }

        [StringLength(256)]
        public string UserName { get; set; } = "Anonyme";

        [Range(1, 5)]
        public int Note { get; set; }

        [Required]
        [StringLength(2000)]
        public string Commentaire { get; set; } = string.Empty;

        public DateTime DateEnvoi { get; set; } = DateTime.Now;
    }
}
