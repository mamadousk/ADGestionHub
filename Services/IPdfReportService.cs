using AdGestionHub.Models;

namespace AdGestionHub.Services
{
    public interface IPdfReportService
    {
        byte[] GenerateSaleReceiptPdf(Sale sale, StoreSettings? settings, int invoiceSequenceNumber, string language = "fr");
        byte[] GenerateQuotePdf(Quote quote, StoreSettings? settings, string language = "fr");
        byte[] GeneratePeriodReportPdf(ReportViewModel report, string language = "fr");
    }
}
