using AdGestionHub.Data;
using AdGestionHub.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AdGestionHub.Controllers
{
    [Authorize]
    public class ProductsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ProductsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // 1. LISTE DES PRODUITS
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            IQueryable<Product> productsQuery = _context.Products;

            if (!User.IsInRole("SuperAdmin"))
            {
                productsQuery = productsQuery.Where(p => p.BoutiqueId == user.BoutiqueId);
            }

            return View(await productsQuery.ToListAsync());
        }

        // 2. DÉTAILS
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            var product = await _context.Products
                .FirstOrDefaultAsync(m => m.Id == id && m.BoutiqueId == user.BoutiqueId);

            if (product == null) return NotFound();

            return View(product);
        }

        // 3. CRÉATION (GET)
        public IActionResult Create()
        {
            return View();
        }

        // 4. CRÉATION (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name,PurchasePrice,SalePrice,StockQuantity,LowStockThreshold,Variations")] Product product)
        {
            var user = await _userManager.GetUserAsync(User);

            if (ModelState.IsValid)
            {
                product.BoutiqueId = user.BoutiqueId;
                _context.Add(product);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(product);
        }

        // 5. MODIFICATION (GET)
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == id && p.BoutiqueId == user.BoutiqueId);

            if (product == null) return NotFound();

            return View(product);
        }

        // 6. MODIFICATION (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,PurchasePrice,SalePrice,StockQuantity,LowStockThreshold,Variations")] Product product)
        {
            if (id != product.Id) return NotFound();

            var user = await _userManager.GetUserAsync(User);

            var existingProduct = await _context.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id && p.BoutiqueId == user.BoutiqueId);

            if (existingProduct == null) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    product.BoutiqueId = user.BoutiqueId;
                    _context.Update(product);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ProductExists(product.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(product);
        }

        // --- SECTION SUPPRESSION CORRIGÉE ---

        // 7. AFFICHE LA PAGE DE CONFIRMATION (GET)
        [HttpGet]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            var product = await _context.Products
                .FirstOrDefaultAsync(m => m.Id == id && m.BoutiqueId == user.BoutiqueId);

            if (product == null) return NotFound();

            return View(product);
        }

        // 8. EXÉCUTE LA SUPPRESSION (POST)
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == id && p.BoutiqueId == user.BoutiqueId);

            if (product != null)
            {
                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        private bool ProductExists(int id)
        {
            return _context.Products.Any(e => e.Id == id);
        }
    }
}