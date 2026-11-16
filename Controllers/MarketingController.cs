using AdGestionHub.Data;
using AdGestionHub.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdGestionHub.Controllers
{
    public class MarketingController : Controller
    {
        private readonly ApplicationDbContext _context;

        public MarketingController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ========== LISTE D'ATTENTE PRO (publique, appelée depuis la page d'accueil) ==========
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> JoinWaitingList(string? email)
        {
            email = email?.Trim();

            if (string.IsNullOrWhiteSpace(email)
                || email.Length > 254
                || !new EmailAddressAttribute().IsValid(email))
            {
                return BadRequest(new { error = "Veuillez fournir une adresse email valide." });
            }

            var normalized = email.ToLowerInvariant();

            var alreadyThere = await _context.WaitingListPros.AnyAsync(w => w.Email == normalized);
            if (!alreadyThere)
            {
                _context.WaitingListPros.Add(new WaitingListPro { Email = normalized });
                await _context.SaveChangesAsync();
            }

            return Ok(new { success = true });
        }

        // ========== FEEDBACK BÊTA (formulaire du layout, utilisateur connecté ou non) ==========
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitFeedback(int note, string? commentaire)
        {
            commentaire = commentaire?.Trim();

            if (note < 1 || note > 5
                || string.IsNullOrWhiteSpace(commentaire)
                || commentaire.Length > 2000)
            {
                if (IsAjaxRequest())
                    return BadRequest(new { error = "Veuillez saisir une note entre 1 et 5 et un commentaire." });

                TempData["ErrorMessage"] = "Veuillez saisir un commentaire valide (maximum 2000 caractères).";
                return RedirectToSafeReturnUrl();
            }

            var feedback = new UserFeedback
            {
                UserName = User.Identity?.Name ?? "Anonyme",
                Note = note,
                Commentaire = commentaire,
                DateEnvoi = DateTime.Now
            };

            _context.UserFeedbacks.Add(feedback);
            await _context.SaveChangesAsync();

            if (IsAjaxRequest())
                return Ok(new { success = true, message = "Merci beaucoup pour votre avis !" });

            TempData["SuccessMessage"] = "Merci beaucoup ! Votre avis a bien été transmis à l'équipe AdGestionHub.";
            return RedirectToSafeReturnUrl();
        }

        // ========== TABLEAU DE BORD MARKETING (SuperAdmin uniquement) ==========
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> AdminDashboard()
        {
            var feedbacks = await _context.UserFeedbacks
                .AsNoTracking()
                .OrderByDescending(f => f.DateEnvoi)
                .ToListAsync();

            var waitingList = await _context.WaitingListPros
                .AsNoTracking()
                .OrderByDescending(w => w.DateInscription)
                .ToListAsync();

            ViewBag.WaitingList = waitingList;
            return View(feedbacks);
        }

        // ========== EXPORT CSV DES CONTACTS (SuperAdmin uniquement) ==========
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> ExportLeads()
        {
            var leads = await _context.WaitingListPros
                .AsNoTracking()
                .OrderByDescending(w => w.DateInscription)
                .ToListAsync();

            var builder = new StringBuilder();
            builder.AppendLine("Email;Date d'inscription");

            foreach (var lead in leads)
            {
                builder.AppendLine($"{CsvSafe(lead.Email)};{lead.DateInscription:dd/MM/yyyy HH:mm}");
            }

            var csvContent = Encoding.UTF8.GetPreamble()
                .Concat(Encoding.UTF8.GetBytes(builder.ToString()))
                .ToArray();

            return File(csvContent, "text/csv", "AdGestionHub_Leads_Pro.csv");
        }

        private bool IsAjaxRequest() =>
            string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

        private IActionResult RedirectToSafeReturnUrl()
        {
            var referer = Request.Headers.Referer.ToString();
            if (Uri.TryCreate(referer, UriKind.Absolute, out var uri)
                && string.Equals(uri.Host, Request.Host.Host, StringComparison.OrdinalIgnoreCase)
                && Url.IsLocalUrl(uri.PathAndQuery))
            {
                return LocalRedirect(uri.PathAndQuery);
            }

            return User.Identity?.IsAuthenticated == true
                ? RedirectToAction("Index", "Home")
                : RedirectToAction("Welcome", "Home");
        }

        private static string CsvSafe(string? value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            var v = value.Replace("\r", " ").Replace("\n", " ");

            if ("=+-@\t".IndexOf(v[0]) >= 0)
                v = "'" + v;

            if (v.Contains(';') || v.Contains('"'))
                v = "\"" + v.Replace("\"", "\"\"") + "\"";

            return v;
        }
    }
}
