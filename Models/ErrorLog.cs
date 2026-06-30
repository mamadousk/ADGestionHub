namespace AdGestionHub.Models
{
    public class ErrorLog
    {
        public int Id { get; set; }
        public string? Message { get; set; }
        public string? StackTrace { get; set; }
        public string? ControllerName { get; set; }
        public string? ActionName { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string? UserEmail { get; set; } // Pour savoir quel client a eu l'erreur
        public int BoutiqueId { get; set; }
    }
}
