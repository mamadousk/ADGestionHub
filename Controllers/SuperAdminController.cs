using AdGestionHub.Data;
using AdGestionHub.Models;
using AdGestionHub.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AdGestionHub.Controllers
{
    [Authorize(Roles = "SuperAdmin")]
    public class SuperAdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly BusinessReportService _reports;

        public SuperAdminController(ApplicationDbContext context, BusinessReportService reports)
        {
            _context = context;
            _reports = reports;
        }

        public async Task<IActionResult> Index()
        {
            var boutiques = await _context.Boutiques
                .AsNoTracking()
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();

            ViewBag.BoutiqueCount = boutiques.Count;
            ViewBag.ActiveBoutiqueCount = boutiques.Count(b => b.IsActive);
            ViewBag.SaleCount = await _context.Sales.AsNoTracking().CountAsync(s => !s.IsCancelled);
            ViewBag.TotalProducts = await _context.Products.AsNoTracking().CountAsync();
            ViewBag.TotalUsers = await _context.Users.AsNoTracking().CountAsync();
            ViewBag.WaitingListCount = await _context.WaitingListPros.AsNoTracking().CountAsync();

            ViewBag.Notifications = await _context.GlobalNotifications
                .AsNoTracking()
                .Where(n => n.ExpiryDate > DateTime.Now && n.IsActive)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

            ViewBag.Feedbacks = await _context.UserFeedbacks
                .AsNoTracking()
                .OrderByDescending(f => f.DateEnvoi)
                .Take(5)
                .ToListAsync();

            return View(boutiques);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleBoutiqueStatus(int id)
        {
            var boutique = await _context.Boutiques.FindAsync(id);
            if (boutique == null) return NotFound();

            boutique.IsActive = !boutique.IsActive;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = boutique.IsActive
                ? $"La boutique « {boutique.Nom} » a été réactivée."
                : $"La boutique « {boutique.Nom} » a été suspendue.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendNotification(string message, bool isCritical, int hoursActive)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                TempData["ErrorMessage"] = "Le message de notification ne peut pas être vide.";
                return RedirectToAction(nameof(Index));
            }

            var safeHours = Math.Clamp(hoursActive > 0 ? hoursActive : 2, 1, 720);
            var notification = new GlobalNotification
            {
                Message = message.Trim(),
                IsCritical = isCritical,
                IsActive = true,
                CreatedAt = DateTime.Now,
                ExpiryDate = DateTime.Now.AddHours(safeHours)
            };

            _context.GlobalNotifications.Add(notification);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Annonce globale diffusée avec succès.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteNotification(int id)
        {
            var notif = await _context.GlobalNotifications.FindAsync(id);
            if (notif != null)
            {
                _context.GlobalNotifications.Remove(notif);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Notification supprimée.";
            }
            return RedirectToAction(nameof(ManageNotifications));
        }

        public async Task<IActionResult> Logs()
        {
            var logs = await _context.ErrorLogs
                .AsNoTracking()
                .OrderByDescending(l => l.CreatedAt)
                .Take(100)
                .ToListAsync();
            return View(logs);
        }

        public async Task<IActionResult> ManageNotifications()
        {
            var notifications = await _context.GlobalNotifications
                .AsNoTracking()
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();
            return View(notifications);
        }

        public async Task<IActionResult> BoutiqueDetails(int id)
        {
            var boutique = await _context.Boutiques
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == id);

            if (boutique == null) return NotFound();

            var settings = await _context.StoreSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.BoutiqueId == boutique.Id);

            var salesCount = await _context.Sales.AsNoTracking().CountAsync(s => s.BoutiqueId == boutique.Id && !s.IsCancelled);
            var productCount = await _context.Products.AsNoTracking().CountAsync(p => p.BoutiqueId == boutique.Id);
            var employeeCount = await _context.Users.AsNoTracking().CountAsync(u => u.BoutiqueId == boutique.Id);
            var debtCount = (await _reports.OutstandingAsync(boutique.Id, DateTime.Today.AddDays(1))).Count(b => b.Kind != "Purchase");

            ViewBag.Settings = settings;
            ViewBag.ProductCount = productCount;
            ViewBag.SaleCount = salesCount;
            ViewBag.EmployeeCount = employeeCount;
            ViewBag.DebtCount = debtCount;

            return View(boutique);
        }
    }
}
