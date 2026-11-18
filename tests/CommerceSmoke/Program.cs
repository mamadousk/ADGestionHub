using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using AdGestionHub;
using AdGestionHub.Data;
using AdGestionHub.Models;
using AdGestionHub.Models.ViewModels;
using AdGestionHub.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-CD");
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
var directory = Directory.CreateTempSubdirectory("adgh-smoke-");
var sqlServer = Environment.GetEnvironmentVariable("ADGH_SMOKE_SQLSERVER");
var sqliteOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseSqlite($"Data Source={Path.Combine(directory.FullName, "test.db")};Pooling=False;Foreign Keys=True").Options;
try
{
    await using (var db = new ApplicationDbContext(sqliteOptions))
        await db.Database.EnsureCreatedAsync();
    await RunAsync(sqliteOptions);
    Console.WriteLine("Scénarios métier SQLite réussis.");

    var target = new SqlConnectionStringBuilder(sqlServer ?? "Server=localhost;Integrated Security=True")
    {
        InitialCatalog = "AdghSmoke_" + Guid.NewGuid().ToString("N")
    };
    var sqlOptions = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(target.ConnectionString)
        .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)).Options;
    await using var server = new ApplicationDbContext(sqlOptions);
    Check(server.GetService<IMigrator>().GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent)
        .Contains("CK_Payments_OneDocument", StringComparison.Ordinal), "script SQL Server généré depuis les migrations versionnées");
    if (sqlServer != null)
    {
        try
        {
            await server.GetService<IMigrator>().MigrateAsync("20261116180000_LocalizationFiscalQuotesAndClients");
            await SeedLegacyAsync(server);
            await server.Database.MigrateAsync();
            var imported = await server.Payments.AsNoTracking().SingleAsync();
            Check(imported.Amount == 150m && imported.AppliedAmount == 150m && imported.CurrencyCode == "USD", "reprise du règlement historique");
            Equal<decimal?>(2500m, imported.CdfPerUsd, "taux historique inversé depuis la devise du document");
            Equal<decimal?>(2500m, (await server.Sales.AsNoTracking().SingleAsync()).CdfPerUsd, "taux repris sur la vente");
            Equal<decimal?>(2700m, (await server.Quotes.AsNoTracking().SingleAsync()).CdfPerUsd, "taux distinct repris sur le devis");
            Equal(7, (await server.StockMovements.AsNoTracking().SingleAsync()).QuantityAfter, "reprise du stock initial");
            await RunAsync(sqlOptions);
            Console.WriteLine("Migration de la version précédente et scénarios SQL Server réussis.");
        }
        finally { await server.Database.EnsureDeletedAsync(); }
    }
}
finally { directory.Delete(recursive: true); }

static async Task SeedLegacyAsync(ApplicationDbContext db)
{
    await db.Database.ExecuteSqlRawAsync("""
        INSERT INTO Boutiques (Nom, Email, Telephone, Adresse, IsActive, CreatedAt)
          VALUES (N'Historique', N'historique@example.test', N'', N'', 1, SYSDATETIME());
        DECLARE @shop int = CAST(SCOPE_IDENTITY() AS int);
        INSERT INTO Sales (BoutiqueId, SaleDate, FinalPrice, CurrencyCode, CurrencySymbol, CurrencyDecimals, DocumentType, DocumentNumber,
          IsFormalizedSnapshot, IsVatEnabledSnapshot, SubtotalNet, TaxAmount, IsCancelled, PaymentMethod, SecondaryCurrencyCode, ExchangeRateToSecondary)
          VALUES (@shop, SYSDATETIME(), 150, N'USD', N'$', 2, N'Receipt', N'REC-00001', 0, 0, 0, 0, 0, N'Cash', N'CDF', 0.0004);
        INSERT INTO Quotes (BoutiqueId, DocumentNumber, CreatedAt, CustomerName, Status, IsVatEnabledSnapshot, CurrencyCode, CurrencySymbol,
          CurrencyDecimals, SecondaryCurrencyCode, ExchangeRateToSecondary, SubtotalNet, TaxAmount, TotalAmount)
          VALUES (@shop, N'PRO-00015', SYSDATETIME(), N'Ancien client', N'Pending', 0, N'CDF', N'FC', 2, N'USD', 2700, 5000, 0, 5000);
        INSERT INTO Products (BoutiqueId, [Name], PurchasePrice, SalePrice, StockQuantity, StockAlertThreshold, LowStockThreshold, UserId)
          VALUES (@shop, N'Ancien produit', 1, 2, 7, 5, 5, N'');
        """);
}

