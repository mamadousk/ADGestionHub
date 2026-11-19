using AdGestionHub.Models;
using AdGestionHub.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdGestionHub.Controllers;

[Authorize(Policy = StaffAccessRules.Reports)]
public sealed class ReportsController(IBoutiqueService boutiques, ITenantContextService tenant, BusinessReportService reports, IPdfReportService pdf) : Controller
{
    public async Task<IActionResult> Index(DateTime? startDate, DateTime? endDate, string? currency)
    {
        if (await boutiques.GetCurrentBoutiqueIdAsync() is not int shop) return Forbid();
        ReportPeriod period;
        try { period = ReportPeriod.Current(startDate, endDate); }
        catch (CommerceRuleException error)
        {
            ModelState.AddModelError("", error.Message);
            period = ReportPeriod.Current(null, null);
        }
        return View(await BuildAsync(shop, period, currency));
    }

    [HttpGet]
    public async Task<IActionResult> Export(string format, DateTime? startDate, DateTime? endDate, string? currency)
    {
        if (await boutiques.GetCurrentBoutiqueIdAsync() is not int shop) return Forbid();
        if (format is not ("pdf" or "xlsx" or "csv") || !ModelState.IsValid) return BadRequest();
        ReportPeriod period;
        try { period = ReportPeriod.Current(startDate, endDate); }
        catch (CommerceRuleException error) { return BadRequest(error.Message); }
        var report = await BuildAsync(shop, period, currency);
        var name = $"rapport-{period.Start:yyyyMMdd}-{period.End:yyyyMMdd}-{report.Currency}";
        return format switch
        {
            "pdf" => File(pdf.ReportPdf(report), "application/pdf", $"{name}.pdf"),
            "xlsx" => File(ReportExport.Excel(report), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{name}.xlsx"),
            _ => File(ReportExport.Csv(report), "text/csv; charset=utf-8", $"{name}.csv")
        };
    }

    private async Task<BusinessReport> BuildAsync(int shop, ReportPeriod period, string? currency)
    {
        var settings = (await tenant.GetContextAsync()).Settings;
        return await reports.BuildAsync(shop, settings, period, Money.IsMvpCurrency(currency) ? currency! : settings.PrimaryCurrencyCode, finance: true);
    }
}
