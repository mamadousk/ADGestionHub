// ==================== FILE: Services/IStockService.cs ====================
using System.Threading.Tasks;

namespace AdGestionHub.Services
{
    public interface IStockService
    {
        Task<bool> CheckStockAvailabilityAsync(int productId, int quantityRequested);
        Task<bool> DeductStockAsync(int productId, int quantity);

        /// <summary>
        /// Décrémente le stock de façon atomique (une seule requête SQL conditionnelle) et seulement
        /// si la boutique est la bonne et que le stock suffit. Retourne false si rien n'a été modifié.
        /// À appeler dans la transaction de la vente.
        /// </summary>
        Task<bool> TryDeductStockAsync(int productId, int boutiqueId, int quantity);
        Task<bool> RestoreStockAsync(int productId, int quantity);
        Task<int> GetLowStockCountAsync(int boutiqueId);
    }
}