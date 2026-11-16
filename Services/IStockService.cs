using System.Threading.Tasks;

namespace AdGestionHub.Services
{
    public interface IStockService
    {
        Task<bool> CheckStockAvailabilityAsync(int productId, int quantityRequested);
        Task<bool> DeductStockAsync(int productId, int quantity);
        Task<bool> TryDeductStockAsync(int productId, int boutiqueId, int quantity);
        Task<bool> RestoreStockAsync(int productId, int quantity);
        Task<bool> RestoreStockAsync(int productId, int boutiqueId, int quantity);
        Task<int> GetLowStockCountAsync(int boutiqueId);
    }
}
