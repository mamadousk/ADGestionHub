using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AdGestionHub.Data;
using AdGestionHub.Models;

namespace AdGestionHub.Controllers
{
    [Authorize(Roles = "SuperAdmin")]
    public class SuperAdminController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SuperAdminController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var boutiques = await _context.Boutiques.ToListAsync();

            ViewBag.BoutiqueCount = boutiques.Count;
            ViewBag.GlobalSales = await _context.Sales.SumAsync(s => s.FinalPrice);
            ViewBag.TotalProducts = await _context.Products.CountAsync();

            ViewBag.Notifications = await _context.GlobalNotifications
                .Where(n => n.ExpiryDate > DateTime.Now)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

            ViewBag.Feedbacks = await _context.UserFeedbacks
                .OrderByDescending(f => f.DateEnvoi)
                .Take(5)
                .ToListAsync();

            return View(boutiques);
        }

        [HttpPost]
        public async Task<IActionResult> SendNotification(string message, bool isCritical, int hoursActive)
        {
            if (string.IsNullOrEmpty(message)) return RedirectToAction("Index");

            var notification = new GlobalNotification
            {
                Message = message,
                IsCritical = isCritical,
                CreatedAt = DateTime.Now,
                ExpiryDate = DateTime.Now.AddHours(hoursActive > 0 ? hoursActive : 2)
            };

            _context.GlobalNotifications.Add(notification);
            await _context.SaveChangesAsync();
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteNotification(int id)
        {
            var notif = await _context.GlobalNotifications.FindAsync(id);
            if (notif != null)
            {
                _context.GlobalNotifications.Remove(notif);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("Index");
        }

        public async Task<IActionResult> Logs()
        {
            var logs = await _context.ErrorLogs
                .OrderByDescending(l => l.CreatedAt)
                .Take(100)
                .ToListAsync();
            return View(logs);
        }

        public async Task<IActionResult> ManageNotifications()
        {
            var notifications = await _context.GlobalNotifications
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();
            return View(notifications);
        }

        public async Task<IActionResult> BoutiqueDetails(int id)
        {
            var boutique = await _context.Boutiques
                .FirstOrDefaultAsync(b => b.Id == id);

            if (boutique == null) return NotFound();

            // CORRECTION ICI : Utilisation de boutique.Id au lieu de BoutiqueId
            var settings = await _context.StoreSettings
                .FirstOrDefaultAsync(s => s.BoutiqueId == boutique.Id);

            ViewBag.Settings = settings;

            // CORRECTION ICI : Utilisation de boutique.Id pour filtrer les produits et ventes
            ViewBag.ProductCount = await _context.Products.CountAsync(p => p.BoutiqueId == boutique.Id);
            ViewBag.SalesCount = await _context.Sales.CountAsync(s => s.BoutiqueId == boutique.Id);

            return View(boutique);
        }
    }
}