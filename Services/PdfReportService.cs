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
        public byte[] GenerateSaleReceiptPdf(Sale sale, StoreSettings? settings, int invoiceSequenceNumber)
        {
            var storeName = !string.IsNullOrWhiteSpace(settings?.StoreName) ? settings!.StoreName : "AdGestionHub";
            var address = settings?.Address;
            var phone = settings?.Phone;
            var email = settings?.ContactEmail;
            var footerMsg = !string.IsNullOrWhiteSpace(settings?.FooterMessage)
                ? settings!.FooterMessage
                : "Merci de votre confiance. À très bientôt !";

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.8f, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header().Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(left =>
                            {
                                left.Item().Text(storeName).FontSize(20).Bold().FontColor(Colors.Orange.Darken2);
                                if (!string.IsNullOrWhiteSpace(address))
                                    left.Item().Text(address).FontColor(Colors.Grey.Darken2);
                                if (!string.IsNullOrWhiteSpace(phone))
                                    left.Item().Text($"Tél : {phone}").FontColor(Colors.Grey.Darken2);
                                if (!string.IsNullOrWhiteSpace(email))
                                    left.Item().Text($"Email : {email}").FontColor(Colors.Grey.Darken2);
                            });

                            row.ConstantItem(180).AlignRight().Column(right =>
                            {
                                right.Item().Text("REÇU DE VENTE").FontSize(14).Bold();
                                right.Item().Text($"N° : #{invoiceSequenceNumber:D5}").SemiBold();
                                right.Item().Text($"Date : {sale.SaleDate:dd/MM/yyyy HH:mm}");
                                right.Item().Text($"Paiement : {sale.PaymentMethod ?? "Cash"}");
                            });
                        });

                        col.Item().PaddingTop(10).BorderBottom(2).BorderColor(Colors.Orange.Darken2);
                    });

                    page.Content().PaddingVertical(15).Column(col =>
                    {
                        col.Item().PaddingBottom(12).Text(text =>
                        {
                            text.Span("Client : ").SemiBold();
                            text.Span(string.IsNullOrWhiteSpace(sale.CustomerName) ? "Client au comptoir" : sale.CustomerName);
                        });

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(4);
                                columns.ConstantColumn(60);
                                columns.ConstantColumn(90);
                                columns.ConstantColumn(90);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Background(Colors.Grey.Lighten3).Padding(6).Text("Désignation").SemiBold();
                                header.Cell().Background(Colors.Grey.Lighten3).Padding(6).AlignCenter().Text("Qté").SemiBold();
                                header.Cell().Background(Colors.Grey.Lighten3).Padding(6).AlignRight().Text("Prix Unit.").SemiBold();
                                header.Cell().Background(Colors.Grey.Lighten3).Padding(6).AlignRight().Text("Sous-total").SemiBold();
                            });

                            foreach (var item in sale.Items)
                            {
                                var lineTotal = item.UnitPrice * item.Quantity;
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(6).Text(item.ProductName);
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(6).AlignCenter().Text(item.Quantity.ToString());
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(6).AlignRight().Text($"{item.UnitPrice:N2} $");
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(6).AlignRight().Text($"{lineTotal:N2} $").SemiBold();
                            }
                        });

                        col.Item().PaddingTop(14).AlignRight().Row(row =>
                        {
                            row.RelativeItem();
                            row.ConstantItem(220).Background(Colors.Grey.Lighten3).Padding(10).Row(tot =>
                            {
                                tot.RelativeItem().Text("TOTAL PAYÉ :").FontSize(12).Bold();
                                tot.ConstantItem(100).AlignRight().Text($"{sale.FinalPrice:N2} $").FontSize(12).Bold().FontColor(Colors.Green.Darken2);
                            });
                        });
                    });

                    page.Footer().AlignCenter().Column(col =>
                    {
                        col.Item().BorderTop(1).BorderColor(Colors.Grey.Lighten2).PaddingTop(8).Text(footerMsg!).FontColor(Colors.Grey.Darken1);
                        col.Item().PaddingTop(2).Text($"Généré par AdGestionHub — {DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(8).FontColor(Colors.Grey.Medium);
                    });
                });
            });

            return document.GeneratePdf();
        }

        public byte[] GeneratePeriodReportPdf(ReportViewModel report)
        {
            var periodLabel = report.StartDate.Date == report.EndDate.Date
                ? $"du {report.StartDate:dd/MM/yyyy}"
                : $"du {report.StartDate:dd/MM/yyyy} au {report.EndDate:dd/MM/yyyy}";

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.5f, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header().Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(left =>
                            {
                                left.Item().Text(report.StoreName).FontSize(18).Bold().FontColor(Colors.Orange.Darken2);
                                left.Item().Text($"Rapport financier {periodLabel}").FontSize(11).FontColor(Colors.Grey.Darken2);
                            });

                            row.ConstantItem(160).AlignRight().Column(right =>
                            {
                                right.Item().Text("RAPPORT D'ACTIVITÉ").Bold();
                                right.Item().Text($"Édité le {DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(9).FontColor(Colors.Grey.Darken1);
                            });
                        });

                        col.Item().PaddingTop(8).BorderBottom(2).BorderColor(Colors.Orange.Darken2);
                    });

                    page.Content().PaddingVertical(12).Column(col =>
                    {
                        // Résumé KPI
                        col.Item().PaddingBottom(14).Row(row =>
                        {
                            row.RelativeItem().Background(Colors.Grey.Lighten4).Padding(8).Column(c =>
                            {
                                c.Item().Text("Chiffre d'affaires").FontSize(9).FontColor(Colors.Grey.Darken2);
                                c.Item().Text($"{report.TotalSales:N2} $").FontSize(13).Bold().FontColor(Colors.Green.Darken2);
                            });
                            row.ConstantItem(8);
                            row.RelativeItem().Background(Colors.Grey.Lighten4).Padding(8).Column(c =>
                            {
                                c.Item().Text("Marge brute").FontSize(9).FontColor(Colors.Grey.Darken2);
                                c.Item().Text($"{report.GrossProfit:N2} $").FontSize(13).Bold();
                            });
                            row.ConstantItem(8);
                            row.RelativeItem().Background(Colors.Grey.Lighten4).Padding(8).Column(c =>
                            {
                                c.Item().Text("Dépenses").FontSize(9).FontColor(Colors.Grey.Darken2);
                                c.Item().Text($"{report.TotalExpenses:N2} $").FontSize(13).Bold().FontColor(Colors.Red.Darken2);
                            });
                            row.ConstantItem(8);
                            row.RelativeItem().Background(Colors.Grey.Lighten4).Padding(8).Column(c =>
                            {
                                c.Item().Text("Bénéfice net").FontSize(9).FontColor(Colors.Grey.Darken2);
                                c.Item().Text($"{report.NetProfit:N2} $").FontSize(13).Bold()
                                    .FontColor(report.NetProfit >= 0 ? Colors.Green.Darken2 : Colors.Red.Darken2);
                            });
                        });

                        // Tableau des ventes
                        col.Item().PaddingBottom(6).Text($"Ventes enregistrées ({report.Sales.Count})").FontSize(12).Bold();
                        if (report.Sales.Any())
                        {
                            col.Item().PaddingBottom(16).Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(85);
                                    columns.RelativeColumn(3);
                                    columns.ConstantColumn(90);
                                    columns.ConstantColumn(80);
                                });

                                table.Header(h =>
                                {
                                    h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Date/Heure").SemiBold();
                                    h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Articles").SemiBold();
                                    h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Paiement").SemiBold();
                                    h.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text("Total").SemiBold();
                                });

                                foreach (var sale in report.Sales)
                                {
                                    var articles = sale.Items.Any()
                                        ? string.Join(", ", sale.Items.Select(i => $"{i.ProductName} (x{i.Quantity})"))
                                        : "Aucun article";
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text($"{sale.SaleDate:dd/MM HH:mm}");
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(articles);
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(sale.PaymentMethod ?? "Cash");
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignRight().Text($"{sale.FinalPrice:N2} $").SemiBold();
                                }
                            });
                        }
                        else
                        {
                            col.Item().PaddingBottom(16).Text("Aucune vente sur cette période.").FontColor(Colors.Grey.Darken1);
                        }

                        // Tableau des dépenses
                        col.Item().PaddingBottom(6).Text($"Dépenses enregistrées ({report.Expenses.Count})").FontSize(12).Bold();
                        if (report.Expenses.Any())
                        {
                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(85);
                                    columns.ConstantColumn(100);
                                    columns.RelativeColumn(3);
                                    columns.ConstantColumn(80);
                                });

                                table.Header(h =>
                                {
                                    h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Date").SemiBold();
                                    h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Catégorie").SemiBold();
                                    h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Description").SemiBold();
                                    h.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text("Montant").SemiBold();
                                });

                                foreach (var exp in report.Expenses)
                                {
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text($"{exp.Date:dd/MM/yyyy}");
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(exp.Category);
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(exp.Description);
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignRight().Text($"{exp.Amount:N2} $").FontColor(Colors.Red.Darken2);
                                }
                            });
                        }
                        else
                        {
                            col.Item().Text("Aucune dépense sur cette période.").FontColor(Colors.Grey.Darken1);
                        }
                    });

                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.Span("AdGestionHub — Adhil Digital | Page ").FontSize(8).FontColor(Colors.Grey.Darken1);
                        x.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Darken1);
                        x.Span(" / ").FontSize(8).FontColor(Colors.Grey.Darken1);
                        x.TotalPages().FontSize(8).FontColor(Colors.Grey.Darken1);
                    });
                });
            });

            return document.GeneratePdf();
        }
    }
}
