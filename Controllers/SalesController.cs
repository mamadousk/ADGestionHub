using AdGestionHub.Data;
using AdGestionHub.Models;
using AdGestionHub.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AdGestionHub.Controllers
{
    [Authorize]
    public class SalesController : Controller
    {
        private static readonly string[] AllowedPaymentMethods =
            { "Cash", "M-Pesa", "Airtel Money", "Orange Money", "Carte Bancaire" };

        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IStockService _stockService;
        private readonly ICacheService _cacheService;
        private readonly IPdfReportService _pdfReportService;
        private readonly ILogger<SalesController> _logger;

        public SalesController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IStockService stockService,
            ICacheService cacheService,
            IPdfReportService pdfReportService,
            ILogger<SalesController> logger)
        {
            _context = context;
            _userManager = userManager;
            _stockService = stockService;
            _cacheService = cacheService;
            _pdfReportService = pdfReportService;
            _logger = logger;
        }

        // ========== LISTE DES VENTES ==========
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

        // ========== DÉTAILS D'UNE VENTE ==========
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

        // ========== CRÉER UNE VENTE (GET) ==========
        public async Task<IActionResult> Create()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.BoutiqueId == null)
                return Challenge();

            var products = await _context.Products
                .AsNoTracking()
                .Where(p => p.BoutiqueId == user.BoutiqueId.Value && p.StockQuantity > 0)
                .OrderBy(p => p.Name)
                .ToListAsync();

            ViewBag.ProductId = new SelectList(products, "Id", "Name");
            return View(new Sale { PaymentMethod = "Cash" });
        }

        // ========== CRÉER UNE VENTE (POST) ==========
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SaleCreateViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.BoutiqueId == null)
                return Challenge();

            var boutiqueId = user.BoutiqueId.Value;
            var items = model.Items ?? new List<SaleCreateItemViewModel>();

            if (!ModelState.IsValid)
                return await CreateViewWithErrorAsync(model, boutiqueId, null);

            if (items.Count == 0)
                return await CreateViewWithErrorAsync(model, boutiqueId, "Le panier ne peut pas être vide.");

            var paymentMethod = string.IsNullOrWhiteSpace(model.PaymentMethod) ? "Cash" : model.PaymentMethod.Trim();
            if (!AllowedPaymentMethods.Contains(paymentMethod))
                return await CreateViewWithErrorAsync(model, boutiqueId, "Moyen de paiement invalide.");

            // Seul un Admin peut vendre à un prix différent du prix catalogue (remise, négociation).
            var canOverridePrice = User.IsInRole("Admin");

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var sale = new Sale
                {
                    SaleDate = DateTime.Now,
                    BoutiqueId = boutiqueId,
                    CustomerName = string.IsNullOrWhiteSpace(model.CustomerName) ? null : model.CustomerName.Trim(),
                    PaymentMethod = paymentMethod
                };

                decimal total = 0;

                foreach (var line in items)
                {
                    var product = await _context.Products
                        .AsNoTracking()
                        .FirstOrDefaultAsync(p => p.Id == line.ProductId && p.BoutiqueId == boutiqueId);

                    if (product == null)
                    {
                        await transaction.RollbackAsync();
                        return await CreateViewWithErrorAsync(model, boutiqueId,
                            "Un des produits du panier est introuvable dans votre boutique.");
                    }

                    var deducted = await _stockService.TryDeductStockAsync(product.Id, boutiqueId, line.Quantity);
                    if (!deducted)
                    {
                        await transaction.RollbackAsync();
                        return await CreateViewWithErrorAsync(model, boutiqueId,
                            $"Stock insuffisant pour « {product.Name} » (demandé : {line.Quantity}, disponible : {product.StockQuantity}).");
                    }

                    var unitPrice = product.SalePrice;
                    if (canOverridePrice && line.UnitPrice.HasValue && line.UnitPrice.Value > 0)
                        unitPrice = decimal.Round(line.UnitPrice.Value, 2);

                    sale.Items.Add(new SaleItem
                    {
                        ProductId = product.Id,
                        ProductName = product.Name,
                        UnitPrice = unitPrice,
                        UnitCost = product.PurchasePrice,
                        Quantity = line.Quantity,
                        BoutiqueId = boutiqueId
                    });

                    total += unitPrice * line.Quantity;
                }

                sale.FinalPrice = total;

                _context.Sales.Add(sale);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                await _cacheService.RemoveAsync($"Dashboard_{boutiqueId}");

                TempData["SuccessMessage"] = $"Vente de {total:N2} $ enregistrée avec succès !";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Échec de l'enregistrement d'une vente pour la boutique {BoutiqueId}", boutiqueId);
                return await CreateViewWithErrorAsync(model, boutiqueId,
                    "Une erreur est survenue, la vente n'a pas été enregistrée. Veuillez réessayer.");
            }
        }

        private async Task<IActionResult> CreateViewWithErrorAsync(SaleCreateViewModel model, int boutiqueId, string? error)
        {
            if (!string.IsNullOrEmpty(error))
                ModelState.AddModelError(string.Empty, error);

            var products = await _context.Products
                .AsNoTracking()
                .Where(p => p.BoutiqueId == boutiqueId && p.StockQuantity > 0)
                .OrderBy(p => p.Name)
                .ToListAsync();

            ViewBag.ProductId = new SelectList(products, "Id", "Name");

            return View(new Sale
            {
                CustomerName = model.CustomerName,
                PaymentMethod = model.PaymentMethod ?? "Cash"
            });
        }

        // ========== VUE IMPRIMABLE DU REÇU ==========
        public async Task<IActionResult> Receipt(int id)
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
            return View(sale);
        }

        // ========== TÉLÉCHARGER LE REÇU PDF (QuestPDF, multiplateforme) ==========
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

            var pdfBytes = _pdfReportService.GenerateSaleReceiptPdf(sale, settings, sequenceNumber);
            return File(pdfBytes, "application/pdf", $"Recu_Vente_{sequenceNumber:D5}.pdf");
        }

        // ========== SUPPRIMER UNE VENTE (GET de confirmation, Admin uniquement) ==========
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

        // ========== SUPPRIMER UNE VENTE (POST, Admin uniquement) ==========
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.BoutiqueId == null)
                return Challenge();

            var boutiqueId = user.BoutiqueId.Value;
            var sale = await _context.Sales
                .Include(s => s.Items)
                .FirstOrDefaultAsync(m => m.Id == id && m.BoutiqueId == boutiqueId);

            if (sale != null)
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

                    _context.Sales.Remove(sale);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    await _cacheService.RemoveAsync($"Dashboard_{boutiqueId}");
                    TempData["SuccessMessage"] = "La vente a été annulée et les stocks ont été restaurés.";
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Erreur lors de la suppression de la vente #{SaleId}", id);
                    TempData["ErrorMessage"] = "Une erreur est survenue lors de l'annulation de la vente.";
                }
            }

            return RedirectToAction(nameof(Index));
        }

        // ========== RECHERCHE DE PRODUITS POUR L'AUTOCOMPLÉTION ==========
        [HttpGet]
        public async Task<IActionResult> GetProducts(string? term)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.BoutiqueId == null)
                return Json(Array.Empty<object>());

            var query = _context.Products
                .AsNoTracking()
                .Where(p => p.BoutiqueId == user.BoutiqueId.Value && p.StockQuantity > 0);

            if (!string.IsNullOrWhiteSpace(term))
            {
                var cleanTerm = term.Trim();
                query = query.Where(p => p.Name.Contains(cleanTerm));
            }

            var products = await query
                .OrderBy(p => p.Name)
                .Take(12)
                .Select(p => new
                {
                    id = p.Id,
                    name = p.Name,
                    label = $"{p.Name} (Stock : {p.StockQuantity}) — {p.SalePrice:N2} $",
                    price = p.SalePrice,
                    stock = p.StockQuantity
                })
                .ToListAsync();

            return Json(products);
        }
    }
}
