using System.Globalization;
using AdGestionHub.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AdGestionHub.Services;

public sealed class PdfReportService : IPdfReportService
{
    private const string Ink = "#17233B";
    private const string Accent = "#BC3A16";
    private const string Border = "#DFE4ED";
    private const string Muted = "#475569";
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    public byte[] SalePdf(Sale sale, StoreSettings settings, byte[]? logo = null) => Commercial(settings, logo,
        sale.DocumentType == "Invoice" ? "Facture commerciale" : "Reçu commercial", sale.Number, sale.SaleDate,
        sale.CustomerName ?? "Client de passage", sale.CurrencyCode, sale.IsCancelled ? "ANNULÉE" : sale.PaymentStatus,
        sale.Items.Select(i => (i.ProductName, i.Quantity, i.UnitPrice, i.EffectiveLineTotal)), sale.EffectiveSubtotalNet,
        sale.Discount, sale.TaxAmount, sale.FinalPrice, sale.IsCancelled ? null : sale.Paid, sale.IsCancelled ? null : sale.Balance,
        HistoricalRate(sale.CdfPerUsd, sale.SecondaryCurrencyCode, sale.ExchangeRateToSecondary, sale.CurrencyCode), sale.CancellationReason);

    public byte[] ReportPdf(BusinessReport report) => Document.Create(container =>
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(30);
            page.DefaultTextStyle(t => t.FontSize(9).FontColor(Ink));
            page.Header().PaddingBottom(14).Column(title =>
            {
                title.Item().Text(report.Settings.StoreName).Bold().FontSize(14);
                title.Item().Text($"Rapport d'activité | {report.Currency} | {report.Period.Start:dd/MM/yyyy} – {report.Period.End:dd/MM/yyyy}").FontColor(Muted);
            });
            page.Content().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1.4f); columns.ConstantColumn(62); columns.RelativeColumn(1.1f);
                    columns.RelativeColumn(2.8f); columns.ConstantColumn(35);
                    for (var i = 0; i < 3; i++) columns.RelativeColumn(0.85f);
                    columns.ConstantColumn(47);
                });
                table.Header(h =>
                {
                    foreach (var heading in ReportExport.Columns) h.Cell().Element(HeaderCell).Text(heading).SemiBold();
                });
                foreach (var row in ReportExport.Rows(report))
                    foreach (var entry in row)
                        table.Cell().Element(BodyCell).Text(entry switch
                        {
                            decimal number => number.ToString("N2", French),
                            IFormattable value => value.ToString(null, French),
                            _ => entry?.ToString() ?? ""
                        });
            });
            Footer(page);
        });
    }).GeneratePdf();

    private static byte[] Commercial(StoreSettings settings, byte[]? logo, string title, string reference, DateTime date, string customer,
        string currency, string status, IEnumerable<(string Name, int Quantity, decimal Price, decimal Total)> lines, decimal subtotal,
        decimal discount, decimal tax, decimal total, decimal? paid, decimal? balance, string? exchange, string? notes) => Document.Create(container =>
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(35);
            page.DefaultTextStyle(t => t.FontSize(10).FontColor(Ink));
            page.Header().PaddingBottom(16).Column(head =>
            {
                if (logo != null) head.Item().PaddingBottom(6).AlignLeft().Width(122).Height(54).Image(logo).FitArea();
                head.Item().Text(settings.StoreName).FontSize(18).Bold();
                if (!string.IsNullOrWhiteSpace(settings.Address)) head.Item().Text(settings.Address).FontColor(Muted);
                var contact = string.Join(" · ", new[] { settings.Phone, settings.ContactEmail }.Where(x => !string.IsNullOrWhiteSpace(x)));
                if (contact.Length > 0) head.Item().Text(contact).FontColor(Muted);
            });
            page.Content().Column(content =>
            {
                content.Item().Text(title).FontSize(24).Bold();
                content.Item().Text($"{reference} · {date:dd/MM/yyyy} · {currency}").FontColor(Muted);
                content.Item().PaddingTop(3).Text(status).SemiBold().FontColor(Accent);
                content.Item().PaddingTop(12).Text($"Client : {customer}");
                content.Item().PaddingVertical(12).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(4); c.RelativeColumn(1); c.RelativeColumn(2); c.RelativeColumn(2);
                    });
                    table.Header(h =>
                    {
                        h.Cell().Element(HeaderCell).Text("Produit").SemiBold();
                        h.Cell().Element(HeaderCell).AlignRight().Text("Qté").SemiBold();
                        h.Cell().Element(HeaderCell).AlignRight().Text("Prix unitaire").SemiBold();
                        h.Cell().Element(HeaderCell).AlignRight().Text("Total").SemiBold();
                    });
                    foreach (var line in lines)
                    {
                        table.Cell().Element(BodyCell).Text(line.Name);
                        table.Cell().Element(BodyCell).AlignRight().Text(line.Quantity.ToString(French));
                        table.Cell().Element(BodyCell).AlignRight().Text(Money.Display(line.Price, currency));
                        table.Cell().Element(BodyCell).AlignRight().Text(Money.Display(line.Total, currency));
                    }
                });
                content.Item().AlignRight().Column(totals =>
                {
                    if (discount != 0 || tax != 0) totals.Item().AlignRight().Text($"Sous-total : {Money.Display(subtotal, currency)}");
                    if (discount != 0) totals.Item().AlignRight().Text($"Remise : {Money.Display(discount, currency)}");
                    if (tax != 0) totals.Item().AlignRight().Text($"Taxe historique : {Money.Display(tax, currency)}");
                    totals.Item().AlignRight().Text($"Total : {Money.Display(total, currency)}").Bold().FontSize(14);
                    if (paid.HasValue) totals.Item().AlignRight().Text($"Réglé : {Money.Display(paid.Value, currency)}");
                    if (balance.HasValue) totals.Item().AlignRight().Text($"Reste à régler : {Money.Display(balance.Value, currency)}").SemiBold();
                    if (exchange != null) totals.Item().AlignRight().Text($"Taux enregistré : {exchange}").FontColor(Muted);
                });
                if (!string.IsNullOrWhiteSpace(notes)) content.Item().PaddingTop(10).Text(notes);
                if (!string.IsNullOrWhiteSpace(settings.FooterMessage)) content.Item().PaddingTop(10).Text(settings.FooterMessage).FontColor(Muted);
                content.Item().PaddingTop(15).Text("Document commercial ordinaire — non normalisé DGI.").FontSize(9).SemiBold().FontColor(Muted);
            });
            Footer(page, "Généré par AdGestionHub");
        });
    }).GeneratePdf();

    public static string? HistoricalRate(decimal? cdfPerUsd, string? secondary, decimal? legacyRate, string currency) =>
        cdfPerUsd is > 0 ? Money.RateLabel(cdfPerUsd.Value)
        : legacyRate is > 0 && !string.IsNullOrWhiteSpace(secondary) ? $"1 {secondary} = {legacyRate.Value.ToString("0.######", French)} {currency}" : null;

    private static void Footer(PageDescriptor page, string label = "AD Gestion Hub") => page.Footer().PaddingTop(12).BorderTop(1).BorderColor(Border).AlignCenter().Text(text =>
    {
        text.Span(label + " · ").FontColor(Muted);
        text.CurrentPageNumber();
        text.Span(" / ");
        text.TotalPages();
    });

    private static IContainer HeaderCell(IContainer container) => container.Background("#EDF1F7").Padding(6);
    private static IContainer BodyCell(IContainer container) => container.BorderBottom(1).BorderColor(Border).Padding(6);
}
