using System;
using System.Linq;
using AdGestionHub.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AdGestionHub.Services
{
    public class PdfReportService : IPdfReportService
    {
        private const string ColorInk = "#0F172A";
        private const string ColorSlate = "#475569";
        private const string ColorMuted = "#64748B";
        private const string ColorBorder = "#E2E8F0";
        private const string ColorSurfaceAlt = "#F8FAFC";
        private const string ColorAccent = "#FF5A1F";
        private const string ColorGreen = "#15803D";
        private const string ColorRed = "#B91C1C";

        public byte[] GenerateSaleReceiptPdf(Sale sale, StoreSettings? settings, int invoiceSequenceNumber, string language = "fr")
        {
            var en = string.Equals(language, "en", StringComparison.OrdinalIgnoreCase);
            var storeName = !string.IsNullOrWhiteSpace(settings?.StoreName) ? settings!.StoreName : "AdGestionHub";
            var address = settings?.Address;
            var phone = settings?.Phone;
            var email = settings?.ContactEmail;
            var footerMsg = !string.IsNullOrWhiteSpace(settings?.FooterMessage)
                ? settings!.FooterMessage
                : (en ? "Thank you for your business." : "Merci de votre confiance.");

            // Respect strict de l'instantané historique (non-rétroactivité)
            var isFormalized = sale.IsFormalizedSnapshot || string.Equals(sale.DocumentType, "Invoice", StringComparison.OrdinalIgnoreCase);
            var isVatEnabled = sale.IsVatEnabledSnapshot;
            var docNumber = !string.IsNullOrWhiteSpace(sale.DocumentNumber)
                ? sale.DocumentNumber
                : (isFormalized ? $"FAC-{invoiceSequenceNumber:D5}" : $"REC-{invoiceSequenceNumber:D5}");

            var docTitle = isFormalized
                ? (en ? "INVOICE" : "FACTURE")
                : (en ? "SALES RECEIPT" : "REÇU DE VENTE");

            string Fmt(decimal val) =>
                CountryProfileCatalog.FormatAmount(val, sale.CurrencyCode, sale.CurrencyDecimals, sale.CurrencySymbol, language);

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.8f, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(9.5f).FontColor(ColorInk));

                    page.Header().Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(left =>
                            {
                                left.Item().Text(storeName).FontSize(18).Bold().FontColor(ColorInk);
                                if (!string.IsNullOrWhiteSpace(address))
                                    left.Item().Text(address).FontColor(ColorSlate);
                                if (!string.IsNullOrWhiteSpace(phone))
                                    left.Item().Text($"{(en ? "Phone" : "Tél")} : {phone}").FontColor(ColorSlate);
                                if (!string.IsNullOrWhiteSpace(email))
                                    left.Item().Text($"Email : {email}").FontColor(ColorSlate);

                                if (isFormalized)
                                {
                                    if (!string.IsNullOrWhiteSpace(sale.StoreLegalTaxIdSnapshot))
                                        left.Item().PaddingTop(2).Text($"{sale.StoreTaxIdLabelSnapshot ?? "NIF"} : {sale.StoreLegalTaxIdSnapshot}").SemiBold().FontColor(ColorInk);
                                    if (!string.IsNullOrWhiteSpace(sale.StoreRccmSnapshot))
                                        left.Item().Text($"{sale.StoreRccmLabelSnapshot ?? "RCCM"} : {sale.StoreRccmSnapshot}").FontColor(ColorSlate);
                                }
                            });

                            row.ConstantItem(200).AlignRight().Column(right =>
                            {
                                right.Item().Text(docTitle).FontSize(14).Bold().FontColor(ColorInk);
                                right.Item().Text($"{(en ? "Ref" : "Réf")} : {docNumber}").SemiBold();
                                right.Item().Text($"{(en ? "Date" : "Date")} : {sale.SaleDate:dd/MM/yyyy HH:mm}").FontColor(ColorSlate);
                                right.Item().Text($"{(en ? "Payment" : "Règlement")} : {sale.PaymentMethod ?? "Cash"}").FontColor(ColorSlate);
                                if (sale.IsCancelled)
                                    right.Item().PaddingTop(4).Text(en ? "STATUS: VOIDED" : "STATUT : ANNULÉ").Bold().FontColor(ColorRed);
                            });
                        });

                        col.Item().PaddingTop(10).BorderBottom(1.5f).BorderColor(ColorAccent);
                    });

                    page.Content().PaddingVertical(16).Column(col =>
                    {
                        col.Item().PaddingBottom(14).Background(ColorSurfaceAlt).Border(1).BorderColor(ColorBorder).Padding(10).Row(r =>
                        {
                            r.RelativeItem().Column(c =>
                            {
                                c.Item().Text(en ? "Customer" : "Client").FontSize(8.5f).FontColor(ColorMuted);
                                c.Item().Text(string.IsNullOrWhiteSpace(sale.CustomerName)
                                    ? (en ? "Walk-in customer" : "Client comptoir")
                                    : sale.CustomerName).SemiBold();
                                if (isFormalized && !string.IsNullOrWhiteSpace(sale.CustomerTaxId))
                                    c.Item().Text($"{sale.StoreTaxIdLabelSnapshot ?? (en ? "Tax ID" : "Identifiant fiscal")} : {sale.CustomerTaxId}").FontSize(8.5f).FontColor(ColorSlate);
                            });

                            if (!string.IsNullOrWhiteSpace(sale.PaidInCurrencyCode))
                            {
                                r.ConstantItem(140).AlignRight().Column(c =>
                                {
                                    c.Item().Text(en ? "Tendered currency" : "Devise de règlement").FontSize(8.5f).FontColor(ColorMuted);
                                    c.Item().Text(sale.PaidInCurrencyCode).SemiBold();
                                });
                            }
                        });

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(4);
                                columns.ConstantColumn(50);
                                columns.ConstantColumn(95);
                                if (isVatEnabled)
                                    columns.ConstantColumn(55);
                                columns.ConstantColumn(95);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Background(ColorSurfaceAlt).BorderBottom(1).BorderColor(ColorBorder).Padding(6).Text(en ? "Description" : "Désignation").SemiBold().FontColor(ColorSlate);
                                header.Cell().Background(ColorSurfaceAlt).BorderBottom(1).BorderColor(ColorBorder).Padding(6).AlignCenter().Text(en ? "Qty" : "Qté").SemiBold().FontColor(ColorSlate);
                                header.Cell().Background(ColorSurfaceAlt).BorderBottom(1).BorderColor(ColorBorder).Padding(6).AlignRight().Text(isVatEnabled ? (en ? "Unit Excl." : "P.U. HT") : (en ? "Unit Price" : "Prix Unit.")).SemiBold().FontColor(ColorSlate);
                                if (isVatEnabled)
                                    header.Cell().Background(ColorSurfaceAlt).BorderBottom(1).BorderColor(ColorBorder).Padding(6).AlignRight().Text(en ? "VAT" : "TVA").SemiBold().FontColor(ColorSlate);
                                header.Cell().Background(ColorSurfaceAlt).BorderBottom(1).BorderColor(ColorBorder).Padding(6).AlignRight().Text(isVatEnabled ? (en ? "Line Excl." : "Montant HT") : (en ? "Total" : "Montant")).SemiBold().FontColor(ColorSlate);
                            });

                            foreach (var item in sale.Items)
                            {
                                var lineNet = item.EffectiveLineNet;
                                table.Cell().BorderBottom(1).BorderColor(ColorBorder).Padding(6).Text(item.ProductName);
                                table.Cell().BorderBottom(1).BorderColor(ColorBorder).Padding(6).AlignCenter().Text(item.Quantity.ToString());
                                table.Cell().BorderBottom(1).BorderColor(ColorBorder).Padding(6).AlignRight().Text(Fmt(item.UnitPrice));
                                if (isVatEnabled)
                                    table.Cell().BorderBottom(1).BorderColor(ColorBorder).Padding(6).AlignRight().Text($"{item.VatRate:0.##}%").FontColor(ColorSlate);
                                table.Cell().BorderBottom(1).BorderColor(ColorBorder).Padding(6).AlignRight().Text(Fmt(lineNet)).SemiBold();
                            }
                        });

                        col.Item().PaddingTop(14).AlignRight().Row(row =>
                        {
                            row.RelativeItem();
                            row.ConstantItem(250).Border(1).BorderColor(ColorBorder).Background(ColorSurfaceAlt).Padding(10).Column(tot =>
                            {
                                if (isVatEnabled)
                                {
                                    tot.Item().PaddingBottom(4).Row(r =>
                                    {
                                        r.RelativeItem().Text(en ? "Subtotal Excl. Tax" : "Total HT").FontColor(ColorSlate);
                                        r.ConstantItem(115).AlignRight().Text(Fmt(sale.EffectiveSubtotalNet)).SemiBold();
                                    });
                                    tot.Item().PaddingBottom(6).BorderBottom(1).BorderColor(ColorBorder).Row(r =>
                                    {
                                        r.RelativeItem().Text(en ? "VAT Amount" : "Montant TVA").FontColor(ColorSlate);
                                        r.ConstantItem(115).AlignRight().Text(Fmt(sale.TaxAmount)).SemiBold();
                                    });
                                    tot.Item().PaddingTop(6).Row(r =>
                                    {
                                        r.RelativeItem().Text(en ? "Total Incl. Tax" : "Total TTC").FontSize(11.5f).Bold();
                                        r.ConstantItem(115).AlignRight().Text(Fmt(sale.FinalPrice)).FontSize(11.5f).Bold().FontColor(ColorInk);
                                    });
                                }
                                else
                                {
                                    tot.Item().Row(r =>
                                    {
                                        r.RelativeItem().Text(en ? "Net Total" : "Total Net").FontSize(11.5f).Bold();
                                        r.ConstantItem(115).AlignRight().Text(Fmt(sale.FinalPrice)).FontSize(11.5f).Bold().FontColor(ColorInk);
                                    });
                                }

                                if (!string.IsNullOrWhiteSpace(sale.SecondaryCurrencyCode) && sale.ExchangeRateToSecondary.HasValue && sale.ExchangeRateToSecondary.Value > 0m)
                                {
                                    var secCurrency = CountryProfileCatalog.GetCurrency(sale.SecondaryCurrencyCode);
                                    var secVal = CountryProfileCatalog.ConvertPrimaryToSecondary(sale.FinalPrice, sale.ExchangeRateToSecondary, secCurrency.Decimals);
                                    if (secVal.HasValue)
                                    {
                                        var secText = CountryProfileCatalog.FormatAmount(secVal.Value, secCurrency.Code, secCurrency.Decimals, secCurrency.Symbol, language);
                                        tot.Item().PaddingTop(6).Row(r =>
                                        {
                                            r.RelativeItem().Text(en ? $"Eq. ({secCurrency.Code})" : $"Contre-valeur ({secCurrency.Code})").FontSize(8.5f).FontColor(ColorMuted);
                                            r.ConstantItem(115).AlignRight().Text(secText).FontSize(8.5f).FontColor(ColorSlate);
                                        });
                                    }
                                }
                            });
                        });

                        if (isFormalized && !isVatEnabled)
                        {
                            col.Item().PaddingTop(10).AlignRight().Text(en ? "VAT not applicable" : "TVA non applicable").FontSize(8.5f).Italic().FontColor(ColorSlate);
                        }
                    });

                    page.Footer().AlignCenter().Column(col =>
                    {
                        col.Item().BorderTop(1).BorderColor(ColorBorder).PaddingTop(8).Text(footerMsg).FontColor(ColorSlate);
                        col.Item().PaddingTop(2).Text($"AdGestionHub — {DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(8).FontColor(ColorMuted);
                    });
                });
            });

            return document.GeneratePdf();
        }

        public byte[] GenerateQuotePdf(Quote quote, StoreSettings? settings, string language = "fr")
        {
            var en = string.Equals(language, "en", StringComparison.OrdinalIgnoreCase);
            var storeName = !string.IsNullOrWhiteSpace(settings?.StoreName) ? settings!.StoreName : "AdGestionHub";
            var isVatEnabled = quote.IsVatEnabledSnapshot;

            string Fmt(decimal val) =>
                CountryProfileCatalog.FormatAmount(val, quote.CurrencyCode, quote.CurrencyDecimals, quote.CurrencySymbol, language);

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.8f, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(9.5f).FontColor(ColorInk));

                    page.Header().Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(left =>
                            {
                                left.Item().Text(storeName).FontSize(18).Bold().FontColor(ColorInk);
                                if (!string.IsNullOrWhiteSpace(settings?.Address))
                                    left.Item().Text(settings.Address).FontColor(ColorSlate);
                                if (!string.IsNullOrWhiteSpace(settings?.Phone))
                                    left.Item().Text($"{(en ? "Phone" : "Tél")} : {settings.Phone}").FontColor(ColorSlate);
                                if (!string.IsNullOrWhiteSpace(quote.StoreLegalTaxIdSnapshot))
                                    left.Item().PaddingTop(2).Text($"{quote.StoreTaxIdLabelSnapshot ?? "NIF"} : {quote.StoreLegalTaxIdSnapshot}").SemiBold();
                                if (!string.IsNullOrWhiteSpace(quote.StoreRccmSnapshot))
                                    left.Item().Text($"{quote.StoreRccmLabelSnapshot ?? "RCCM"} : {quote.StoreRccmSnapshot}").FontColor(ColorSlate);
                            });

                            row.ConstantItem(200).AlignRight().Column(right =>
                            {
                                right.Item().Text(en ? "QUOTE / PROFORMA" : "DEVIS / PROFORMA").FontSize(14).Bold();
                                right.Item().Text($"{(en ? "Ref" : "Réf")} : {quote.DocumentNumber}").SemiBold();
                                right.Item().Text($"Date : {quote.CreatedAt:dd/MM/yyyy}").FontColor(ColorSlate);
                                if (quote.ValidUntil.HasValue)
                                    right.Item().Text($"{(en ? "Valid until" : "Valable jusqu'au")} : {quote.ValidUntil.Value:dd/MM/yyyy}").FontColor(ColorSlate);
                            });
                        });

                        col.Item().PaddingTop(10).BorderBottom(1.5f).BorderColor(ColorAccent);
                    });

                    page.Content().PaddingVertical(16).Column(col =>
                    {
                        col.Item().PaddingBottom(14).Background(ColorSurfaceAlt).Border(1).BorderColor(ColorBorder).Padding(10).Column(c =>
                        {
                            c.Item().Text(en ? "Prepared for" : "Destinataire").FontSize(8.5f).FontColor(ColorMuted);
                            c.Item().Text(quote.CustomerName).SemiBold();
                            if (!string.IsNullOrWhiteSpace(quote.CustomerPhone))
                                c.Item().Text(quote.CustomerPhone).FontColor(ColorSlate);
                            if (!string.IsNullOrWhiteSpace(quote.CustomerTaxId))
                                c.Item().Text($"{quote.StoreTaxIdLabelSnapshot ?? (en ? "Tax ID" : "Identifiant fiscal")} : {quote.CustomerTaxId}").FontSize(8.5f).FontColor(ColorSlate);
                        });

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(4);
                                columns.ConstantColumn(50);
                                columns.ConstantColumn(95);
                                if (isVatEnabled)
                                    columns.ConstantColumn(55);
                                columns.ConstantColumn(95);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Background(ColorSurfaceAlt).BorderBottom(1).BorderColor(ColorBorder).Padding(6).Text(en ? "Description" : "Désignation").SemiBold().FontColor(ColorSlate);
                                header.Cell().Background(ColorSurfaceAlt).BorderBottom(1).BorderColor(ColorBorder).Padding(6).AlignCenter().Text(en ? "Qty" : "Qté").SemiBold().FontColor(ColorSlate);
                                header.Cell().Background(ColorSurfaceAlt).BorderBottom(1).BorderColor(ColorBorder).Padding(6).AlignRight().Text(isVatEnabled ? (en ? "Unit Excl." : "P.U. HT") : (en ? "Unit Price" : "Prix Unit.")).SemiBold().FontColor(ColorSlate);
                                if (isVatEnabled)
                                    header.Cell().Background(ColorSurfaceAlt).BorderBottom(1).BorderColor(ColorBorder).Padding(6).AlignRight().Text(en ? "VAT" : "TVA").SemiBold().FontColor(ColorSlate);
                                header.Cell().Background(ColorSurfaceAlt).BorderBottom(1).BorderColor(ColorBorder).Padding(6).AlignRight().Text(isVatEnabled ? (en ? "Line Excl." : "Montant HT") : (en ? "Total" : "Montant")).SemiBold().FontColor(ColorSlate);
                            });

                            foreach (var item in quote.Items)
                            {
                                table.Cell().BorderBottom(1).BorderColor(ColorBorder).Padding(6).Text(item.ProductName);
                                table.Cell().BorderBottom(1).BorderColor(ColorBorder).Padding(6).AlignCenter().Text(item.Quantity.ToString());
                                table.Cell().BorderBottom(1).BorderColor(ColorBorder).Padding(6).AlignRight().Text(Fmt(item.UnitPrice));
                                if (isVatEnabled)
                                    table.Cell().BorderBottom(1).BorderColor(ColorBorder).Padding(6).AlignRight().Text($"{item.VatRate:0.##}%").FontColor(ColorSlate);
                                table.Cell().BorderBottom(1).BorderColor(ColorBorder).Padding(6).AlignRight().Text(Fmt(item.LineNet)).SemiBold();
                            }
                        });

                        col.Item().PaddingTop(14).AlignRight().Row(row =>
                        {
                            row.RelativeItem();
                            row.ConstantItem(250).Border(1).BorderColor(ColorBorder).Background(ColorSurfaceAlt).Padding(10).Column(tot =>
                            {
                                if (isVatEnabled)
                                {
                                    tot.Item().PaddingBottom(4).Row(r =>
                                    {
                                        r.RelativeItem().Text(en ? "Subtotal Excl. Tax" : "Total HT").FontColor(ColorSlate);
                                        r.ConstantItem(115).AlignRight().Text(Fmt(quote.SubtotalNet)).SemiBold();
                                    });
                                    tot.Item().PaddingBottom(6).BorderBottom(1).BorderColor(ColorBorder).Row(r =>
                                    {
                                        r.RelativeItem().Text(en ? "VAT Amount" : "Montant TVA").FontColor(ColorSlate);
                                        r.ConstantItem(115).AlignRight().Text(Fmt(quote.TaxAmount)).SemiBold();
                                    });
                                    tot.Item().PaddingTop(6).Row(r =>
                                    {
                                        r.RelativeItem().Text(en ? "Total Incl. Tax" : "Total TTC").FontSize(11.5f).Bold();
                                        r.ConstantItem(115).AlignRight().Text(Fmt(quote.TotalAmount)).FontSize(11.5f).Bold();
                                    });
                                }
                                else
                                {
                                    tot.Item().Row(r =>
                                    {
                                        r.RelativeItem().Text(en ? "Net Total" : "Total Net").FontSize(11.5f).Bold();
                                        r.ConstantItem(115).AlignRight().Text(Fmt(quote.TotalAmount)).FontSize(11.5f).Bold();
                                    });
                                }
                            });
                        });

                        if (!isVatEnabled)
                        {
                            col.Item().PaddingTop(8).AlignRight().Text(en ? "VAT not applicable" : "TVA non applicable").FontSize(8.5f).Italic().FontColor(ColorSlate);
                        }

                        if (!string.IsNullOrWhiteSpace(quote.Notes))
                        {
                            col.Item().PaddingTop(14).Text($"{(en ? "Terms & Notes" : "Conditions & Notes")} : {quote.Notes}").FontSize(8.5f).FontColor(ColorSlate);
                        }
                    });

                    page.Footer().AlignCenter().Text($"AdGestionHub — {DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(8).FontColor(ColorMuted);
                });
            });

            return document.GeneratePdf();
        }

        public byte[] GeneratePeriodReportPdf(ReportViewModel report, string language = "fr")
        {
            var en = string.Equals(language, "en", StringComparison.OrdinalIgnoreCase);
            var periodLabel = report.StartDate.Date == report.EndDate.Date
                ? $"{report.StartDate:dd/MM/yyyy}"
                : $"{report.StartDate:dd/MM/yyyy} — {report.EndDate:dd/MM/yyyy}";

            string Fmt(decimal val) =>
                CountryProfileCatalog.FormatAmount(val, report.CurrencyCode, report.CurrencyDecimals, report.CurrencySymbol, language);

            string FmtSale(Sale s, decimal val) =>
                CountryProfileCatalog.FormatAmount(val, s.CurrencyCode, s.CurrencyDecimals, s.CurrencySymbol, language);

            string FmtExp(Expense e, decimal val) =>
                CountryProfileCatalog.FormatAmount(val, e.CurrencyCode, e.CurrencyDecimals, e.CurrencySymbol, language);

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.5f, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(9.5f).FontColor(ColorInk));

                    page.Header().Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(left =>
                            {
                                left.Item().Text(report.StoreName).FontSize(17).Bold().FontColor(ColorInk);
                                left.Item().Text($"{(en ? "Financial Report" : "Rapport financier")} ({periodLabel})").FontSize(10.5f).FontColor(ColorSlate);
                            });

                            row.ConstantItem(170).AlignRight().Column(right =>
                            {
                                right.Item().Text(en ? "FINANCIAL STATEMENT" : "ÉTAT FINANCIER").Bold();
                                right.Item().Text($"{DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(8.5f).FontColor(ColorMuted);
                            });
                        });

                        col.Item().PaddingTop(8).BorderBottom(1.5f).BorderColor(ColorAccent);
                    });

                    page.Content().PaddingVertical(12).Column(col =>
                    {
                        col.Item().PaddingBottom(14).Row(row =>
                        {
                            row.RelativeItem().Background(ColorSurfaceAlt).Border(1).BorderColor(ColorBorder).Padding(8).Column(c =>
                            {
                                c.Item().Text(report.IsVatEnabled ? (en ? "Revenue (Incl. Tax)" : "CA TTC") : (en ? "Net Revenue" : "Chiffre d'affaires")).FontSize(8.5f).FontColor(ColorMuted);
                                c.Item().Text(Fmt(report.TotalSales)).FontSize(12).Bold().FontColor(ColorGreen);
                                if (report.IsVatEnabled)
                                    c.Item().Text($"{(en ? "Excl." : "HT")}: {Fmt(report.TotalSubtotalNet)} | {(en ? "VAT" : "TVA")}: {Fmt(report.TotalTaxAmount)}").FontSize(7.5f).FontColor(ColorSlate);
                            });
                            row.ConstantItem(8);
                            row.RelativeItem().Background(ColorSurfaceAlt).Border(1).BorderColor(ColorBorder).Padding(8).Column(c =>
                            {
                                c.Item().Text(en ? "Gross Margin" : "Marge brute").FontSize(8.5f).FontColor(ColorMuted);
                                c.Item().Text(Fmt(report.GrossProfit)).FontSize(12).Bold();
                            });
                            row.ConstantItem(8);
                            row.RelativeItem().Background(ColorSurfaceAlt).Border(1).BorderColor(ColorBorder).Padding(8).Column(c =>
                            {
                                c.Item().Text(en ? "Expenses" : "Dépenses").FontSize(8.5f).FontColor(ColorMuted);
                                c.Item().Text(Fmt(report.TotalExpenses)).FontSize(12).Bold().FontColor(ColorRed);
                            });
                            row.ConstantItem(8);
                            row.RelativeItem().Background(ColorSurfaceAlt).Border(1).BorderColor(ColorBorder).Padding(8).Column(c =>
                            {
                                c.Item().Text(en ? "Net Profit" : "Bénéfice net").FontSize(8.5f).FontColor(ColorMuted);
                                c.Item().Text(Fmt(report.NetProfit)).FontSize(12).Bold()
                                    .FontColor(report.NetProfit >= 0 ? ColorGreen : ColorRed);
                            });
                        });

                        col.Item().PaddingBottom(6).Text($"{(en ? "Recorded Sales" : "Ventes enregistrées")} ({report.Sales.Count})").FontSize(11).Bold();
                        if (report.Sales.Any())
                        {
                            col.Item().PaddingBottom(16).Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(80);
                                    columns.ConstantColumn(75);
                                    columns.RelativeColumn(3);
                                    columns.ConstantColumn(80);
                                    columns.ConstantColumn(85);
                                });

                                table.Header(h =>
                                {
                                    h.Cell().Background(ColorSurfaceAlt).BorderBottom(1).BorderColor(ColorBorder).Padding(5).Text("Date").SemiBold().FontColor(ColorSlate);
                                    h.Cell().Background(ColorSurfaceAlt).BorderBottom(1).BorderColor(ColorBorder).Padding(5).Text(en ? "Ref" : "Réf").SemiBold().FontColor(ColorSlate);
                                    h.Cell().Background(ColorSurfaceAlt).BorderBottom(1).BorderColor(ColorBorder).Padding(5).Text(en ? "Items" : "Articles").SemiBold().FontColor(ColorSlate);
                                    h.Cell().Background(ColorSurfaceAlt).BorderBottom(1).BorderColor(ColorBorder).Padding(5).Text(en ? "Payment" : "Paiement").SemiBold().FontColor(ColorSlate);
                                    h.Cell().Background(ColorSurfaceAlt).BorderBottom(1).BorderColor(ColorBorder).Padding(5).AlignRight().Text("Total").SemiBold().FontColor(ColorSlate);
                                });

                                foreach (var sale in report.Sales)
                                {
                                    var articles = sale.Items.Any()
                                        ? string.Join(", ", sale.Items.Select(i => $"{i.ProductName} (x{i.Quantity})"))
                                        : "-";
                                    var docRef = !string.IsNullOrWhiteSpace(sale.DocumentNumber) ? sale.DocumentNumber : $"#{sale.Id}";
                                    table.Cell().BorderBottom(1).BorderColor(ColorBorder).Padding(5).Text($"{sale.SaleDate:dd/MM HH:mm}");
                                    table.Cell().BorderBottom(1).BorderColor(ColorBorder).Padding(5).Text(docRef).FontColor(ColorSlate);
                                    table.Cell().BorderBottom(1).BorderColor(ColorBorder).Padding(5).Text(articles);
                                    table.Cell().BorderBottom(1).BorderColor(ColorBorder).Padding(5).Text(sale.PaymentMethod ?? "Cash");
                                    table.Cell().BorderBottom(1).BorderColor(ColorBorder).Padding(5).AlignRight().Text(FmtSale(sale, sale.FinalPrice)).SemiBold();
                                }
                            });
                        }
                        else
                        {
                            col.Item().PaddingBottom(16).Text(en ? "No sales recorded in this period." : "Aucune vente sur cette période.").FontColor(ColorMuted);
                        }

                        col.Item().PaddingBottom(6).Text($"{(en ? "Recorded Expenses" : "Dépenses enregistrées")} ({report.Expenses.Count})").FontSize(11).Bold();
                        if (report.Expenses.Any())
                        {
                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(85);
                                    columns.ConstantColumn(100);
                                    columns.RelativeColumn(3);
                                    columns.ConstantColumn(85);
                                });

                                table.Header(h =>
                                {
                                    h.Cell().Background(ColorSurfaceAlt).BorderBottom(1).BorderColor(ColorBorder).Padding(5).Text("Date").SemiBold().FontColor(ColorSlate);
                                    h.Cell().Background(ColorSurfaceAlt).BorderBottom(1).BorderColor(ColorBorder).Padding(5).Text(en ? "Category" : "Catégorie").SemiBold().FontColor(ColorSlate);
                                    h.Cell().Background(ColorSurfaceAlt).BorderBottom(1).BorderColor(ColorBorder).Padding(5).Text("Description").SemiBold().FontColor(ColorSlate);
                                    h.Cell().Background(ColorSurfaceAlt).BorderBottom(1).BorderColor(ColorBorder).Padding(5).AlignRight().Text(en ? "Amount" : "Montant").SemiBold().FontColor(ColorSlate);
                                });

                                foreach (var exp in report.Expenses)
                                {
                                    table.Cell().BorderBottom(1).BorderColor(ColorBorder).Padding(5).Text($"{exp.Date:dd/MM/yyyy}");
                                    table.Cell().BorderBottom(1).BorderColor(ColorBorder).Padding(5).Text(exp.Category);
                                    table.Cell().BorderBottom(1).BorderColor(ColorBorder).Padding(5).Text(exp.Description);
                                    table.Cell().BorderBottom(1).BorderColor(ColorBorder).Padding(5).AlignRight().Text(FmtExp(exp, exp.Amount)).FontColor(ColorRed);
                                }
                            });
                        }
                        else
                        {
                            col.Item().Text(en ? "No expenses recorded in this period." : "Aucune dépense sur cette période.").FontColor(ColorMuted);
                        }
                    });

                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.Span("AdGestionHub | Page ").FontSize(8).FontColor(ColorMuted);
                        x.CurrentPageNumber().FontSize(8).FontColor(ColorMuted);
                        x.Span(" / ").FontSize(8).FontColor(ColorMuted);
                        x.TotalPages().FontSize(8).FontColor(ColorMuted);
                    });
                });
            });

            return document.GeneratePdf();
        }
    }
}
