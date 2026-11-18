using AdGestionHub.Models;

namespace AdGestionHub.Services;

public interface IPdfReportService
{
    byte[] SalePdf(Sale sale, StoreSettings settings);
    byte[] QuotePdf(Quote quote, StoreSettings settings);
    byte[] ReportPdf(BusinessReport report);
}
