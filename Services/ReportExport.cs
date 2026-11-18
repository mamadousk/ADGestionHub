using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using AdGestionHub.Models;

namespace AdGestionHub.Services;

public static class ReportExport
{
    public static IReadOnlyList<string> Columns { get; } =
        ["Rubrique", "Date", "Référence", "Libellé", "Devise", "Montant", "Réglé", "Solde", "Quantité"];

    public static IEnumerable<object?[]> Rows(BusinessReport report)
    {
        var c = report.Currency;
        yield return ["Période", IsoDate(report.Period.Start), "", $"{report.Settings.StoreName} · jusqu'au {report.Period.End:dd/MM/yyyy}", c, null, null, null, null];
        yield return ["Synthèse", null, null, "Chiffre d'affaires", c, report.Revenue, null, null, report.Sales.Count];
        yield return ["Synthèse", null, null, "Total des ventes", c, report.Invoiced, null, null, null];
        yield return ["Synthèse", null, null, "Total des achats", c, report.Purchased, null, null, report.Purchases.Count];
        yield return ["Synthèse", null, null, "Dépenses", c, report.Expensed, null, null, null];
        yield return ["Synthèse", null, null, "Recettes encaissées", c, report.Received, null, null, null];
        yield return ["Synthèse", null, null, "Règlements sur ventes (inclus dans les recettes)", c, report.SalesReceipts, null, null, null];
        yield return ["Synthèse", null, null, "Règlements fournisseurs", c, report.SupplierPayments, null, null, null];
        yield return ["Synthèse", null, null, "Créances à la fin de la période", c, null, null, report.Receivables, null];
        yield return ["Synthèse", null, null, "Dettes à la fin de la période", c, null, null, report.Payables, null];
        yield return ["Synthèse", null, null, "Stock actuel au coût catalogue", report.Settings.PrimaryCurrencyCode, report.StockValue, null, null, report.StockUnits];

        foreach (var s in report.Sales)
            yield return ["Ventes", IsoDate(s.SaleDate), s.Number, s.CustomerName ?? "Client de passage", c, s.FinalPrice, null, null, null];
        foreach (var p in report.Purchases)
            yield return ["Achats", IsoDate(p.RecordedAt), p.Number, p.SupplierName, c, p.Total, null, null, null];
        foreach (var product in report.TopProducts)
            yield return ["Produits vendus", null, null, product.Name, null, null, null, null, product.Units];
        foreach (var stock in report.Stock)
            yield return ["Stock actuel", null, stock.Sku, stock.IsLow ? $"{stock.Name} · sous le seuil" : stock.Name,
                report.Settings.PrimaryCurrencyCode, stock.Value, null, null, stock.Quantity];
        foreach (var outstanding in report.Outstanding)
            yield return [outstanding.Kind == "Purchase" ? "Dettes fournisseurs" : "Créances clients", IsoDate(outstanding.Date), outstanding.Number,
                outstanding.Contact, c, outstanding.Total, outstanding.Paid, outstanding.Balance, null];
        foreach (var payment in report.Payments)
        {
            var label = payment.PurchaseId != null ? "Paiement fournisseur" : payment.SaleId != null ? "Règlement client" : payment.DebtId != null ? "Créance manuelle" : "Recette diverse";
            yield return [payment.PurchaseId != null ? "Décaissements" : "Encaissements", IsoDate(payment.RecordedAt), $"RGL-{payment.Id}",
                $"{label} · {PaymentMeans.Display(payment.Method)}{(payment.Description == null ? "" : $" · {payment.Description}")}", c, payment.Amount, null, null, null];
        }
        foreach (var expense in report.Expenses)
            yield return ["Dépenses", IsoDate(expense.Date), $"DEP-{expense.Id}", $"{expense.Description} · {expense.Category} · {PaymentMeans.Display(expense.PaymentMethod)}",
                c, expense.Amount, null, null, null];
    }

    public static byte[] Csv(BusinessReport report)
    {
        var fr = CultureInfo.GetCultureInfo("fr-FR");
        var output = new StringBuilder("\uFEFF").AppendJoin(';', Columns).Append("\r\n");
        foreach (var row in Rows(report))
            output.AppendJoin(';', row.Select(cell => cell switch
            {
                null => "",
                IFormattable number => number.ToString(null, fr),
                _ => QuoteCsv(cell.ToString() ?? "")
            })).Append("\r\n");
        return Encoding.UTF8.GetBytes(output.ToString());
    }

