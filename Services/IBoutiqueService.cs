using AdGestionHub.Models;
using System.Threading.Tasks;

namespace AdGestionHub.Services
{
    public interface IBoutiqueService
    {
        Task<Boutique> GetCurrentBoutiqueAsync();
        Task<int?> GetCurrentBoutiqueIdAsync();
    }
}