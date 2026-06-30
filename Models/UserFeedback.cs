using System;

namespace AdGestionHub.Models
{
    public class UserFeedback
    {
        public int Id { get; set; }
        public string UserName { get; set; }

        // CORRECTION ICI : Ton code HTML cherche "Note", "Commentaire" et "DateEnvoi"
        public int Note { get; set; }
        public string Commentaire { get; set; }
        public DateTime DateEnvoi { get; set; } = DateTime.Now;
    }
}