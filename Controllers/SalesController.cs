using AdGestionHub.Data;
using AdGestionHub.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Rotativa.AspNetCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AdGestionHub.Controllers
{
    [Authorize]
    public class SalesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public SalesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var sales = await _context.Sales
                .Include(s => s.Items)
                .Where(s => s.BoutiqueId == user.BoutiqueId)
                .OrderByDescending(s => s.SaleDate)
                .ToListAsync();
            return View(sales);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            var sale = await _context.Sales
                .Include(s => s.Items)
                .FirstOrDefaultAsync(m => m.Id == id && m.BoutiqueId == user.BoutiqueId);

            if (sale == null) return NotFound();
            return View(sale);
        }

        public async Task<IActionResult> Create()
        {
            var user = await _userManager.GetUserAsync(User);
            var products = await _context.Products
                .Where(p => p.BoutiqueId == user.BoutiqueId)
                .OrderBy(p => p.Name)
                .ToListAsync();

            ViewBag.ProductId = new SelectList(products, "Id", "Name");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Sale sale)
        {
            var user = await _userManager.GetUserAsync(User);

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
                    sale.SaleDate = DateTime.Now;
                    sale.BoutiqueId = user.BoutiqueId ?? 0;
                    
                    // CORRECTION : Recalcul strict du prix final (Total = Σ Prix * Qté)
                    sale.FinalPrice = itemsToProcess.Sum(i => i.UnitPrice * i.Quantity);

                    _context.Sales.Add(sale);
                    await _context.SaveChangesAsync();

                    foreach (var item in itemsToProcess)
                    {
                        var newItem = new SaleItem
                        {
                            SaleId = sale.Id,
                            ProductName = item.ProductName,
                            UnitPrice = item.UnitPrice,
                            Quantity = item.Quantity,
                            ProductId = item.ProductId <= 0 ? null : item.ProductId
                        };

                        if (newItem.ProductId != null)
                        {
                            var productInStock = await _context.Products
                                .FirstOrDefaultAsync(p => p.Id == newItem.ProductId && p.BoutiqueId == user.BoutiqueId);

                            if (productInStock != null)
                            {
                                productInStock.StockQuantity -= newItem.Quantity;
                                _context.Update(productInStock);
                            }
                        }
                        _context.SaleItems.Add(newItem);
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    ModelState.AddModelError("", "Erreur base de données : " + (ex.InnerException?.Message ?? ex.Message));
                }
            }

            var prods = await _context.Products.Where(p => p.BoutiqueId == user.BoutiqueId).ToListAsync();
            ViewBag.ProductId = new SelectList(prods, "Id", "Name");
            return View(sale);
        }

        public async Task<IActionResult> DownloadReceipt(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var sale = await _context.Sales
                .Include(s => s.Items)
                .FirstOrDefaultAsync(m => m.Id == id && m.BoutiqueId == user.BoutiqueId);

            if (sale == null) return NotFound();

            // Calcul du numéro de reçu propre à la boutique (ex: 1, 2, 3...)
            var sequenceNumber = await _context.Sales
                .Where(s => s.BoutiqueId == sale.BoutiqueId && s.Id <= sale.Id)
            .CountAsync();

            // On stocke le numéro dans le ViewBag pour la vue HTML
            ViewBag.StoreInvoiceNumber = sequenceNumber;

            return new ViewAsPdf("Receipt", sale)
            {
                FileName = $"Recu_Vente_{sequenceNumber}.pdf",
                PageSize = Rotativa.AspNetCore.Options.Size.A4
            };
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var sale = await _context.Sales
                .Include(s => s.Items)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (sale != null)
            {
                // 1. Remettre les articles dans le stock avant de supprimer
                foreach (var item in sale.Items)
                {
                    var product = await _context.Products.FindAsync(item.ProductId);
                    if (product != null)
                    {
                        product.StockQuantity += item.Quantity;
                        _context.Update(product);
                    }
                }

                // 2. Supprimer la vente
                _context.Sales.Remove(sale);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

    }
}