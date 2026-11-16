using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AdGestionHub.Data;
using AdGestionHub.Models;
using AdGestionHub.Models.ViewModels;
using AdGestionHub.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AdGestionHub.Controllers
{
    [Authorize]
    public class QuotesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ITenantContextService _tenantContext;
        private readonly IStockService _stockService;
        private readonly ICacheService _cacheService;
        private readonly IPdfReportService _pdfReportService;
        private readonly IAppLocalizer _L;
        private readonly ILogger<QuotesController> _logger;

        public QuotesController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ITenantContextService tenantContext,
            IStockService stockService,
            ICacheService cacheService,
            IPdfReportService pdfReportService,
            IAppLocalizer localizer,
            ILogger<QuotesController> logger)
        {
            _context = context;
            _userManager = userManager;
            _tenantContext = tenantContext;
            _stockService = stockService;
            _cacheService = cacheService;
            _pdfReportService = pdfReportService;
            _L = localizer;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.BoutiqueId == null) return Challenge();

            var quotes = await _context.Quotes
                .AsNoTracking()
                .Include(q => q.Items)
                .Where(q => q.BoutiqueId == user.BoutiqueId.Value)
                .OrderByDescending(q => q.CreatedAt)
                .ToListAsync();

            return View(quotes);
        }

        public async Task<IActionResult> Create()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.BoutiqueId == null) return Challenge();

            var ctx = await _tenantContext.GetContextAsync();
            if (!ctx.IsFormalized)
            {
                TempData["ErrorMessage"] = _L.T(
                    "Les Devis & Proformas sont disponibles lorsque le statut Formalisé est activé dans Paramètres > Fiscalité.",
                    "Quotes & Proformas are unlocked when Formalized status is enabled in Settings > Tax & Documents.");
                return RedirectToAction("Index", "Sales");
            }

            ViewBag.Clients = await _context.Clients
                .AsNoTracking()
                .Where(c => c.BoutiqueId == user.BoutiqueId.Value)
                .OrderBy(c => c.Name)
                .ToListAsync();

            return View(new QuoteCreateViewModel
            {
                ValidUntil = DateTime.Today.AddDays(15)
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(QuoteCreateViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.BoutiqueId == null) return Challenge();

            var boutiqueId = user.BoutiqueId.Value;
            var ctx = await _tenantContext.GetContextAsync();
            if (!ctx.IsFormalized)
                return RedirectToAction("Index", "Sales");

            var items = model.Items ?? new List<SaleCreateItemViewModel>();
            if (items.Count == 0)
                ModelState.AddModelError(string.Empty, _L.T("Ajoutez au moins un article au devis.", "Add at least one item to the quote."));

            if (!ModelState.IsValid)
            {
                ViewBag.Clients = await _context.Clients
                    .AsNoTracking()
                    .Where(c => c.BoutiqueId == boutiqueId)
                    .OrderBy(c => c.Name)
                    .ToListAsync();
                return View(model);
            }

            var decimals = ctx.PrimaryCurrency.Decimals;
            var canOverridePrice = User.IsInRole("Admin");

            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var docNumber = await _tenantContext.AllocateNextDocumentNumberAsync(boutiqueId, "Quote");

                var quote = new Quote
                {
                    BoutiqueId = boutiqueId,
                    DocumentNumber = docNumber,
                    CreatedAt = DateTime.Now,
                    ValidUntil = model.ValidUntil,
                    ClientId = model.ClientId,
                    CustomerName = model.CustomerName.Trim(),
                    CustomerPhone = string.IsNullOrWhiteSpace(model.CustomerPhone) ? null : model.CustomerPhone.Trim(),
                    CustomerTaxId = string.IsNullOrWhiteSpace(model.CustomerTaxId) ? null : model.CustomerTaxId.Trim(),
                    Status = "Pending",
                    IsVatEnabledSnapshot = ctx.IsVatEnabled,
                    CurrencyCode = ctx.PrimaryCurrency.Code,
                    CurrencySymbol = ctx.PrimaryCurrency.Symbol,
                    CurrencyDecimals = decimals,
                    SecondaryCurrencyCode = ctx.SecondaryCurrency?.Code,
                    ExchangeRateToSecondary = ctx.Settings.SecondaryCurrencyEnabled ? ctx.Settings.SecondaryExchangeRate : null,
                    StoreLegalTaxIdSnapshot = ctx.Settings.TaxIdentificationNumber,
                    StoreTaxIdLabelSnapshot = ctx.Country.TaxIdLabelFr,
                    StoreRccmSnapshot = ctx.Settings.TradeRegisterNumber,
                    StoreRccmLabelSnapshot = ctx.Country.TradeRegisterLabelFr,
                    Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim()
                };

                decimal subtotalNet = 0m;
                decimal totalTax = 0m;

                foreach (var line in items)
                {
                    var product = await _context.Products
                        .AsNoTracking()
                        .FirstOrDefaultAsync(p => p.Id == line.ProductId && p.BoutiqueId == boutiqueId);

                    if (product == null)
                    {
                        await tx.RollbackAsync();
                        ModelState.AddModelError(string.Empty, _L.T("Produit introuvable.", "Product not found."));
                        return View(model);
                    }

                    var unitPrice = product.SalePrice;
                    if (canOverridePrice && line.UnitPrice.HasValue && line.UnitPrice.Value > 0)
                        unitPrice = decimal.Round(line.UnitPrice.Value, decimals);

                    var vatRate = ctx.IsVatEnabled ? (product.VatRate ?? ctx.DefaultVatRate) : 0m;
                    var lineNet = decimal.Round(unitPrice * line.Quantity, decimals, MidpointRounding.AwayFromZero);
                    var lineTax = ctx.IsVatEnabled
                        ? decimal.Round(lineNet * vatRate / 100m, decimals, MidpointRounding.AwayFromZero)
                        : 0m;
                    var lineTotal = lineNet + lineTax;

                    quote.Items.Add(new QuoteItem
                    {
                        BoutiqueId = boutiqueId,
                        ProductId = product.Id,
                        ProductName = product.Name,
                        Quantity = line.Quantity,
                        UnitPrice = unitPrice,
                        VatRate = vatRate,
                        LineNet = lineNet,
                        LineTax = lineTax,
                        LineTotal = lineTotal
                    });

                    subtotalNet += lineNet;
                    totalTax += lineTax;
                }

                quote.SubtotalNet = subtotalNet;
                quote.TaxAmount = totalTax;
                quote.TotalAmount = subtotalNet + totalTax;

                _context.Quotes.Add(quote);
                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                TempData["SuccessMessage"] = _L.T(
                    $"Devis {quote.DocumentNumber} créé avec succès.",
                    $"Quote {quote.DocumentNumber} created successfully.");

                return RedirectToAction(nameof(Details), new { id = quote.Id });
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "Erreur lors de la création du devis pour la boutique {BoutiqueId}", boutiqueId);
                ModelState.AddModelError(string.Empty, _L.T("Erreur lors de l'enregistrement du devis.", "Failed to save quote."));
                return View(model);
            }
        }

        public async Task<IActionResult> Details(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.BoutiqueId == null) return Challenge();

            var quote = await _context.Quotes
                .AsNoTracking()
                .Include(q => q.Items)
                .FirstOrDefaultAsync(q => q.Id == id && q.BoutiqueId == user.BoutiqueId.Value);

            if (quote == null) return NotFound();

            var settings = await _context.StoreSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.BoutiqueId == user.BoutiqueId.Value);

            ViewBag.StoreSettings = settings;
            return View(quote);
        }

        public async Task<IActionResult> DownloadPdf(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.BoutiqueId == null) return Challenge();

            var quote = await _context.Quotes
                .AsNoTracking()
                .Include(q => q.Items)
                .FirstOrDefaultAsync(q => q.Id == id && q.BoutiqueId == user.BoutiqueId.Value);

            if (quote == null) return NotFound();

            var settings = await _context.StoreSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.BoutiqueId == user.BoutiqueId.Value);

            var pdfBytes = _pdfReportService.GenerateQuotePdf(quote, settings, _L.CurrentLanguage);
            return File(pdfBytes, "application/pdf", $"{quote.DocumentNumber}.pdf");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConvertToSale(int id, string paymentMethod = "Cash")
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.BoutiqueId == null) return Challenge();

            var boutiqueId = user.BoutiqueId.Value;
            var quote = await _context.Quotes
                .Include(q => q.Items)
                .FirstOrDefaultAsync(q => q.Id == id && q.BoutiqueId == boutiqueId);

            if (quote == null) return NotFound();
            if (string.Equals(quote.Status, "Converted", StringComparison.OrdinalIgnoreCase) && quote.ConvertedSaleId.HasValue)
                return RedirectToAction("Receipt", "Sales", new { id = quote.ConvertedSaleId.Value });

            var cleanPayment = CountryProfileCatalog.AllAllowedPaymentMethods.Contains(paymentMethod) ? paymentMethod : "Cash";

            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var docNum = await _tenantContext.AllocateNextDocumentNumberAsync(boutiqueId, "Invoice");

                var sale = new Sale
                {
                    BoutiqueId = boutiqueId,
                    SaleDate = DateTime.Now,
                    ClientId = quote.ClientId,
                    CustomerName = quote.CustomerName,
                    CustomerTaxId = quote.CustomerTaxId,
                    PaymentMethod = cleanPayment,
                    DocumentNumber = docNum,
                    DocumentType = "Invoice",
                    IsFormalizedSnapshot = true,
                    IsVatEnabledSnapshot = quote.IsVatEnabledSnapshot,
                    CurrencyCode = quote.CurrencyCode,
                    CurrencySymbol = quote.CurrencySymbol,
                    CurrencyDecimals = quote.CurrencyDecimals,
                    SecondaryCurrencyCode = quote.SecondaryCurrencyCode,
                    ExchangeRateToSecondary = quote.ExchangeRateToSecondary,
                    StoreLegalTaxIdSnapshot = quote.StoreLegalTaxIdSnapshot,
                    StoreTaxIdLabelSnapshot = quote.StoreTaxIdLabelSnapshot,
                    StoreRccmSnapshot = quote.StoreRccmSnapshot,
                    StoreRccmLabelSnapshot = quote.StoreRccmLabelSnapshot,
                    SubtotalNet = quote.SubtotalNet,
                    TaxAmount = quote.TaxAmount,
                    FinalPrice = quote.TotalAmount
                };

                foreach (var qItem in quote.Items)
                {
                    decimal unitCost = 0m;
                    if (qItem.ProductId.HasValue)
                    {
                        var prod = await _context.Products.AsNoTracking()
                            .FirstOrDefaultAsync(p => p.Id == qItem.ProductId.Value && p.BoutiqueId == boutiqueId);
                        if (prod == null)
                        {
                            await tx.RollbackAsync();
                            TempData["ErrorMessage"] = _L.T($"Produit « {qItem.ProductName} » introuvable en stock.", $"Product « {qItem.ProductName} » not found.");
                            return RedirectToAction(nameof(Details), new { id });
                        }

                        var deducted = await _stockService.TryDeductStockAsync(prod.Id, boutiqueId, qItem.Quantity);
                        if (!deducted)
                        {
                            await tx.RollbackAsync();
                            TempData["ErrorMessage"] = _L.T(
                                $"Stock insuffisant pour « {prod.Name} » (disponible : {prod.StockQuantity}).",
                                $"Insufficient stock for « {prod.Name} » (available: {prod.StockQuantity}).");
                            return RedirectToAction(nameof(Details), new { id });
                        }

                        unitCost = prod.PurchasePrice;
                    }

                    sale.Items.Add(new SaleItem
                    {
                        BoutiqueId = boutiqueId,
                        ProductId = qItem.ProductId,
                        ProductName = qItem.ProductName,
                        Quantity = qItem.Quantity,
                        UnitPrice = qItem.UnitPrice,
                        UnitCost = unitCost,
                        VatRate = qItem.VatRate,
                        LineNet = qItem.LineNet,
                        LineTax = qItem.LineTax,
                        LineTotal = qItem.LineTotal
                    });
                }

                _context.Sales.Add(sale);
                await _context.SaveChangesAsync();

                quote.Status = "Converted";
                quote.ConvertedSaleId = sale.Id;
                await _context.SaveChangesAsync();

                await tx.CommitAsync();
                await _cacheService.RemoveAsync($"Dashboard_{boutiqueId}");
                await _tenantContext.LogAuditAsync(boutiqueId, $"Conversion du devis {quote.DocumentNumber} en facture {sale.DocumentNumber}");

                TempData["SuccessMessage"] = _L.T(
                    $"Devis {quote.DocumentNumber} converti en facture {sale.DocumentNumber}.",
                    $"Quote {quote.DocumentNumber} converted into invoice {sale.DocumentNumber}.");

                return RedirectToAction("Receipt", "Sales", new { id = sale.Id });
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "Erreur lors de la conversion du devis #{QuoteId}", id);
                TempData["ErrorMessage"] = _L.T("Impossible de convertir ce devis.", "Could not convert quote.");
                return RedirectToAction(nameof(Details), new { id });
            }
        }
    }
}