    private static string QuoteCsv(string text)
    {
        // Text from a catalog or contact must remain text when opened in a spreadsheet.
        var start = text.TrimStart().TrimStart('\ufeff').TrimStart();
        if (start.Length > 0 && start[0] is '=' or '+' or '-' or '@') text = "'" + text;
        return $"\"{text.Replace("\"", "\"\"")}\"";
    }

    public static byte[] Excel(BusinessReport report)
    {
        using var file = new MemoryStream();
        using (var zip = new ZipArchive(file, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddText(zip, "[Content_Types].xml", """
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                  <Default Extension="xml" ContentType="application/xml"/>
                  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
                  <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
                </Types>
                """);
            AddText(zip, "_rels/.rels", """
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
                </Relationships>
                """);
            AddText(zip, "xl/workbook.xml", """
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                  <sheets><sheet name="Rapport" sheetId="1" r:id="rId1"/></sheets>
                </workbook>
                """);
            AddText(zip, "xl/_rels/workbook.xml.rels", """
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
                </Relationships>
                """);
            using var stream = zip.CreateEntry("xl/worksheets/sheet1.xml").Open();
            using var xml = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false) });
            xml.WriteStartElement("worksheet", SpreadsheetNamespace);
            xml.WriteStartElement("cols", SpreadsheetNamespace);
            WriteColumn(xml, 1, 3, 22);
            WriteColumn(xml, 4, 4, 46);
            WriteColumn(xml, 5, 9, 16);
            xml.WriteEndElement();
            xml.WriteStartElement("sheetData", SpreadsheetNamespace);
            WriteRow(xml, Columns.Cast<object?>(), 1);
            var rowNumber = 2;
            foreach (var row in Rows(report)) WriteRow(xml, row, rowNumber++);
            xml.WriteEndElement();
            xml.WriteEndElement();
        }
        return file.ToArray();
    }

    private const string SpreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    private static void WriteColumn(XmlWriter xml, int min, int max, int width)
    {
        xml.WriteStartElement("col", SpreadsheetNamespace);
        xml.WriteAttributeString("min", XmlConvert.ToString(min));
        xml.WriteAttributeString("max", XmlConvert.ToString(max));
        xml.WriteAttributeString("width", XmlConvert.ToString(width));
        xml.WriteAttributeString("customWidth", "1");
        xml.WriteEndElement();
    }

    private static void WriteRow(XmlWriter xml, IEnumerable<object?> cells, int row)
    {
        xml.WriteStartElement("row", SpreadsheetNamespace);
        xml.WriteAttributeString("r", XmlConvert.ToString(row));
        var column = 'A';
        foreach (var cell in cells)
        {
            var reference = column++ + XmlConvert.ToString(row);
            if (cell == null) continue;
            xml.WriteStartElement("c", SpreadsheetNamespace);
            xml.WriteAttributeString("r", reference);
            if (cell is IFormattable number)
            {
                xml.WriteElementString("v", SpreadsheetNamespace, number.ToString(null, CultureInfo.InvariantCulture));
            }
            else
            {
                xml.WriteAttributeString("t", "inlineStr");
                xml.WriteStartElement("is", SpreadsheetNamespace);
                xml.WriteStartElement("t", SpreadsheetNamespace);
                xml.WriteAttributeString("xml", "space", null, "preserve");
                xml.WriteString(XmlText(cell.ToString() ?? ""));
                xml.WriteEndElement();
                xml.WriteEndElement();
            }
            xml.WriteEndElement();
        }
        xml.WriteEndElement();
    }

    private static string XmlText(string value)
    {
        var text = new StringBuilder(value.Length);
        foreach (var rune in value.EnumerateRunes())
            if (rune.Value is 9 or 10 or 13 or >= 32 and <= 0xd7ff or >= 0xe000 and <= 0xfffd or >= 0x10000)
                text.Append(rune.ToString());
        return text.ToString();
    }

    private static void AddText(ZipArchive zip, string path, string value)
    {
        using var file = new StreamWriter(zip.CreateEntry(path).Open(), new UTF8Encoding(false));
        file.Write(value);
    }

    private static string IsoDate(DateTime date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
