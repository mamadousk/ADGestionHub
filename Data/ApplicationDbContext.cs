using AdGestionHub.Models;
using Microsoft.AspNetCore.Identity;
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
        public DbSet<Client> Clients { get; set; }
        public DbSet<Sale> Sales { get; set; }
        public DbSet<SaleItem> SaleItems { get; set; }
        public DbSet<Quote> Quotes { get; set; }
        public DbSet<QuoteItem> QuoteItems { get; set; }
        public DbSet<StoreSettings> StoreSettings { get; set; }
        public DbSet<StoreLogo> StoreLogos { get; set; }
        public DbSet<TermsAcceptance> TermsAcceptances { get; set; }
        public DbSet<Expense> Expenses { get; set; }
        public DbSet<Debt> Debts { get; set; }
        public DbSet<Supplier> Suppliers { get; set; }
        public DbSet<Purchase> Purchases { get; set; }
        public DbSet<PurchaseItem> PurchaseItems { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<StockMovement> StockMovements { get; set; }
        public DbSet<EInvoicingOptIn> EInvoicingOptIns { get; set; }
        public DbSet<WaitingListPro> WaitingListPros { get; set; }
        public DbSet<UserFeedback> UserFeedbacks { get; set; }
        public DbSet<GlobalNotification> GlobalNotifications { get; set; }
        public DbSet<SystemLog> SystemLogs { get; set; }
        public DbSet<ErrorLog> ErrorLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Keep the Identity keys identical when EF tools or tests build the context without AddDefaultIdentity.
            modelBuilder.Entity<IdentityUserLogin<string>>().Property(l => l.LoginProvider).HasMaxLength(128);
            modelBuilder.Entity<IdentityUserLogin<string>>().Property(l => l.ProviderKey).HasMaxLength(128);
            modelBuilder.Entity<IdentityUserToken<string>>().Property(t => t.LoginProvider).HasMaxLength(128);
            modelBuilder.Entity<IdentityUserToken<string>>().Property(t => t.Name).HasMaxLength(128);

            // Relation entre ApplicationUser et Boutique
            modelBuilder.Entity<ApplicationUser>()
                .HasOne(u => u.Boutique)
                .WithMany(b => b.Users)
                .HasForeignKey(u => u.BoutiqueId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TermsAcceptance>()
                .HasIndex(a => new { a.UserId, a.Version }).IsUnique();
            modelBuilder.Entity<TermsAcceptance>()
                .HasOne<ApplicationUser>().WithMany().HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<StoreLogo>()
                .HasOne<Boutique>().WithOne().HasForeignKey<StoreLogo>(l => l.BoutiqueId)
                .OnDelete(DeleteBehavior.Cascade);

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

            modelBuilder.Entity<Client>()
                .HasOne(c => c.Boutique)
                .WithMany(b => b.Clients)
                .HasForeignKey(c => c.BoutiqueId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Sale>()
                .HasOne(s => s.Client)
                .WithMany()
                .HasForeignKey(s => s.ClientId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Quote>()
                .HasOne(q => q.Boutique)
                .WithMany(b => b.Quotes)
                .HasForeignKey(q => q.BoutiqueId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Quote>()
                .HasOne(q => q.Client)
                .WithMany()
                .HasForeignKey(q => q.ClientId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);

            // --- PRÉCISION MONÉTAIRE & FISCALE EXPLICITE ---
            modelBuilder.Entity<StoreSettings>().Property(s => s.SecondaryExchangeRate).HasPrecision(18, 6);
            modelBuilder.Entity<StoreSettings>().Property(s => s.DefaultVatRate).HasPrecision(5, 2);

            modelBuilder.Entity<Product>().Property(p => p.PurchasePrice).HasPrecision(18, 2);
            modelBuilder.Entity<Product>().Property(p => p.SalePrice).HasPrecision(18, 2);
            modelBuilder.Entity<Product>().Property(p => p.VatRate).HasPrecision(5, 2);

            modelBuilder.Entity<Sale>().Property(s => s.SubtotalNet).HasPrecision(18, 2);
            modelBuilder.Entity<Sale>().Property(s => s.TaxAmount).HasPrecision(18, 2);
            modelBuilder.Entity<Sale>().Property(s => s.FinalPrice).HasPrecision(18, 2);
            modelBuilder.Entity<Sale>().Property(s => s.ExchangeRateToSecondary).HasPrecision(18, 6);

            modelBuilder.Entity<SaleItem>().Property(si => si.UnitPrice).HasPrecision(18, 2);
            modelBuilder.Entity<SaleItem>().Property(si => si.UnitCost).HasPrecision(18, 2);
            modelBuilder.Entity<SaleItem>().Property(si => si.VatRate).HasPrecision(5, 2);
            modelBuilder.Entity<SaleItem>().Property(si => si.LineNet).HasPrecision(18, 2);
            modelBuilder.Entity<SaleItem>().Property(si => si.LineTax).HasPrecision(18, 2);
            modelBuilder.Entity<SaleItem>().Property(si => si.LineTotal).HasPrecision(18, 2);

            modelBuilder.Entity<Quote>().Property(q => q.SubtotalNet).HasPrecision(18, 2);
            modelBuilder.Entity<Quote>().Property(q => q.TaxAmount).HasPrecision(18, 2);
            modelBuilder.Entity<Quote>().Property(q => q.TotalAmount).HasPrecision(18, 2);
            modelBuilder.Entity<Quote>().Property(q => q.ExchangeRateToSecondary).HasPrecision(18, 6);

            modelBuilder.Entity<QuoteItem>().Property(qi => qi.UnitPrice).HasPrecision(18, 2);
            modelBuilder.Entity<QuoteItem>().Property(qi => qi.VatRate).HasPrecision(5, 2);
            modelBuilder.Entity<QuoteItem>().Property(qi => qi.LineNet).HasPrecision(18, 2);
            modelBuilder.Entity<QuoteItem>().Property(qi => qi.LineTax).HasPrecision(18, 2);
            modelBuilder.Entity<QuoteItem>().Property(qi => qi.LineTotal).HasPrecision(18, 2);

            modelBuilder.Entity<Expense>().Property(e => e.Amount).HasPrecision(18, 2);
            modelBuilder.Entity<Debt>().Property(d => d.Amount).HasPrecision(18, 2);

            // --- INDEXATION POUR PERFORMANCE MULTI-TENANT ---
            modelBuilder.Entity<StoreSettings>().HasIndex(s => s.BoutiqueId);
            modelBuilder.Entity<Product>().HasIndex(p => p.BoutiqueId);
            modelBuilder.Entity<Client>().HasIndex(c => new { c.BoutiqueId, c.Name });
            modelBuilder.Entity<Sale>().HasIndex(s => new { s.BoutiqueId, s.SaleDate });
            modelBuilder.Entity<SaleItem>().HasIndex(si => si.BoutiqueId);
            modelBuilder.Entity<Quote>().HasIndex(q => new { q.BoutiqueId, q.CreatedAt });
            modelBuilder.Entity<QuoteItem>().HasIndex(qi => qi.BoutiqueId);
            modelBuilder.Entity<Expense>().HasIndex(e => new { e.BoutiqueId, e.Date });
            modelBuilder.Entity<Debt>().HasIndex(d => new { d.BoutiqueId, d.IsArchived, d.IsPaid });
            modelBuilder.Entity<EInvoicingOptIn>().HasIndex(o => new { o.BoutiqueId, o.CountryCode });
            modelBuilder.Entity<SystemLog>().HasIndex(sl => sl.BoutiqueId);
            modelBuilder.Entity<ErrorLog>().HasIndex(el => el.BoutiqueId);
            modelBuilder.Entity<WaitingListPro>().HasIndex(w => w.Email).IsUnique();
            ConfigureCommerce(modelBuilder);
        }

        private static void ConfigureCommerce(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Supplier>().HasOne<Boutique>().WithMany()
                .HasForeignKey(s => s.BoutiqueId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<Supplier>().HasIndex(s => new { s.BoutiqueId, s.Name });

            modelBuilder.Entity<Purchase>().HasOne<Supplier>().WithMany()
                .HasForeignKey(p => new { p.SupplierId, p.BoutiqueId }).HasPrincipalKey(s => new { s.Id, s.BoutiqueId })
                .OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<Purchase>().HasIndex(p => new { p.BoutiqueId, p.RecordedAt });
            modelBuilder.Entity<PurchaseItem>().HasOne<Product>().WithMany()
                .HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.NoAction);

            var payments = modelBuilder.Entity<Payment>();
            payments.HasOne<Boutique>().WithMany().HasForeignKey(p => p.BoutiqueId).OnDelete(DeleteBehavior.NoAction);
            payments.HasOne<Sale>().WithMany(s => s.Payments)
                .HasForeignKey(p => new { p.SaleId, p.BoutiqueId }).HasPrincipalKey(s => new { s.Id, s.BoutiqueId })
                .OnDelete(DeleteBehavior.NoAction);
            payments.HasOne<Purchase>().WithMany(s => s.Payments)
                .HasForeignKey(p => new { p.PurchaseId, p.BoutiqueId }).HasPrincipalKey(s => new { s.Id, s.BoutiqueId })
                .OnDelete(DeleteBehavior.NoAction);
            payments.HasOne<Debt>().WithMany(s => s.Payments)
                .HasForeignKey(p => new { p.DebtId, p.BoutiqueId }).HasPrincipalKey(s => new { s.Id, s.BoutiqueId })
                .OnDelete(DeleteBehavior.NoAction);
            payments.HasIndex(p => new { p.BoutiqueId, p.RecordedAt });
            payments.ToTable("Payments", t => t.HasCheckConstraint("CK_Payments_OneDocument", """
                (CASE WHEN SaleId IS NULL THEN 0 ELSE 1 END + CASE WHEN PurchaseId IS NULL THEN 0 ELSE 1 END +
                 CASE WHEN DebtId IS NULL THEN 0 ELSE 1 END) <= 1
                """));

            var movements = modelBuilder.Entity<StockMovement>();
            movements.HasOne<Boutique>().WithMany().HasForeignKey(m => m.BoutiqueId).OnDelete(DeleteBehavior.NoAction);
            movements.HasOne<Product>().WithMany().HasForeignKey(m => m.ProductId).OnDelete(DeleteBehavior.NoAction);
            movements.HasIndex(m => new { m.BoutiqueId, m.ProductId, m.RecordedAt });
            movements.ToTable("StockMovements", t => t.HasCheckConstraint("CK_StockMovements_Quantity", "Delta <> 0 AND QuantityAfter >= 0"));

            modelBuilder.Entity<Sale>().Property(s => s.Discount).HasPrecision(18, 2);
            modelBuilder.Entity<Purchase>().Property(s => s.Total).HasPrecision(18, 2);
            modelBuilder.Entity<PurchaseItem>().Property(s => s.UnitPrice).HasPrecision(18, 2);
            payments.Property(s => s.Amount).HasPrecision(18, 2);
            payments.Property(s => s.AppliedAmount).HasPrecision(18, 2);

            modelBuilder.Entity<StoreSettings>().Property(s => s.CdfPerUsd).HasPrecision(18, 6);
            modelBuilder.Entity<Sale>().Property(s => s.CdfPerUsd).HasPrecision(18, 6);
            modelBuilder.Entity<Quote>().Property(s => s.CdfPerUsd).HasPrecision(18, 6);
            modelBuilder.Entity<Purchase>().Property(s => s.CdfPerUsd).HasPrecision(18, 6);
            modelBuilder.Entity<Expense>().Property(s => s.CdfPerUsd).HasPrecision(18, 6);
            payments.Property(p => p.CdfPerUsd).HasPrecision(18, 6);
        }
    }
}
