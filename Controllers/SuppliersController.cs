using AdGestionHub.Data;
using AdGestionHub.Models;
using AdGestionHub.Models.ViewModels;
using AdGestionHub.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdGestionHub.Controllers;

[Authorize(Roles = "Admin")]
public sealed class SuppliersController(ApplicationDbContext db, IBoutiqueService boutiques, CommerceWriter writer) : Controller
{
    public async Task<IActionResult> Index(string? search)
    {
        if (await boutiques.GetCurrentBoutiqueIdAsync() is not int shop) return Forbid();
        var query = db.Suppliers.AsNoTracking().Where(c => c.BoutiqueId == shop);
        if (CommerceOperations.Clean(search) is string term)
            query = query.Where(c => c.Name.Contains(term) || (c.Phone != null && c.Phone.Contains(term)));
        ViewData["Search"] = search;
        return View(await query.OrderBy(c => c.Name).ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        if (await boutiques.GetCurrentBoutiqueIdAsync() is not int shop) return Forbid();
        var supplier = await db.Suppliers.AsNoTracking().SingleOrDefaultAsync(c => c.BoutiqueId == shop && c.Id == id);
        if (supplier == null) return NotFound();
        var purchases = await db.Purchases.AsNoTracking().Where(p => p.BoutiqueId == shop && p.SupplierId == id).Include(p => p.Payments)
            .OrderByDescending(p => p.RecordedAt).ToListAsync();
        return View(new BusinessHistory<Supplier>(supplier, purchases.Select(p =>
            new DocumentBalance(p.Id, p.Number, p.RecordedAt, p.SupplierName, p.CurrencyCode, p.Total, p.Paid, "Purchase")).ToList()));
    }

    [HttpGet]
    public IActionResult Create() => View("Form", new ContactInput());

    [HttpPost]
    public Task<IActionResult> Create(ContactInput model) => SaveAsync(null, model);

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        if (await boutiques.GetCurrentBoutiqueIdAsync() is not int shop) return Forbid();
        var contact = await db.Suppliers.AsNoTracking().SingleOrDefaultAsync(c => c.BoutiqueId == shop && c.Id == id);
        if (contact == null) return NotFound();
        ViewData["ContactId"] = id;
        return View("Form", new ContactInput { Name = contact.Name, Phone = contact.Phone, Address = contact.Address, Email = contact.Email });
    }

    [HttpPost]
    public Task<IActionResult> Edit(int id, ContactInput model) => SaveAsync(id, model);

    private async Task<IActionResult> SaveAsync(int? id, ContactInput model)
    {
        if (await boutiques.GetCurrentBoutiqueIdAsync() is not int shop) return Forbid();
        if (ModelState.IsValid)
        {
            try
            {
                var supplier = await writer.ExecuteAsync(shop, async () =>
                {
                    var contact = id.HasValue
                        ? await db.Suppliers.SingleOrDefaultAsync(c => c.BoutiqueId == shop && c.Id == id) ?? throw new CommerceNotFoundException()
                        : new Supplier { BoutiqueId = shop };
                    contact.Name = model.Name.Trim();
                    contact.Phone = CommerceOperations.Clean(model.Phone);
                    contact.Address = CommerceOperations.Clean(model.Address);
                    contact.Email = CommerceOperations.Clean(model.Email);
                    if (!id.HasValue) db.Suppliers.Add(contact);
                    writer.Audit(shop, id.HasValue ? $"Fiche fournisseur #{id} modifiée" : "Fiche fournisseur créée");
                    return contact;
                });
                TempData["SuccessMessage"] = "Fiche fournisseur enregistrée.";
                return RedirectToAction(nameof(Details), new { id = supplier.Id });
            }
            catch (CommerceNotFoundException) { return NotFound(); }
        }
        ViewData["ContactId"] = id;
        return View("Form", model);
    }
}
