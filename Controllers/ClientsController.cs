using AdGestionHub.Data;
using AdGestionHub.Models;
using AdGestionHub.Models.ViewModels;
using AdGestionHub.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdGestionHub.Controllers;

[Authorize(Policy = "ShopMember")]
public sealed class ClientsController(ApplicationDbContext db, IBoutiqueService boutiques, CommerceWriter writer) : Controller
{
    public async Task<IActionResult> Index(string? search)
    {
        if (await boutiques.GetCurrentBoutiqueIdAsync() is not int shop) return Forbid();
        var query = db.Clients.AsNoTracking().Where(c => c.BoutiqueId == shop);
        if (CommerceOperations.Clean(search) is string term)
            query = query.Where(c => c.Name.Contains(term) || (c.Phone != null && c.Phone.Contains(term)));
        ViewData["Search"] = search;
        return View(await query.OrderBy(c => c.Name).ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        if (await boutiques.GetCurrentBoutiqueIdAsync() is not int shop) return Forbid();
        var client = await db.Clients.AsNoTracking().SingleOrDefaultAsync(c => c.BoutiqueId == shop && c.Id == id);
        if (client == null) return NotFound();
        var sales = await db.Sales.AsNoTracking().Where(s => s.BoutiqueId == shop && s.ClientId == id && !s.IsCancelled)
            .Include(s => s.Payments).ToListAsync();
        List<Debt> oldDebts = User.IsInRole("Admin")
            ? await db.Debts.AsNoTracking().Where(d => d.BoutiqueId == shop && d.ClientId == id && !d.IsArchived).Include(d => d.Payments).ToListAsync()
            : [];
        var history = sales.Select(s => new DocumentBalance(s.Id, s.Number, s.SaleDate, client.Name, s.CurrencyCode, s.FinalPrice, s.Paid, "Sale"))
            .Concat(oldDebts.Select(d => new DocumentBalance(d.Id, $"Créance #{d.Id}", d.DateCreated, client.Name, d.CurrencyCode, d.Amount, d.Paid, "Debt")))
            .OrderByDescending(x => x.Date).ToList();
        return View(new BusinessHistory<Client>(client, history));
    }

    [HttpGet]
    public IActionResult Create() => View("Form", new ContactInput());

    [HttpPost]
    public Task<IActionResult> Create(ContactInput model) => SaveAsync(null, model);

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        if (await boutiques.GetCurrentBoutiqueIdAsync() is not int shop) return Forbid();
        var client = await db.Clients.AsNoTracking().SingleOrDefaultAsync(c => c.BoutiqueId == shop && c.Id == id);
        if (client == null) return NotFound();
        ViewData["ContactId"] = id;
        return View("Form", new ContactInput { Name = client.Name, Phone = client.Phone, Address = client.Address, Email = client.Email });
    }

    [HttpPost]
    public Task<IActionResult> Edit(int id, ContactInput model) => SaveAsync(id, model);

    [HttpPost, Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        if (await boutiques.GetCurrentBoutiqueIdAsync() is not int shop) return Forbid();
        try
        {
            await writer.ExecuteAsync(shop, async () =>
            {
                var client = await db.Clients.SingleOrDefaultAsync(c => c.BoutiqueId == shop && c.Id == id) ?? throw new CommerceNotFoundException();
                if (await db.Sales.AnyAsync(s => s.BoutiqueId == shop && s.ClientId == id) || await db.Quotes.AnyAsync(q => q.BoutiqueId == shop && q.ClientId == id)
                    || await db.Debts.AnyAsync(d => d.BoutiqueId == shop && d.ClientId == id))
                    throw new CommerceRuleException("Ce client a un historique et doit rester dans le répertoire.");
                db.Clients.Remove(client);
                writer.Audit(shop, $"Suppression de la fiche client #{id}");
                return true;
            });
            TempData["SuccessMessage"] = "Client supprimé.";
            return RedirectToAction(nameof(Index));
        }
        catch (CommerceRuleException error) { TempData["ErrorMessage"] = error.Message; }
        catch (CommerceNotFoundException) { return NotFound(); }
        catch (DbUpdateException) { TempData["ErrorMessage"] = "La fiche ne peut pas être supprimée car elle est liée à un document."; }
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task<IActionResult> SaveAsync(int? id, ContactInput model)
    {
        if (await boutiques.GetCurrentBoutiqueIdAsync() is not int shop) return Forbid();
        if (ModelState.IsValid)
        {
            try
            {
                var client = await writer.ExecuteAsync(shop, async () =>
                {
                    var contact = id.HasValue
                        ? await db.Clients.SingleOrDefaultAsync(c => c.BoutiqueId == shop && c.Id == id) ?? throw new CommerceNotFoundException()
                        : new Client { BoutiqueId = shop, CreatedAt = DateTime.Now };
                    contact.Name = model.Name.Trim();
                    contact.Phone = CommerceOperations.Clean(model.Phone);
                    contact.Address = CommerceOperations.Clean(model.Address);
                    contact.Email = CommerceOperations.Clean(model.Email);
                    if (!id.HasValue) db.Clients.Add(contact);
                    writer.Audit(shop, id.HasValue ? $"Fiche client #{id} modifiée" : "Fiche client créée");
                    return contact;
                });
                TempData["SuccessMessage"] = "Fiche client enregistrée.";
                return RedirectToAction(nameof(Details), new { id = client.Id });
            }
            catch (CommerceNotFoundException) { return NotFound(); }
        }
        ViewData["ContactId"] = id;
        return View("Form", model);
    }
}
