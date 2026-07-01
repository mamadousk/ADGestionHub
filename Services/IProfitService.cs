// ==================== FILE: Services/IProfitService.cs ====================
using System;
using System.Threading.Tasks;

namespace AdGestionHub.Services
{
    public interface IProfitService
    {
        Task<decimal> CalculateProfitAsync(int boutiqueId, DateTime? startDate = null, DateTime? endDate = null);
        Task<decimal> CalculateGrossProfitAsync(int boutiqueId, DateTime? startDate = null, DateTime? endDate = null);
    }
}