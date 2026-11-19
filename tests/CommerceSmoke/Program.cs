using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using AdGestionHub;
using AdGestionHub.Data;
using AdGestionHub.Models;
using AdGestionHub.Models.ViewModels;
using AdGestionHub.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Metadata;

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

    VerifyPopupHtml();

    var target = new SqlConnectionStringBuilder(sqlServer ?? "Server=localhost;Integrated Security=True")
    {
        InitialCatalog = "AdghSmoke_" + Guid.NewGuid().ToString("N")
    };
    var sqlOptions = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(target.ConnectionString).Options;
    await using var server = new ApplicationDbContext(sqlOptions);
    Check(server.GetService<IMigrator>().GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent)
        .Contains("CK_Payments_OneDocument", StringComparison.Ordinal), "script SQL Server généré depuis les migrations versionnées");
    const string onboarding = "20261118213000_PublicV1Onboarding";
    var popupUpgrade = server.GetService<IMigrator>().GenerateScript(fromMigration: onboarding, options: MigrationsSqlGenerationOptions.Idempotent);
    Check(popupUpgrade.Contains("CREATE TABLE [PublicPopups]", StringComparison.Ordinal)
        && !popupUpgrade.Contains("GlobalNotifications", StringComparison.Ordinal), "migration additive sans reprise publique des annonces existantes");
    CheckSnapshot(server);
    if (sqlServer != null)
    {
        try
        {
            const string legacy = "20261116180000_LocalizationFiscalQuotesAndClients";
            var migrator = server.GetService<IMigrator>();
            await migrator.MigrateAsync(legacy);
            await SeedLegacyAsync(server);
            const string previous = "20261118193000_CommerceMvp";
            var import = migrator.GenerateScript(fromMigration: legacy, toMigration: previous, options: MigrationsSqlGenerationOptions.Idempotent);
            await ApplyScriptAsync(server, import);
            await ApplyScriptAsync(server, import); // Must not duplicate imported payments or stock.
            await SeedV1Async(server);
            var upgrade = migrator.GenerateScript(fromMigration: previous, options: MigrationsSqlGenerationOptions.Idempotent);
            await ApplyScriptAsync(server, upgrade);
            await ApplyScriptAsync(server, upgrade);
            await server.Database.MigrateAsync();
            Check(!await server.TermsAcceptances.AnyAsync(), "aucune acceptation créée pour les comptes existants");
            Check(!await server.StoreLogos.AnyAsync() && !(await server.Boutiques.SingleAsync()).SetupPending, "pas de logo ni de configuration imposés par la migration");
            Check(!await server.PublicPopups.AnyAsync() && (await server.GlobalNotifications.SingleAsync()).Message == "Annonce historique privée",
                "l’ancienne annonce est conservée, aucun pop-up public n’est créé");
            var oldPurchase = await server.Purchases.AsNoTracking().Include(p => p.Items).Include(p => p.Payments).SingleAsync();
            Check(oldPurchase.Balance == 7.36m && oldPurchase.Items.Single().Quantity == 4 && oldPurchase.ReceivedAt == null, "achat et règlement historiques inchangés");
            var existingUser = await server.Users.AsNoTracking().SingleAsync();
            server.TermsAcceptances.Add(new TermsAcceptance { UserId = existingUser.Id, Version = LegalPublication.TermsVersion, AcceptedAtUtc = DateTimeOffset.UtcNow });
            await server.SaveChangesAsync();
            Check(await server.TermsAcceptances.CountAsync() == 1, "stockage d'une preuve d'acceptation sur le schéma migré");
            var imported = await server.Payments.AsNoTracking().SingleAsync(p => p.SaleId != null);
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
catch (Exception error)
{
    var annotation = error.ToString().Replace("%", "%25").Replace("\r", "%0D").Replace("\n", "%0A");
    Console.Error.WriteLine($"::error title=Échec du scénario métier ou de migration::{annotation}");
    throw;
}
finally { directory.Delete(recursive: true); }

static void VerifyPopupHtml()
{
    var sanitized = PublicPopupHtml.Prepare("""
        <h2 id="test">Annonce</h2><p style="color:red" onclick="return false">Texte <strong>mis en forme</strong>.</p>
        <ul><li>Un élément</li></ul><a href="/Infos/CGU" target="_blank">Conditions</a>
        <a href="https://example.test/info">Informations</a>
        <a href="http://example.test/">Lien non sécurisé</a><a href="//example.test/">Lien relatif au protocole</a>
        <a href="javascript:void(0)">Lien hors liste</a><a href="data:text/plain,texte">Lien de données</a>
        <script>texte non publiable</script><style>p{color:red}</style><iframe></iframe><img alt="Non autorisée">
        """);
    Check(sanitized != null, "publication avec du texte");
    var html = sanitized!;
    foreach (var allowed in new[] { "<h2>Annonce</h2>", "<strong>mis en forme</strong>", "<ul>", "href=\"/Infos/CGU\"", "href=\"https://example.test/info\"" })
        Check(html.Contains(allowed, StringComparison.Ordinal), $"mise en forme conservée : {allowed}");
    foreach (var forbidden in new[] { "style=", "onclick=", "id=", "target=", "<script", "<style", "<iframe", "<img", "texte non publiable", "href=\"http:", "href=\"//", "href=\"data:", "href=\"javascript:" })
        Check(!html.Contains(forbidden, StringComparison.OrdinalIgnoreCase), $"élément retiré : {forbidden}");
    Check(PublicPopupHtml.Prepare("<p>&nbsp;<br></p>") == null
        && PublicPopupHtml.Prepare(new string('A', PublicPopupHtml.MaxInputLength + 1)) == null,
        "un pop-up vide ou trop long n’est pas publiable");
    var now = DateTimeOffset.UtcNow;
    var popup = new PublicPopup { PublishedAtUtc = now, ExpiresAtUtc = now.AddHours(1) };
    Check(popup.IsVisibleAt(now) && !popup.IsVisibleAt(popup.ExpiresAtUtc), "expiration à l’heure prévue");
    popup.WithdrawnAtUtc = now;
    Check(!popup.IsVisibleAt(now), "retrait immédiat du prochain rendu");
}

static void CheckSnapshot(ApplicationDbContext db)
{
    if (!db.Database.HasPendingModelChanges()) return;
    var previous = db.GetService<IMigrationsAssembly>().ModelSnapshot?.Model;
    var source = previous == null ? null : db.GetService<IModelRuntimeInitializer>().Initialize(previous).GetRelationalModel();
    var target = db.GetService<IDesignTimeModel>().Model.GetRelationalModel();
    var changes = db.GetService<IMigrationsModelDiffer>().GetDifferences(source, target).Select(operation =>
    {
        var type = operation.GetType();
        return $"{type.Name} {type.GetProperty("Table")?.GetValue(operation)}.{type.GetProperty("Name")?.GetValue(operation)}";
    });
    var summary = string.Join("; ", changes);
    Console.Error.WriteLine($"::error title=Le modèle EF diverge du dernier snapshot::{summary}");
    throw new InvalidOperationException(summary);
}

static async Task ApplyScriptAsync(ApplicationDbContext db, string script)
{
    await db.Database.OpenConnectionAsync();
    try
    {
        foreach (var batch in Regex.Split(script, @"(?im)^GO[ \t]*\r?$"))
            if (!string.IsNullOrWhiteSpace(batch)) await db.Database.ExecuteSqlRawAsync(batch);
    }
    finally { await db.Database.CloseConnectionAsync(); }
}

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

static async Task SeedV1Async(ApplicationDbContext db) => await db.Database.ExecuteSqlRawAsync("""
    DECLARE @shop int = (SELECT Id FROM Boutiques WHERE Nom = N'Historique');
    DECLARE @product int = (SELECT Id FROM Products WHERE BoutiqueId = @shop);
    INSERT INTO AspNetUsers (Id, BoutiqueId, UserName, EmailConfirmed, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled, AccessFailedCount)
      VALUES (N'ancien-compte', @shop, N'archive@example.test', 0, 0, 0, 1, 0);
    INSERT INTO Suppliers (BoutiqueId, [Name]) VALUES (@shop, N'Fournisseur conservé');
    DECLARE @supplier int = CAST(SCOPE_IDENTITY() AS int);
    INSERT INTO Purchases (BoutiqueId, SupplierId, SupplierName, CurrencyCode, Total, RecordedAt)
      VALUES (@shop, @supplier, N'Fournisseur conservé', N'USD', 9.36, SYSDATETIME());
    DECLARE @purchase int = CAST(SCOPE_IDENTITY() AS int);
    INSERT INTO PurchaseItems (PurchaseId, ProductId, ProductName, Quantity, UnitPrice)
      VALUES (@purchase, @product, N'Ancien produit', 4, 2.34);
    INSERT INTO Payments (BoutiqueId, PurchaseId, Method, CurrencyCode, AppliedCurrencyCode, Amount, AppliedAmount, RecordedAt)
      VALUES (@shop, @purchase, N'Espèces', N'USD', N'USD', 2, 2, SYSDATETIME());
    INSERT INTO GlobalNotifications (Message, IsActive, IsCritical, CreatedAt, ExpiryDate)
      VALUES (N'Annonce historique privée', 1, 0, SYSDATETIME(), DATEADD(HOUR, 1, SYSDATETIME()));
    """);

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
    var rice = new Product { BoutiqueId = a.Id, Name = "Riz", SalePrice = 2800m, PurchasePrice = 1100m };
    var salt = new Product { BoutiqueId = a.Id, Name = "Sel", SalePrice = 700m, PurchasePrice = 400m };
    var foreign = new Product { BoutiqueId = b.Id, Name = "Stock B", SalePrice = 99m, StockQuantity = 10 };
    db.AddRange(settings, client, otherClient, supplier, rice, salt, foreign);
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

    // Historical bookkeeping still contributes to its own report section, not to receipts or spending.
    Check(!cdf.HasSupplierHistory, "pas d'archives fournisseurs affichées sur une boutique neuve");
    var oldPurchase = new Purchase
    {
        BoutiqueId = a.Id, SupplierId = supplier.Id, SupplierName = supplier.Name, CurrencyCode = "USD", Total = 9.36m,
        RecordedAt = today.AddHours(2),
        Items = [new PurchaseItem { ProductId = rice.Id, ProductName = rice.Name, Quantity = 4, UnitPrice = 2.34m }],
        Payments = [new Payment { BoutiqueId = a.Id, CurrencyCode = "USD", AppliedCurrencyCode = "USD", Amount = 2m,
            AppliedAmount = 2m, Method = "Espèces", RecordedAt = today.AddHours(3) }]
    };
    db.Purchases.Add(oldPurchase);
    await db.SaveChangesAsync();
    await Throws<CommerceNotFoundException>(() => operations.RecordPaymentAsync(a.Id, "Purchase", oldPurchase.Id, PaymentOf(2m, "USD")));
    Equal(3, await Quantity(rice), "un achat non reçu en archive ne change pas le stock");
    await operations.RecordPaymentAsync(a.Id, "Receipt", null, new PaymentInput { CurrencyCode = "CDF", Amount = 150m, Description = "Autre recette" });
    var usd = await reports.BuildAsync(a.Id, settings, moment, "USD", finance: true);
    Check(usd.HasSupplierHistory, "rubrique d'archives disponible lorsque pertinente");
    Equal(9.36m, usd.Purchased, "montant d'achat historique préservé");
    Equal(7.36m, usd.Payables, "solde fournisseur historique préservé");
    Equal(1m, usd.Received, "encaissement en USD sans mélanger les devises");
    Equal(2m, usd.SupplierPayments, "décaissement historique distinct de l'achat");
    Equal(0m, usd.Expensed, "aucun double compte d'un règlement historique en dépenses");
    Check(!(await reports.OutstandingAsync(a.Id, moment.Until)).Any(x => x.Kind == "Purchase"), "l'écran des créances ne contient pas de dettes fournisseurs");
    var empty = await reports.BuildAsync(b.Id, new StoreSettings(), moment, "USD", finance: true);
    Check(empty.Sales.Count == 0 && !empty.HasSupplierHistory && !Encoding.UTF8.GetString(ReportExport.Csv(empty)).Contains("fournisseur"), "rapport et archives isolés");

    var second = await operations.SellAsync(a.Id, new SaleInput
    {
        ClientId = client.Id, CurrencyCode = "USD", PaymentMode = "Unpaid", Items = [Line(rice, 1)], ExpectedTotal = .93m
    }, canDiscount: true);
    Equal(.93m, second.Balance, "prix converti au centime avant multiplication");
    Equal("FAC-00421", second.Number, "numérotation existante préservée malgré un compteur à reprendre");
    Equal<decimal?>(3000m, second.CdfPerUsd, "taux enregistré sur la vente en USD");
    Equal(2, await Quantity(rice), "stock décrémenté lors de la vente");
    Check(await operations.CancelSaleAsync(a.Id, second.Id, "Erreur documentée"), "annulation d'une vente non réglée");
    Equal(3, await Quantity(rice), "stock restauré à l'annulation");
    Equal(6, await db.SystemLogs.CountAsync(l => l.BoutiqueId == a.Id), "audit des opérations validées, sans doublon ni écriture pour les actions refusées");

    var pdf = new PdfReportService();
    var png = await VerifyLogoAsync();
    db.StoreLogos.Add(new StoreLogo { BoutiqueId = a.Id, Png = png });
    await db.SaveChangesAsync();
    var logo = await db.StoreLogos.AsNoTracking().SingleAsync(l => l.BoutiqueId == a.Id);
    foreach (var document in new[] { pdf.SalePdf(second, settings), pdf.SalePdf(second, settings, logo.Png), pdf.ReportPdf(usd) })
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

static async Task<byte[]> VerifyLogoAsync()
{
    using var bitmap = new SkiaSharp.SKBitmap(900, 300, true);
    bitmap.Erase(SkiaSharp.SKColors.OrangeRed);
    using var jpeg = bitmap.Encode(SkiaSharp.SKEncodedImageFormat.Jpeg, 90);
    using var input = new MemoryStream(jpeg.ToArray());
    var file = new FormFile(input, 0, input.Length, "Logo", "test.jpg");
    var normalized = await StoreLogoImages.NormalizeAsync(file, CancellationToken.None);
    using var decoded = SkiaSharp.SKBitmap.Decode(normalized);
    Check(decoded.Width == 512 && decoded.Height == 170, "JPEG normalisé en PNG borné sans modifier ses proportions");
    Check(normalized.Take(8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }), "stockage dans un seul format raster");
    return normalized;
}

static TradeLineInput Line(Product product, int quantity) => new() { ProductId = product.Id, Quantity = quantity };
static PaymentInput PaymentOf(decimal amount, string currency = "CDF", string method = "Espèces") => new() { Amount = amount, CurrencyCode = currency, Method = method };
static void Check(bool valid, string detail) { if (!valid) throw new InvalidOperationException(detail); }
static void Equal<T>(T expected, T actual, string detail) => Check(EqualityComparer<T>.Default.Equals(expected, actual), $"{detail} : attendu {expected}, obtenu {actual}");
static async Task Throws<T>(Func<Task> action) where T : Exception
{
    try { await action(); }
    catch (T) { return; }
    throw new InvalidOperationException($"Exception attendue {typeof(T).Name} non levée.");
}
