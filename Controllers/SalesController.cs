using AdGestionHub.Data;
using AdGestionHub.Models;
using AdGestionHub.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
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
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IStockService _stockService;

        public SalesController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IStockService stockService)
        {
            _context = context;
            _userManager = userManager;
            _stockService = stockService;
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
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Sale sale)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.BoutiqueId == null)
                return Challenge();

            if (sale.Items == null || !sale.Items.Any())
            {
                ModelState.AddModelError("", "Le panier ne peut pas être vide.");
                var products = await _context.Products.Where(p => p.BoutiqueId == user.BoutiqueId).ToListAsync();
                ViewBag.ProductId = new SelectList(products, "Id", "Name");
                return View(sale);
            }

            var itemsToProcess = sale.Items.ToList();
            sale.Items = new List<SaleItem>();

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // 1. Vérification des stocks
                    foreach (var item in itemsToProcess.Where(i => i.ProductId.HasValue))
                    {
                        var product = await _context.Products
                            .FirstOrDefaultAsync(p => p.Id == item.ProductId && p.BoutiqueId == user.BoutiqueId);

                        if (product == null)
                        {
                            ModelState.AddModelError("", $"Produit '{item.ProductName}' introuvable dans votre boutique.");
                            var products = await _context.Products.Where(p => p.BoutiqueId == user.BoutiqueId).ToListAsync();
                            ViewBag.ProductId = new SelectList(products, "Id", "Name");
                            return View(sale);
                        }

                        if (!await _stockService.CheckStockAvailabilityAsync(product.Id, item.Quantity))
                        {
                            ModelState.AddModelError("", $"Stock insuffisant pour le produit '{product.Name}'. Disponible : {product.StockQuantity}, demandé : {item.Quantity}");
                            var products = await _context.Products.Where(p => p.BoutiqueId == user.BoutiqueId).ToListAsync();
                            ViewBag.ProductId = new SelectList(products, "Id", "Name");
                            return View(sale);
                        }
                    }

                    // 2. Création de la vente
                    sale.SaleDate = DateTime.Now;
                    sale.BoutiqueId = user.BoutiqueId.Value;

                    var culture = CultureInfo.InvariantCulture;
                    sale.FinalPrice = itemsToProcess.Sum(i =>
                    {
                        decimal unitPrice = Convert.ToDecimal(i.UnitPrice, culture);
                        return unitPrice * i.Quantity;
                    });

                    _context.Sales.Add(sale);
                    await _context.SaveChangesAsync();

                    // 3. Ajout des articles et déduction des stocks
                    foreach (var item in itemsToProcess)
                    {
                        var newItem = new SaleItem
                        {
                            SaleId = sale.Id,
                            ProductName = item.ProductName,
                            UnitPrice = Convert.ToDecimal(item.UnitPrice, CultureInfo.InvariantCulture),
                            Quantity = item.Quantity,
                            ProductId = item.ProductId <= 0 ? null : item.ProductId,
                            BoutiqueId = user.BoutiqueId.Value
                        };

                        if (newItem.ProductId != null)
                        {
                            await _stockService.DeductStockAsync(newItem.ProductId.Value, newItem.Quantity);
                        }

                        _context.SaleItems.Add(newItem);
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    TempData["SuccessMessage"] = "Vente enregistrée avec succès !";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    ModelState.AddModelError("", "Erreur base de données : " + (ex.InnerException?.Message ?? ex.Message));
                }
            }

            var productsList = await _context.Products.Where(p => p.BoutiqueId == user.BoutiqueId).ToListAsync();
            ViewBag.ProductId = new SelectList(productsList, "Id", "Name");
            return View(sale);
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