using System;
using System.Linq;
using System.Threading.Tasks;
using AdGestionHub.Data;
using AdGestionHub.Models;
using AdGestionHub.Models.ViewModels;
using AdGestionHub.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdGestionHub.Controllers
{
    [Authorize]
    public class ClientsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IBoutiqueService _boutiqueService;
        private readonly ITenantContextService _tenantContext;
        private readonly IAppLocalizer _L;

        public ClientsController(
            ApplicationDbContext context,
            IBoutiqueService boutiqueService,
            ITenantContextService tenantContext,
            IAppLocalizer localizer)
        {
            _context = context;
            _boutiqueService = boutiqueService;
            _tenantContext = tenantContext;
            _L = localizer;
        }

        public async Task<IActionResult> Index(string? search)
        {
            var boutiqueId = await _boutiqueService.GetCurrentBoutiqueIdAsync();
            if (boutiqueId == null) return Challenge();

            var query = _context.Clients
                .AsNoTracking()
                .Where(c => c.BoutiqueId == boutiqueId.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(c =>
                    c.Name.Contains(term)
                    || (c.Phone != null && c.Phone.Contains(term))
                    || (c.TaxIdentificationNumber != null && c.TaxIdentificationNumber.Contains(term)));
            }

            ViewBag.CurrentSearch = search;
            var clients = await query.OrderBy(c => c.Name).ToListAsync();
            return View(clients);
        }

        public async Task<IActionResult> Create()
        {
            var boutiqueId = await _boutiqueService.GetCurrentBoutiqueIdAsync();
            if (boutiqueId == null) return Challenge();

            var ctx = await _tenantContext.GetContextAsync();
            return View(new ClientFormViewModel
            {
                Phone = ctx.Settings.PhoneDialCode + " "
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ClientFormViewModel model)
        {
            var boutiqueId = await _boutiqueService.GetCurrentBoutiqueIdAsync();
            if (boutiqueId == null) return Challenge();

            if (!ModelState.IsValid)
                return View(model);

            var ctx = await _tenantContext.GetContextAsync();
            var cleanPhone = model.Phone?.Trim();
            if (string.Equals(cleanPhone, ctx.Settings.PhoneDialCode, StringComparison.OrdinalIgnoreCase))
                cleanPhone = null;

            var client = new Client
            {
                BoutiqueId = boutiqueId.Value,
                Name = model.Name.Trim(),
                Phone = string.IsNullOrWhiteSpace(cleanPhone) ? null : cleanPhone,
                Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim(),
                Address = string.IsNullOrWhiteSpace(model.Address) ? null : model.Address.Trim(),
                IsBusinessClient = model.IsBusinessClient,
                TaxIdentificationNumber = ctx.IsFormalized && !string.IsNullOrWhiteSpace(model.TaxIdentificationNumber)
                    ? model.TaxIdentificationNumber.Trim()
                    : null,
                Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim(),
                CreatedAt = DateTime.Now
            };

            _context.Clients.Add(client);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = _L.T(
                $"Fiche client « {client.Name} » enregistrée.",
                $"Customer « {client.Name} » saved.");
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var boutiqueId = await _boutiqueService.GetCurrentBoutiqueIdAsync();
            if (boutiqueId == null) return Challenge();

            var client = await _context.Clients
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id.Value && c.BoutiqueId == boutiqueId.Value);

            if (client == null) return NotFound();

            return View(new ClientFormViewModel
            {
                Id = client.Id,
                Name = client.Name,
                Phone = client.Phone,
                Email = client.Email,
                Address = client.Address,
                IsBusinessClient = client.IsBusinessClient,
                TaxIdentificationNumber = client.TaxIdentificationNumber,
                Notes = client.Notes
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ClientFormViewModel model)
        {
            if (id != model.Id) return NotFound();

            var boutiqueId = await _boutiqueService.GetCurrentBoutiqueIdAsync();
            if (boutiqueId == null) return Challenge();

            var client = await _context.Clients
                .FirstOrDefaultAsync(c => c.Id == id && c.BoutiqueId == boutiqueId.Value);

            if (client == null) return NotFound();

            if (!ModelState.IsValid)
                return View(model);

            var ctx = await _tenantContext.GetContextAsync();

            client.Name = model.Name.Trim();
            client.Phone = string.IsNullOrWhiteSpace(model.Phone) ? null : model.Phone.Trim();
            client.Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim();
            client.Address = string.IsNullOrWhiteSpace(model.Address) ? null : model.Address.Trim();
            client.IsBusinessClient = model.IsBusinessClient;
            if (ctx.IsFormalized)
            {
                client.TaxIdentificationNumber = string.IsNullOrWhiteSpace(model.TaxIdentificationNumber)
                    ? null
                    : model.TaxIdentificationNumber.Trim();
            }
            client.Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim();

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = _L.T("Fiche client mise à jour.", "Customer record updated.");
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> Delete(int id)
        {
            var boutiqueId = await _boutiqueService.GetCurrentBoutiqueIdAsync();
            if (boutiqueId == null) return Challenge();

            var client = await _context.Clients
                .FirstOrDefaultAsync(c => c.Id == id && c.BoutiqueId == boutiqueId.Value);

            if (client != null)
            {
                await _context.Sales
                    .Where(s => s.BoutiqueId == boutiqueId.Value && s.ClientId == client.Id)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.ClientId, (int?)null));

                await _context.Quotes
                    .Where(q => q.BoutiqueId == boutiqueId.Value && q.ClientId == client.Id)
                    .ExecuteUpdateAsync(q => q.SetProperty(x => x.ClientId, (int?)null));

                _context.Clients.Remove(client);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = _L.T("Client supprimé du répertoire.", "Customer removed.");
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> SearchJson(string? term)
        {
            var boutiqueId = await _boutiqueService.GetCurrentBoutiqueIdAsync();
            if (boutiqueId == null) return Json(Array.Empty<object>());

            var query = _context.Clients
                .AsNoTracking()
                .Where(c => c.BoutiqueId == boutiqueId.Value);

            if (!string.IsNullOrWhiteSpace(term))
            {
                var clean = term.Trim();
                query = query.Where(c => c.Name.Contains(clean) || (c.Phone != null && c.Phone.Contains(clean)));
            }

            var list = await query
                .OrderBy(c => c.Name)
                .Take(10)
                .Select(c => new
                {
                    id = c.Id,
                    name = c.Name,
                    phone = c.Phone ?? "",
                    taxId = c.TaxIdentificationNumber ?? "",
                    isB2B = c.IsBusinessClient
                })
                .ToListAsync();

            return Json(list);
        }
    }
}
