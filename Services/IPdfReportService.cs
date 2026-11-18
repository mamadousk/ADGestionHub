using AdGestionHub.Models;

namespace AdGestionHub.Services;

public interface IPdfReportService
{
    byte[] SalePdf(Sale sale, StoreSettings settings, byte[]? logo = null);
    byte[] ReportPdf(BusinessReport report);
}
