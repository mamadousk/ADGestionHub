using AdGestionHub.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AdGestionHub.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Boutique> Boutiques { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Sale> Sales { get; set; }
        public DbSet<StoreSettings> StoreSettings { get; set; }
        public DbSet<Expense> Expenses { get; set; }
        public DbSet<WaitingListPro> WaitingListPros { get; set; }
        public DbSet<UserFeedback> UserFeedbacks { get; set; }
        public DbSet<Debt> Debts { get; set; }
        public DbSet<GlobalNotification> GlobalNotifications { get; set; }
        public DbSet<SystemLog> SystemLogs { get; set; }
        public DbSet<ErrorLog> ErrorLogs { get; set; }
        public DbSet<SaleItem> SaleItems { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // --- INDEXATION POUR PERFORMANCE MULTI-TENANT ---
            // On crée un index sur BoutiqueId pour chaque table qui stocke des données par boutique.
            // Cela permet à SQL de filtrer instantanément les données de l'utilisateur connecté.

            modelBuilder.Entity<StoreSettings>().HasIndex(s => s.BoutiqueId);
            modelBuilder.Entity<Product>().HasIndex(p => p.BoutiqueId);
            modelBuilder.Entity<Sale>().HasIndex(s => s.BoutiqueId);
            modelBuilder.Entity<SaleItem>().HasIndex(si => si.BoutiqueId);
            modelBuilder.Entity<Expense>().HasIndex(e => e.BoutiqueId);
            modelBuilder.Entity<Debt>().HasIndex(d => d.BoutiqueId);

            // Optionnel : Index sur les logs pour la maintenance
            modelBuilder.Entity<SystemLog>().HasIndex(sl => sl.BoutiqueId);
            modelBuilder.Entity<ErrorLog>().HasIndex(el => el.BoutiqueId);
            modelBuilder.Entity<Product>()
    .HasIndex(p => p.Barcode)
    .IsUnique()
    .HasFilter("[Barcode] IS NOT NULL");
        }
    }
}