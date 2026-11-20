using System.ComponentModel.DataAnnotations;

namespace AdGestionHub.Models
{
    // Persisted in the journal. Sale and Expense refer to creation, not later corrections.
    public enum OperationSubject { Sale = 1, Expense = 2, Client = 3 }

    public class SystemLog
    {
        public const string AuditPrefix = "[AUDIT] ";

        public int Id { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string? StackTrace { get; set; }
        public string? UserEmail { get; set; }
        public DateTime DateOccurred { get; set; } = DateTime.Now;
        public int? BoutiqueId { get; set; }

        // Snapshots, not foreign keys: account deletion must not erase the author of a write.
        [MaxLength(450)] public string? ActorUserId { get; set; }
        [MaxLength(150)] public string? ActorName { get; set; }
        public OperationSubject? Subject { get; set; }
        public int? SubjectId { get; set; }
    }
}
