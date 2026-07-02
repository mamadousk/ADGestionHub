using AdGestionHub.Data;
using AdGestionHub.Models;
using AdGestionHub.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Rotativa.AspNetCore;
using System;
using System.Collections.Generic;
using System.Globalization;
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
        private readonly ILogger<SalesController> _logger;

        public SalesController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IStockService stockService,
            ILogger<SalesController> logger)
        {
            _context = context;
            _userManager = userManager;
            _stockService = stockService;
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
                .Where(s => s.BoutiqueId == user.BoutiqueId)
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
                .FirstOrDefaultAsync(m => m.Id == id && m.BoutiqueId == user.BoutiqueId);

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
                .Where(p => p.BoutiqueId == user.BoutiqueId)
                .OrderBy(p => p.Name)
                .ToListAsync();

            ViewBag.ProductId = new SelectList(products, "Id", "Name");
            return View();
        }

        // ========== CRÉER UNE VENTE (POST) ==========
        // Le navigateur envoie seulement : produit, quantité (et un prix, pris en compte pour les Admin uniquement).
        // Prix catalogue, nom du produit, boutique, date et total sont décidés ici, côté serveur.
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

            var paymentMethod = string.IsNullOrWhiteSpace(model.PaymentMethod) ? "Cash" : model.PaymentMethod;
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

                    // Décrémentation atomique : échoue si le stock est insuffisant,
                    // y compris quand le même produit apparaît sur plusieurs lignes.
                    var deducted = await _stockService.TryDeductStockAsync(product.Id, boutiqueId, line.Quantity);
                    if (!deducted)
                    {
                        await transaction.RollbackAsync();
                        return await CreateViewWithErrorAsync(model, boutiqueId,
                            $"Stock insuffisant pour le produit '{product.Name}' (quantité demandée : {line.Quantity}).");
                    }

                    var unitPrice = product.SalePrice;
                    if (canOverridePrice && line.UnitPrice.HasValue && line.UnitPrice.Value > 0)
                        unitPrice = decimal.Round(line.UnitPrice.Value, 2);

                    sale.Items.Add(new SaleItem
                    {
                        ProductId = product.Id,
                        ProductName = product.Name,
                        UnitPrice = unitPrice,
                        Quantity = line.Quantity,
                        BoutiqueId = boutiqueId
                    });

                    total += unitPrice * line.Quantity;
                }

                sale.FinalPrice = total;

                _context.Sales.Add(sale);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["SuccessMessage"] = "Vente enregistrée avec succès !";
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

        // Réaffiche le formulaire avec la liste des produits (le panier est reconstruit côté navigateur).
        private async Task<IActionResult> CreateViewWithErrorAsync(SaleCreateViewModel model, int boutiqueId, string? error)
        {
            if (!string.IsNullOrEmpty(error))
                ModelState.AddModelError(string.Empty, error);

            var products = await _context.Products
                .AsNoTracking()
                .Where(p => p.BoutiqueId == boutiqueId)
                .OrderBy(p => p.Name)
                .ToListAsync();

            ViewBag.ProductId = new SelectList(products, "Id", "Name");

            return View(new Sale
            {
                CustomerName = model.CustomerName,
                PaymentMethod = model.PaymentMethod
            });
        }

        // ========== TÉLÉCHARGER LE REÇU ==========
        public async Task<IActionResult> DownloadReceipt(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.BoutiqueId == null)
                return Challenge();

            var sale = await _context.Sales
                .AsNoTracking()
                .Include(s => s.Items)
                .FirstOrDefaultAsync(m => m.Id == id && m.BoutiqueId == user.BoutiqueId);

            if (sale == null) return NotFound();

            var sequenceNumber = await _context.Sales
                .AsNoTracking()
                .Where(s => s.BoutiqueId == sale.BoutiqueId && s.Id <= sale.Id)
                .CountAsync();

            ViewBag.StoreInvoiceNumber = sequenceNumber;

            return new ViewAsPdf("Receipt", sale)
            {
                FileName = $"Recu_Vente_{sequenceNumber}.pdf",
                PageSize = Rotativa.AspNetCore.Options.Size.A4
            };
        }

        // ========== SUPPRIMER UNE VENTE ==========
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.BoutiqueId == null)
                return Challenge();

            var sale = await _context.Sales
                .Include(s => s.Items)
                .FirstOrDefaultAsync(m => m.Id == id && m.BoutiqueId == user.BoutiqueId);

            if (sale != null)
            {
                using (var transaction = await _context.Database.BeginTransactionAsync())
                {
                    try
                    {
                        foreach (var item in sale.Items)
                        {
                            if (item.ProductId != null)
                            {
                                await _stockService.RestoreStockAsync(item.ProductId.Value, item.Quantity);
                            }
                        }

                        _context.Sales.Remove(sale);
                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();

                        TempData["SuccessMessage"] = "La vente a été supprimée et les stocks restaurés.";
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        TempData["ErrorMessage"] = "Erreur lors de la suppression : " + ex.Message;
                    }
                }
            }

            return RedirectToAction(nameof(Index));
        }

        // ========== RECHERCHE DE PRODUITS POUR L'AUTOCOMPLÉTION (gardée car utile) ==========
        [HttpGet]
        public async Task<IActionResult> GetProducts(string term)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.BoutiqueId == null)
                return Json(new List<object>());

            var products = await _context.Products
                .AsNoTracking()
                .Where(p => p.BoutiqueId == user.BoutiqueId && p.Name.Contains(term))
                .Select(p => new { id = p.Id, label = p.Name, price = p.SalePrice })
                .Take(10)
                .ToListAsync();

            return Json(products);
        }
    }
}