static async Task RunAsync(DbContextOptions<ApplicationDbContext> options)
{
    await using var db = new ApplicationDbContext(options);
    var http = new HttpContextAccessor();
    var writer = new CommerceWriter(db, http);
    var stock = new StockService(db);
    var operations = new CommerceOperations(db, writer, stock);
    var reports = new BusinessReportService(db);
    var today = DateTime.Today;
    var a = new Boutique { Nom = "Commerce pilote A", Email = "a@example.test" };
    var b = new Boutique { Nom = "Commerce pilote B", Email = "b@example.test" };
    db.Boutiques.AddRange(a, b);
    await db.SaveChangesAsync();
    var settings = CommerceWriter.DefaultSettings(a);
    settings.CdfPerUsd = 2800;
    settings.CustomPaymentMethods = "Bon boutique";
    var client = new Client { BoutiqueId = a.Id, Name = "- Client du test CSV" };
    var otherClient = new Client { BoutiqueId = b.Id, Name = "Client B" };
    var supplier = new Supplier { BoutiqueId = a.Id, Name = "Fournisseur A" };
    var otherSupplier = new Supplier { BoutiqueId = b.Id, Name = "Fournisseur B" };
    var rice = new Product { BoutiqueId = a.Id, Name = "Riz", SalePrice = 2800m, PurchasePrice = 1100m };
    var salt = new Product { BoutiqueId = a.Id, Name = "Sel", SalePrice = 700m, PurchasePrice = 400m };
    var foreign = new Product { BoutiqueId = b.Id, Name = "Stock B", SalePrice = 99m, StockQuantity = 10 };
    db.AddRange(settings, client, otherClient, supplier, otherSupplier, rice, salt, foreign);
    await db.SaveChangesAsync();
    await writer.ExecuteAsync(a.Id, async () =>
    {
        await stock.ApplyAsync(a.Id, rice, 5, "Stock initial", null, DateTime.Now);
        await stock.ApplyAsync(a.Id, salt, 1, "Stock initial", null, DateTime.Now);
        return true;
    });

    var sale = await operations.SellAsync(a.Id, new SaleInput
    {
        ClientId = client.Id, Discount = 300m, PaymentMode = "Partial", InitialPayment = 1400m,
        PaymentMethod = "M-Pesa", Items = [Line(rice, 2), Line(salt, 1)]
    }, canDiscount: true);
    Equal(6000m, sale.FinalPrice, "total net de la remise");
    Equal(4600m, sale.Balance, "solde de vente initial");
    Equal(3, await Quantity(rice), "décrément de stock");
    Equal(0, await Quantity(salt), "rupture à zéro");
    var moment = new ReportPeriod(today, today);
    BusinessReport cdf = await reports.BuildAsync(a.Id, settings, moment, "CDF", finance: true);
    Equal(6000m, cdf.Invoiced, "vente comptée une fois dans le tableau de bord");
    Equal(1400m, cdf.Received, "paiement distinct du total des ventes");
    Equal(4600m, cdf.Receivables, "créance client issue de la vente");
    Equal(2L, cdf.TopProducts.First().Units, "classement des produits vendus");

    await Throws<CommerceRuleException>(() => operations.SellAsync(a.Id, new SaleInput { ClientId = otherClient.Id, PaymentMode = "Unpaid", Items = [Line(rice, 1)] }, false));
    await Throws<CommerceRuleException>(() => operations.SellAsync(a.Id, new SaleInput { PaymentMethod = "Espèces", Items = [Line(foreign, 1)] }, false));
    await Throws<CommerceRuleException>(() => operations.SellAsync(a.Id, new SaleInput { PaymentMethod = "Espèces", Items = [Line(rice, 1), Line(salt, 1)] }, false));
    await Throws<CommerceRuleException>(() => operations.SellAsync(a.Id, new SaleInput { Discount = 1m, PaymentMethod = "Espèces", Items = [Line(rice, 1)] }, false));
    await Throws<CommerceRuleException>(() => operations.SellAsync(a.Id, new SaleInput { ExpectedTotal = 1m, PaymentMethod = "Espèces", Items = [Line(rice, 1)] }, true));
    await Throws<CommerceRuleException>(() => operations.CancelSaleAsync(a.Id, sale.Id, "Sans remboursement"));
    await Throws<CommerceNotFoundException>(() => operations.RecordPaymentAsync(b.Id, "Sale", sale.Id, PaymentOf(1m)));
    Equal(3, await Quantity(rice), "échec multi-lignes : tout le stock est restauré par rollback");
    Equal(10, await Quantity(foreign), "stock isolé");
    Equal(4, await db.StockMovements.CountAsync(m => m.BoutiqueId == a.Id), "aucun historique sur une vente rejetée");

    var earlier = today.AddDays(-1).AddHours(10);
    await db.Sales.Where(s => s.Id == sale.Id).ExecuteUpdateAsync(s => s
        .SetProperty(x => x.SaleDate, earlier).SetProperty(x => x.DocumentNumber, "FAC-00420"));
    await db.StoreSettings.Where(s => s.BoutiqueId == a.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.NextInvoiceNumber, 1));
    await db.Payments.Where(p => p.BoutiqueId == a.Id && p.SaleId == sale.Id)
        .ExecuteUpdateAsync(p => p.SetProperty(x => x.RecordedAt, earlier));
    await ChangeRate(3000m);
    var paidInUsd = await operations.RecordPaymentAsync(a.Id, "Sale", sale.Id, PaymentOf(1m, "USD", "Bon boutique"));
    Equal(3000m, paidInUsd.AppliedAmount, "règlement dans l'autre devise au taux du jour");
    Equal<decimal?>(3000m, paidInUsd.CdfPerUsd, "taux du règlement figé");
    Equal<decimal?>(2800m, (await db.Sales.AsNoTracking().SingleAsync(s => s.Id == sale.Id)).CdfPerUsd, "taux de vente inchangé");
    await Throws<CommerceRuleException>(() => operations.RecordPaymentAsync(a.Id, "Sale", sale.Id, PaymentOf(1600.01m)));

    async Task<bool> PayConcurrently()
    {
        await using var fresh = new ApplicationDbContext(options);
        try
        {
            await new CommerceOperations(fresh, new CommerceWriter(fresh, http), new StockService(fresh))
                .RecordPaymentAsync(a.Id, "Sale", sale.Id, PaymentOf(1600m));
            return true;
        }
        catch (CommerceRuleException) { return false; }
    }
    var results = await Task.WhenAll(Task.Run(PayConcurrently), Task.Run(PayConcurrently));
    Equal(1, results.Count(x => x), "deux règlements concurrents ne peuvent pas solder deux fois");
    var historical = await reports.BuildAsync(a.Id, settings, new ReportPeriod(earlier.Date, earlier.Date), "CDF", finance: true);
    Equal(4600m, historical.Receivables, "les règlements ultérieurs ne changent pas les soldes passés");
    Equal(1400m, historical.Received, "les règlements sont comptés à leur date propre");

    var instant = await operations.BuyAsync(a.Id, new PurchaseInput
    {
        SupplierId = supplier.Id, CurrencyCode = "USD", Items = [Line(rice, 4, 2.34m)], PaymentMode = "Unpaid", ReceiveNow = false
    });
    Equal(9.36m, instant.Total, "montant d'achat calculé exactement");
    Equal(3, await Quantity(rice), "pas de stock avant réception");
    Check(await operations.ReceiveAsync(a.Id, instant.Id), "réception effective");
    Check(!await operations.ReceiveAsync(a.Id, instant.Id), "réception non répétée");
    Equal(7, await Quantity(rice), "stock à la réception");
    await Throws<CommerceRuleException>(() => operations.BuyAsync(a.Id, new PurchaseInput
    {
        SupplierId = otherSupplier.Id, CurrencyCode = "USD", Items = [Line(rice, 1, 1m)], PaymentMode = "Unpaid"
    }));
    await operations.RecordPaymentAsync(a.Id, "Purchase", instant.Id, PaymentOf(2m, "USD"));
    await operations.RecordPaymentAsync(a.Id, "Receipt", null, new PaymentInput { CurrencyCode = "CDF", Amount = 150m, Description = "Autre recette" });
    var usd = await reports.BuildAsync(a.Id, settings, moment, "USD", finance: true);
    Equal(7.36m, usd.Payables, "dette fournisseur après paiement partiel");
    Equal(1m, usd.Received, "encaissement en USD sans mélanger les devises");
    Equal(2m, usd.SupplierPayments, "décaissement distinct de l'achat");
    Equal(0, (await reports.BuildAsync(b.Id, new StoreSettings(), moment, "CDF", finance: true)).Sales.Count, "rapport isolé");

    var quoteInput = new QuoteInput { ClientId = client.Id, CurrencyCode = "USD", Items = [Line(rice, 1)] };
    var quote = await operations.SaveQuoteAsync(a.Id, null, quoteInput);
    Equal(QuoteStatus.Draft, quote.Status, "brouillon créé");
    Equal(.93m, quote.TotalAmount, "prix converti au centime avant multiplication");
    await Throws<CommerceRuleException>(() => operations.ConvertQuoteAsync(a.Id, quote.Id));
    quoteInput.Publish = true;
    quote = await operations.SaveQuoteAsync(a.Id, quote.Id, quoteInput);
    Check(await operations.AcceptQuoteAsync(a.Id, quote.Id), "acceptation du devis");
    await ChangeRate(3300m);
    var convertedId = await operations.ConvertQuoteAsync(a.Id, quote.Id);
    Equal(convertedId, await operations.ConvertQuoteAsync(a.Id, quote.Id), "conversion idempotente");
    var converted = await db.Sales.AsNoTracking().Include(s => s.Items).Include(s => s.Payments).SingleAsync(s => s.Id == convertedId);
    Equal(.93m, converted.Balance, "prix du devis préservé, vente non réglée");
    Equal("FAC-00421", converted.Number, "numérotation existante préservée malgré un compteur à reprendre");
    Equal<decimal?>(3000m, converted.CdfPerUsd, "taux utilisé dans le devis préservé dans sa vente");
    Equal(6, await Quantity(rice), "stock décrémenté une seule fois lors de la conversion");
    Check(await operations.CancelSaleAsync(a.Id, convertedId, "Erreur documentée"), "annulation d'une vente non réglée");
    Equal(7, await Quantity(rice), "stock restauré à l'annulation");
    Check(await db.SystemLogs.CountAsync(l => l.BoutiqueId == a.Id) >= 8, "journalisation des opérations");

    var pdf = new PdfReportService();
    foreach (var document in new[] { pdf.SalePdf(converted, settings), pdf.QuotePdf(quote, settings), pdf.ReportPdf(usd) })
        Check(Encoding.ASCII.GetString(document, 0, 5) == "%PDF-", "document PDF valide");
    var csv = Encoding.UTF8.GetString(ReportExport.Csv(historical));
    Check(csv.StartsWith("\uFEFFRubrique;", StringComparison.Ordinal) && csv.Contains("'- Client du test CSV", StringComparison.Ordinal), "CSV encodé et texte client non interprété");
    using var zip = new ZipArchive(new MemoryStream(ReportExport.Excel(usd)), ZipArchiveMode.Read);
    using var sheet = zip.GetEntry("xl/worksheets/sheet1.xml")!.Open();
    XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    var workbook = XDocument.Load(sheet);
    Check(workbook.Descendants(ns + "v").Any(n => n.Value == "7.36") && !workbook.Descendants(ns + "f").Any(), "Excel contient de vraies cellules numériques et des libellés littéraux");
    Check(ReportPeriod.Current(today.AddDays(-365), today).Until == today.AddDays(1), "borne inclusive de la période");
    await Throws<CommerceRuleException>(() => Task.FromResult(ReportPeriod.Current(today, today.AddDays(-1))));

    Task<int> Quantity(Product product) => db.Products.Where(p => p.Id == product.Id).Select(p => p.StockQuantity).SingleAsync();
    Task<decimal> ChangeRate(decimal rate) => writer.ExecuteAsync(a.Id, async () =>
    {
        var row = await writer.SettingsAsync(a.Id);
        row.CdfPerUsd = rate;
        return rate;
    });
}

static TradeLineInput Line(Product product, int quantity, decimal? price = null) => new() { ProductId = product.Id, Quantity = quantity, UnitPrice = price };
static PaymentInput PaymentOf(decimal amount, string currency = "CDF", string method = "Espèces") => new() { Amount = amount, CurrencyCode = currency, Method = method };
static void Check(bool valid, string detail) { if (!valid) throw new InvalidOperationException(detail); }
static void Equal<T>(T expected, T actual, string detail) => Check(EqualityComparer<T>.Default.Equals(expected, actual), $"{detail} : attendu {expected}, obtenu {actual}");
static async Task Throws<T>(Func<Task> action) where T : Exception
{
    try { await action(); }
    catch (T) { return; }
    throw new InvalidOperationException($"Exception attendue {typeof(T).Name} non levée.");
}
