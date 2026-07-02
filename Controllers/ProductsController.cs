using AdGestionHub.Data;
using AdGestionHub.Models;
using AdGestionHub.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace AdGestionHub.Controllers
{
    [Authorize(Policy = "AdminOrSuperAdmin")]
    public class ProductsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IBoutiqueService _boutiqueService;

        public ProductsController(ApplicationDbContext context, IBoutiqueService boutiqueService)
        {
            _context = context;
            _boutiqueService = boutiqueService;
        }

        // GET: Products
        public async Task<IActionResult> Index()
        {
            var boutiqueId = await _boutiqueService.GetCurrentBoutiqueIdAsync();
            if (boutiqueId == null) return Unauthorized();

            var products = await _context.Products
                .Where(p => p.BoutiqueId == boutiqueId)
                .ToListAsync();

            return View(products);
        }

        // GET: Products/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var boutiqueId = await _boutiqueService.GetCurrentBoutiqueIdAsync();
            if (boutiqueId == null) return Unauthorized();

            var product = await _context.Products
                .Where(p => p.BoutiqueId == boutiqueId && p.Id == id)
                .FirstOrDefaultAsync();

            if (product == null) return NotFound();
            return View(product);
        }

        // GET: Products/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Products/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Nom,Description,PrixAchat,PrixVente,Quantite,SeuilAlerte")] Product product)
        {
            if (ModelState.IsValid)
            {
                var boutiqueId = await _boutiqueService.GetCurrentBoutiqueIdAsync();
                if (boutiqueId == null) return Unauthorized();

                product.BoutiqueId = boutiqueId.Value;
                _context.Add(product);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Produit ajouté avec succès.";
                return RedirectToAction(nameof(Index));
            }
            return View(product);
        }

        // GET: Products/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var boutiqueId = await _boutiqueService.GetCurrentBoutiqueIdAsync();
            if (boutiqueId == null) return Unauthorized();

            var product = await _context.Products
                .Where(p => p.BoutiqueId == boutiqueId && p.Id == id)
                .FirstOrDefaultAsync();

            if (product == null) return NotFound();
            return View(product);
        }

        // POST: Products/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Nom,Description,PrixAchat,PrixVente,Quantite,SeuilAlerte,BoutiqueId")] Product product)
        {
            if (id != product.Id) return NotFound();

            var boutiqueId = await _boutiqueService.GetCurrentBoutiqueIdAsync();
            if (boutiqueId == null) return Unauthorized();

            if (product.BoutiqueId != boutiqueId)
                return Forbid();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(product);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Produit mis à jour.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ProductExists(product.Id))
                        return NotFound();
                    else
                        throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(product);
        }

        // GET: Products/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var boutiqueId = await _boutiqueService.GetCurrentBoutiqueIdAsync();
            if (boutiqueId == null) return Unauthorized();

            var product = await _context.Products
                .Where(p => p.BoutiqueId == boutiqueId && p.Id == id)
                .FirstOrDefaultAsync();

            if (product == null) return NotFound();
            return View(product);
        }

        // POST: Products/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var boutiqueId = await _boutiqueService.GetCurrentBoutiqueIdAsync();
            if (boutiqueId == null) return Unauthorized();

            var product = await _context.Products
                .Where(p => p.BoutiqueId == boutiqueId && p.Id == id)
                .FirstOrDefaultAsync();

            if (product != null)
            {
                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Produit supprimé.";
            }
            return RedirectToAction(nameof(Index));
        }

        private bool ProductExists(int id)
        {
            return _context.Products.Any(e => e.Id == id);
        }
    }
}