namespace AdGestionHub.Models
{
    public class SystemLog
    {
        public int Id { get; set; }
        public string ErrorMessage { get; set; }
        public string StackTrace { get; set; }
        public string UserEmail { get; set; }
        public DateTime DateOccurred { get; set; } = DateTime.Now;
        public int BoutiqueId { get; set; }
    }
}
