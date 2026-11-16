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

            // Relation entre ApplicationUser et Boutique
            modelBuilder.Entity<ApplicationUser>()
                .HasOne(u => u.Boutique)
                .WithMany(b => b.Users)
                .HasForeignKey(u => u.BoutiqueId)
                .OnDelete(DeleteBehavior.Restrict);

            // Relations des journaux (BoutiqueId nullable pour les erreurs anonymes / SuperAdmin)
            modelBuilder.Entity<ErrorLog>()
                .HasOne<Boutique>()
                .WithMany(b => b.ErrorLogs)
                .HasForeignKey(e => e.BoutiqueId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<SystemLog>()
                .HasOne<Boutique>()
                .WithMany(b => b.SystemLogs)
                .HasForeignKey(s => s.BoutiqueId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<StoreSettings>()
                .HasOne(s => s.Boutique)
                .WithMany(b => b.StoreSettings)
                .HasForeignKey(s => s.BoutiqueId)
                .OnDelete(DeleteBehavior.Cascade);

            // Précision monétaire explicite
            modelBuilder.Entity<Product>().Property(p => p.PurchasePrice).HasPrecision(18, 2);
            modelBuilder.Entity<Product>().Property(p => p.SalePrice).HasPrecision(18, 2);
            modelBuilder.Entity<Sale>().Property(s => s.FinalPrice).HasPrecision(18, 2);
            modelBuilder.Entity<SaleItem>().Property(si => si.UnitPrice).HasPrecision(18, 2);
            modelBuilder.Entity<SaleItem>().Property(si => si.UnitCost).HasPrecision(18, 2);
            modelBuilder.Entity<Expense>().Property(e => e.Amount).HasPrecision(18, 2);
            modelBuilder.Entity<Debt>().Property(d => d.Amount).HasPrecision(18, 2);

            // --- INDEXATION POUR PERFORMANCE MULTI-TENANT ---
            modelBuilder.Entity<StoreSettings>().HasIndex(s => s.BoutiqueId);
            modelBuilder.Entity<Product>().HasIndex(p => p.BoutiqueId);
            modelBuilder.Entity<Sale>().HasIndex(s => new { s.BoutiqueId, s.SaleDate });
            modelBuilder.Entity<SaleItem>().HasIndex(si => si.BoutiqueId);
            modelBuilder.Entity<Expense>().HasIndex(e => new { e.BoutiqueId, e.Date });
            modelBuilder.Entity<Debt>().HasIndex(d => new { d.BoutiqueId, d.IsArchived, d.IsPaid });
            modelBuilder.Entity<SystemLog>().HasIndex(sl => sl.BoutiqueId);
            modelBuilder.Entity<ErrorLog>().HasIndex(el => el.BoutiqueId);
            modelBuilder.Entity<WaitingListPro>().HasIndex(w => w.Email).IsUnique();
        }
    }
}
