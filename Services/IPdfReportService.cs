using AdGestionHub.Models;

namespace AdGestionHub.Services
{
    public interface IPdfReportService
    {
        byte[] GenerateSaleReceiptPdf(Sale sale, StoreSettings? settings, int invoiceSequenceNumber);
        byte[] GeneratePeriodReportPdf(ReportViewModel report);
    }
}
