

    namespace AdGestionHub.Models
    {
        public class Boutique
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public DateTime CreatedAt { get; set; } = DateTime.Now;
            public string OwnerEmail { get; set; }
        public bool IsActive { get; set; } = true; // Par défaut, la boutique est active
    }
    }
