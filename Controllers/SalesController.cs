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
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AdGestionHub.Controllers
{
    [Authorize]
    public class SalesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ITenantContextService _tenantContext;
        private readonly IStockService _stockService;
        private readonly ICacheService _cacheService;
        private readonly IPdfReportService _pdfReportService;
        private readonly IAppLocalizer _L;
        private readonly ILogger<SalesController> _logger;

        public SalesController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ITenantContextService tenantContext,
            IStockService stockService,
            ICacheService cacheService,
            IPdfReportService pdfReportService,
            IAppLocalizer localizer,
            ILogger<SalesController> logger)
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
            if (user == null || user.BoutiqueId == null)
                return Challenge();

            var sales = await _context.Sales
                .AsNoTracking()
                .Include(s => s.Items)
                .Where(s => s.BoutiqueId == user.BoutiqueId.Value)
                .OrderByDescending(s => s.SaleDate)
                .ToListAsync();

            return View(sales);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.BoutiqueId == null)
                return Challenge();

            var sale = await _context.Sales
                .AsNoTracking()
                .Include(s => s.Items)
                .FirstOrDefaultAsync(m => m.Id == id && m.BoutiqueId == user.BoutiqueId.Value);

            if (sale == null) return NotFound();
            return View(sale);
        }

        public async Task<IActionResult> Create()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.BoutiqueId == null)
                return Challenge();

            await LoadPosViewBagAsync(user.BoutiqueId.Value);
            return View(new Sale { PaymentMethod = "Cash" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SaleCreateViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.BoutiqueId == null)
                return Challenge();

            var boutiqueId = user.BoutiqueId.Value;
            var ctx = await _tenantContext.GetContextAsync();
            var items = model.Items ?? new List<SaleCreateItemViewModel>();

            if (!ModelState.IsValid)
                return await CreateViewWithErrorAsync(model, boutiqueId, null);

            if (items.Count == 0)
                return await CreateViewWithErrorAsync(model, boutiqueId, _L.T("Le panier ne peut pas être vide.", "Cart cannot be empty."));

            var paymentMethod = string.IsNullOrWhiteSpace(model.PaymentMethod) ? "Cash" : model.PaymentMethod.Trim();
            if (!CountryProfileCatalog.AllAllowedPaymentMethods.Contains(paymentMethod))
                return await CreateViewWithErrorAsync(model, boutiqueId, _L.T("Mode de paiement invalide.", "Invalid payment method."));

            var canOverridePrice = User.IsInRole("Admin");
            var decimals = ctx.PrimaryCurrency.Decimals;
            var docType = ctx.IsFormalized ? "Invoice" : "Receipt";

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var docNumber = await _tenantContext.AllocateNextDocumentNumberAsync(boutiqueId, docType);

                string? paidCurrency = ctx.PrimaryCurrency.Code;
                if (ctx.Settings.SecondaryCurrencyEnabled
                    && !string.IsNullOrWhiteSpace(model.PaidInCurrencyCode)
                    && (string.Equals(model.PaidInCurrencyCode, ctx.PrimaryCurrency.Code, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(model.PaidInCurrencyCode, ctx.SecondaryCurrency?.Code, StringComparison.OrdinalIgnoreCase)))
                {
                    paidCurrency = model.PaidInCurrencyCode.ToUpperInvariant();
                }

                var sale = new Sale
                {
                    SaleDate = DateTime.Now,
                    BoutiqueId = boutiqueId,
                    ClientId = model.ClientId,
                    CustomerName = string.IsNullOrWhiteSpace(model.CustomerName) ? null : model.CustomerName.Trim(),
                    CustomerTaxId = ctx.IsFormalized && !string.IsNullOrWhiteSpace(model.CustomerTaxId)
                        ? model.CustomerTaxId.Trim()
                        : null,
                    PaymentMethod = paymentMethod,
                    DocumentNumber = docNumber,
                    DocumentType = docType,
                    IsFormalizedSnapshot = ctx.IsFormalized,
                    IsVatEnabledSnapshot = ctx.IsVatEnabled,
                    CurrencyCode = ctx.PrimaryCurrency.Code,
                    CurrencySymbol = ctx.PrimaryCurrency.Symbol,
                    CurrencyDecimals = decimals,
                    SecondaryCurrencyCode = ctx.Settings.SecondaryCurrencyEnabled ? ctx.SecondaryCurrency?.Code : null,
                    ExchangeRateToSecondary = ctx.Settings.SecondaryCurrencyEnabled ? ctx.Settings.SecondaryExchangeRate : null,
                    PaidInCurrencyCode = paidCurrency,
                    StoreLegalTaxIdSnapshot = ctx.IsFormalized ? ctx.Settings.TaxIdentificationNumber : null,
                    StoreTaxIdLabelSnapshot = ctx.IsFormalized ? ctx.Country.TaxIdLabelFr : null,
                    StoreRccmSnapshot = ctx.IsFormalized ? ctx.Settings.TradeRegisterNumber : null,
                    StoreRccmLabelSnapshot = ctx.IsFormalized ? ctx.Country.TradeRegisterLabelFr : null
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
                        await transaction.RollbackAsync();
                        return await CreateViewWithErrorAsync(model, boutiqueId,
                            _L.T("Un produit du panier est introuvable dans votre boutique.", "One of the items was not found in your catalog."));
                    }

                    var deducted = await _stockService.TryDeductStockAsync(product.Id, boutiqueId, line.Quantity);
                    if (!deducted)
                    {
                        await transaction.RollbackAsync();
                        return await CreateViewWithErrorAsync(model, boutiqueId,
                            _L.T(
                                $"Stock insuffisant pour « {product.Name} » (demandé : {line.Quantity}, disponible : {product.StockQuantity}).",
                                $"Insufficient stock for « {product.Name} » (requested: {line.Quantity}, available: {product.StockQuantity})."));
                    }

                    var unitPrice = product.SalePrice;
                    if (canOverridePrice && line.UnitPrice.HasValue && line.UnitPrice.Value > 0)
                        unitPrice = decimal.Round(line.UnitPrice.Value, decimals, MidpointRounding.AwayFromZero);
                    else
                        unitPrice = decimal.Round(unitPrice, decimals, MidpointRounding.AwayFromZero);

                    // Moteur de calcul unique : Net HT + TVA (= 0 si désactivée) = Total
                    var vatRate = ctx.IsVatEnabled ? (product.VatRate ?? ctx.DefaultVatRate) : 0m;
                    var lineNet = decimal.Round(unitPrice * line.Quantity, decimals, MidpointRounding.AwayFromZero);
                    var lineTax = ctx.IsVatEnabled
                        ? decimal.Round(lineNet * vatRate / 100m, decimals, MidpointRounding.AwayFromZero)
                        : 0m;
                    var lineTotal = lineNet + lineTax;

                    sale.Items.Add(new SaleItem
                    {
                        ProductId = product.Id,
                        ProductName = product.Name,
                        UnitPrice = unitPrice,
                        UnitCost = product.PurchasePrice,
                        Quantity = line.Quantity,
                        VatRate = vatRate,
                        LineNet = lineNet,
                        LineTax = lineTax,
                        LineTotal = lineTotal,
                        BoutiqueId = boutiqueId
                    });

                    subtotalNet += lineNet;
                    totalTax += lineTax;
                }

                sale.SubtotalNet = subtotalNet;
                sale.TaxAmount = totalTax;
                sale.FinalPrice = subtotalNet + totalTax;

                _context.Sales.Add(sale);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                await _cacheService.RemoveAsync($"Dashboard_{boutiqueId}");

                var formattedTotal = ctx.FormatPrimary(sale.FinalPrice);
                TempData["SuccessMessage"] = _L.T(
                    $"{(ctx.IsFormalized ? "Facture" : "Vente")} {sale.DocumentNumber} ({formattedTotal}) enregistrée.",
                    $"{(ctx.IsFormalized ? "Invoice" : "Sale")} {sale.DocumentNumber} ({formattedTotal}) recorded.");

                return RedirectToAction(nameof(Receipt), new { id = sale.Id });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Échec de l'enregistrement d'une vente pour la boutique {BoutiqueId}", boutiqueId);
                return await CreateViewWithErrorAsync(model, boutiqueId,
                    _L.T("Une erreur est survenue, l'opération n'a pas été enregistrée.", "An error occurred; the transaction was rolled back."));
            }
        }

        private async Task LoadPosViewBagAsync(int boutiqueId)
        {
            var products = await _context.Products
                .AsNoTracking()
                .Where(p => p.BoutiqueId == boutiqueId && p.StockQuantity > 0)
                .OrderBy(p => p.Name)
                .ToListAsync();

            var clients = await _context.Clients
                .AsNoTracking()
                .Where(c => c.BoutiqueId == boutiqueId)
                .OrderBy(c => c.Name)
                .ToListAsync();

            ViewBag.ProductId = new SelectList(products, "Id", "Name");
            ViewBag.Clients = clients;
        }

        private async Task<IActionResult> CreateViewWithErrorAsync(SaleCreateViewModel model, int boutiqueId, string? error)
        {
            if (!string.IsNullOrEmpty(error))
                ModelState.AddModelError(string.Empty, error);

            await LoadPosViewBagAsync(boutiqueId);

            return View(new Sale
            {
                CustomerName = model.CustomerName,
                CustomerTaxId = model.CustomerTaxId,
                PaymentMethod = model.PaymentMethod ?? "Cash"
            });
        }

        // Modification directe d'une vente par le propriétaire autorisée en mode Non formalisé (avec trace d'audit)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> EditMetadata(SaleEditMetadataViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.BoutiqueId == null)
                return Challenge();

            var boutiqueId = user.BoutiqueId.Value;
            var sale = await _context.Sales
                .FirstOrDefaultAsync(s => s.Id == model.Id && s.BoutiqueId == boutiqueId);

            if (sale == null) return NotFound();

            if (sale.IsFormalizedSnapshot)
            {
                TempData["ErrorMessage"] = _L.T(
                    "Une facture émise en statut Formalisé ne peut pas être modifiée directement.",
                    "A formalized invoice cannot be directly modified after issuance.");
                return RedirectToAction(nameof(Details), new { id = sale.Id });
            }

            var prevCustomer = sale.CustomerName ?? "(comptoir)";
            var prevPayment = sale.PaymentMethod ?? "Cash";

            sale.CustomerName = string.IsNullOrWhiteSpace(model.CustomerName) ? null : model.CustomerName.Trim();
            if (!string.IsNullOrWhiteSpace(model.PaymentMethod) && CountryProfileCatalog.AllAllowedPaymentMethods.Contains(model.PaymentMethod.Trim()))
            {
                sale.PaymentMethod = model.PaymentMethod.Trim();
            }

            await _context.SaveChangesAsync();
            await _tenantContext.LogAuditAsync(
                boutiqueId,
                $"Modification de la vente {(!string.IsNullOrWhiteSpace(sale.DocumentNumber) ? sale.DocumentNumber : "#" + sale.Id)}",
                $"Client : {prevCustomer} -> {sale.CustomerName ?? "(comptoir)"} | Paiement : {prevPayment} -> {sale.PaymentMethod}");

            TempData["SuccessMessage"] = _L.T("Informations de la vente mises à jour (journalisé dans l'audit).", "Sale details updated (logged in audit history).");
            return RedirectToAction(nameof(Details), new { id = sale.Id });
        }

        public async Task<IActionResult> Receipt(int id, string? format = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.BoutiqueId == null)
                return Challenge();

            var boutiqueId = user.BoutiqueId.Value;
            var sale = await _context.Sales
                .AsNoTracking()
                .Include(s => s.Items)
                .FirstOrDefaultAsync(m => m.Id == id && m.BoutiqueId == boutiqueId);

            if (sale == null) return NotFound();

            var settings = await _context.StoreSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.BoutiqueId == boutiqueId);

            var sequenceNumber = await _context.Sales
                .AsNoTracking()
                .CountAsync(s => s.BoutiqueId == boutiqueId && s.Id <= sale.Id);

            ViewBag.StoreSettings = settings;
            ViewBag.StoreInvoiceNumber = sequenceNumber;
            ViewBag.PrintFormat = string.Equals(format, "thermal", StringComparison.OrdinalIgnoreCase) ? "thermal" : "a4";
            return View(sale);
        }

        public async Task<IActionResult> DownloadReceipt(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.BoutiqueId == null)
                return Challenge();

            var boutiqueId = user.BoutiqueId.Value;
            var sale = await _context.Sales
                .AsNoTracking()
                .Include(s => s.Items)
                .FirstOrDefaultAsync(m => m.Id == id && m.BoutiqueId == boutiqueId);

            if (sale == null) return NotFound();

            var settings = await _context.StoreSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.BoutiqueId == boutiqueId);

            var sequenceNumber = await _context.Sales
                .AsNoTracking()
                .CountAsync(s => s.BoutiqueId == boutiqueId && s.Id <= sale.Id);

            var pdfBytes = _pdfReportService.GenerateSaleReceiptPdf(sale, settings, sequenceNumber, _L.CurrentLanguage);
            var safeDoc = !string.IsNullOrWhiteSpace(sale.DocumentNumber) ? sale.DocumentNumber : $"{sequenceNumber:D5}";
            return File(pdfBytes, "application/pdf", $"{safeDoc}.pdf");
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.BoutiqueId == null)
                return Challenge();

            var sale = await _context.Sales
                .AsNoTracking()
                .Include(s => s.Items)
                .FirstOrDefaultAsync(m => m.Id == id && m.BoutiqueId == user.BoutiqueId.Value);

            if (sale == null) return NotFound();
            return View(sale);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(int id, string? reason)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.BoutiqueId == null)
                return Challenge();

            var boutiqueId = user.BoutiqueId.Value;
            var sale = await _context.Sales
                .Include(s => s.Items)
                .FirstOrDefaultAsync(m => m.Id == id && m.BoutiqueId == boutiqueId);

            if (sale != null && !sale.IsCancelled)
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    foreach (var item in sale.Items)
                    {
                        if (item.ProductId.HasValue)
                        {
                            await _stockService.RestoreStockAsync(item.ProductId.Value, boutiqueId, item.Quantity);
                        }
                    }

                    // Préservation de la numérotation séquentielle sans trou + trace d'audit complète
                    sale.IsCancelled = true;
                    sale.CancelledAt = DateTime.Now;
                    sale.CancellationReason = string.IsNullOrWhiteSpace(reason) ? "Annulation propriétaire" : reason.Trim();

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    var docRef = !string.IsNullOrWhiteSpace(sale.DocumentNumber) ? sale.DocumentNumber : $"#{sale.Id}";
                    await _tenantContext.LogAuditAsync(
                        boutiqueId,
                        $"Annulation du justificatif {docRef} (stocks restaurés)",
                        $"Montant : {sale.FinalPrice} {sale.CurrencyCode} | Motif : {sale.CancellationReason}");

                    await _cacheService.RemoveAsync($"Dashboard_{boutiqueId}");
                    TempData["SuccessMessage"] = _L.T(
                        $"L'opération {docRef} a été annulée, les stocks ont été restaurés et l'action a été journalisée.",
                        $"Transaction {docRef} was voided, stock restored, and recorded in audit logs.");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Erreur lors de l'annulation de la vente #{SaleId}", id);
                    TempData["ErrorMessage"] = _L.T("Une erreur est survenue lors de l'annulation.", "Failed to void transaction.");
                }
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> GetProducts(string? term)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.BoutiqueId == null)
                return Json(Array.Empty<object>());

            var ctx = await _tenantContext.GetContextAsync();

            var query = _context.Products
                .AsNoTracking()
                .Where(p => p.BoutiqueId == user.BoutiqueId.Value && p.StockQuantity > 0);

            if (!string.IsNullOrWhiteSpace(term))
            {
                var cleanTerm = term.Trim();
                query = query.Where(p => p.Name.Contains(cleanTerm) || (p.Sku != null && p.Sku.Contains(cleanTerm)));
            }

            var rawProducts = await query
                .OrderBy(p => p.Name)
                .Take(15)
                .ToListAsync();

            var result = rawProducts.Select(p => new
            {
                id = p.Id,
                name = p.Name,
                sku = p.Sku ?? "",
                label = $"{p.Name} ({_L.T("Stock", "Stock")} : {p.StockQuantity}) — {ctx.FormatPrimary(p.SalePrice)}",
                price = p.SalePrice,
                vatRate = ctx.IsVatEnabled ? (p.VatRate ?? ctx.DefaultVatRate) : 0m,
                stock = p.StockQuantity
            });

            return Json(result);
        }
    }
}
