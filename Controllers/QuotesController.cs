using AdGestionHub.Data;
using AdGestionHub.Models;
using AdGestionHub.Models.ViewModels;
using AdGestionHub.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdGestionHub.Controllers;

[Authorize(Policy = "ShopMember")]
public sealed class QuotesController(ApplicationDbContext db, IBoutiqueService boutiques, ITenantContextService tenant,
    CommerceOperations commerce, IPdfReportService pdf) : TradeController(db, boutiques, tenant)
{
    public async Task<IActionResult> Index()
    {
        if (await ShopAsync() is not int shop) return Forbid();
        return View(await Db.Quotes.AsNoTracking().Where(q => q.BoutiqueId == shop)
            .OrderByDescending(q => q.CreatedAt).ThenByDescending(q => q.Id).ToListAsync());
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        if (await ShopAsync() is not int shop) return Forbid();
        await PrepareAsync(shop, quote: true);
        return View(new QuoteInput { CurrencyCode = (await Tenant.GetContextAsync()).PrimaryCurrency.Code });
    }

    [HttpPost]
    public Task<IActionResult> Create(QuoteInput model) => SaveAsync(null, model);

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        if (await ShopAsync() is not int shop) return Forbid();
        var quote = await FindAsync(shop, id);
        if (quote == null) return NotFound();
        if (quote.Status != QuoteStatus.Draft) return RedirectToAction(nameof(Details), new { id });
        await PrepareAsync(shop, quote: true);
        ViewData["QuoteId"] = id;
        return View("Create", new QuoteInput
        {
            ClientId = quote.ClientId, CurrencyCode = quote.CurrencyCode, CustomerName = quote.CustomerName,
            ValidUntil = quote.ValidUntil, Notes = quote.Notes,
            Items = quote.Items.Select(i => new TradeLineInput { ProductId = i.ProductId ?? 0, Quantity = i.Quantity }).ToList()
        });
    }

    [HttpPost]
    public Task<IActionResult> Edit(int id, QuoteInput model) => SaveAsync(id, model);

    public async Task<IActionResult> Details(int id)
    {
        if (await ShopAsync() is not int shop) return Forbid();
        var quote = await FindAsync(shop, id);
        return quote == null ? NotFound() : View(quote);
    }

    public async Task<IActionResult> DownloadPdf(int id)
    {
        if (await ShopAsync() is not int shop) return Forbid();
        var quote = await FindAsync(shop, id);
        if (quote == null) return NotFound();
        return File(pdf.QuotePdf(quote, (await Tenant.GetContextAsync()).Settings), "application/pdf", $"devis-{quote.Id}.pdf");
    }

    [HttpPost]
    public async Task<IActionResult> Accept(int id)
    {
        if (await ShopAsync() is not int shop) return Forbid();
        try
        {
            await commerce.AcceptQuoteAsync(shop, id);
            TempData["SuccessMessage"] = "Devis marqué comme accepté.";
        }
        catch (CommerceRuleException error) { TempData["ErrorMessage"] = error.Message; }
        catch (CommerceNotFoundException) { return NotFound(); }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> ConvertToSale(int id)
    {
        if (await ShopAsync() is not int shop) return Forbid();
        try
        {
            var saleId = await commerce.ConvertQuoteAsync(shop, id);
            TempData["SuccessMessage"] = "Vente issue du devis. Vous pouvez enregistrer le règlement depuis la vente.";
            return RedirectToAction("Details", "Sales", new { id = saleId });
        }
        catch (CommerceRuleException error) { TempData["ErrorMessage"] = error.Message; }
        catch (CommerceNotFoundException) { return NotFound(); }
        return RedirectToAction(nameof(Details), new { id });
    }

    private Task<Quote?> FindAsync(int shop, int id) => Db.Quotes.AsNoTracking().Include(q => q.Items.Where(i => i.BoutiqueId == shop))
        .SingleOrDefaultAsync(q => q.Id == id && q.BoutiqueId == shop);

    private async Task<IActionResult> SaveAsync(int? id, QuoteInput model)
    {
        if (await ShopAsync() is not int shop) return Forbid();
        if (ModelState.IsValid)
        {
            try
            {
                var quote = await commerce.SaveQuoteAsync(shop, id, model);
                TempData["SuccessMessage"] = $"Devis {quote.DocumentNumber} enregistré.";
                return RedirectToAction(nameof(Details), new { id = quote.Id });
            }
            catch (CommerceRuleException error) { ModelState.AddModelError("", error.Message); }
            catch (CommerceNotFoundException) { return NotFound(); }
        }
        await PrepareAsync(shop, quote: true);
        ViewData["QuoteId"] = id;
        return View("Create", model);
    }
}
