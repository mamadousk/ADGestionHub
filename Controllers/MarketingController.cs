using Microsoft.AspNetCore.Mvc;
using AdGestionHub.Models; // Assure-toi que c'est le bon namespace
using AdGestionHub.Data;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore; // Nécessaire pour ToListAsync()

namespace AdGestionHub.Controllers
{
    public class MarketingController : Controller
    {
        private readonly ApplicationDbContext _context;

        public MarketingController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Action pour la Liste d'Attente Pro
        [HttpPost]
        public async Task<IActionResult> JoinWaitingList(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return BadRequest();

            var entry = new WaitingListPro { Email = email };
            _context.WaitingListPros.Add(entry);
            await _context.SaveChangesAsync();

            return Ok();
        }

        // Action pour le Feedback Bêta
        [HttpPost]
        public async Task<IActionResult> SubmitFeedback(int note, string commentaire)
        {
            // 1. On récupère le nom de l'utilisateur connecté
            var userName = User.Identity?.Name ?? "Anonyme";

            // 2. On crée l'objet feedback avec les bons noms
            var feedback = new UserFeedback
            {
                UserName = userName, // Utilise la variable qu'on vient de créer
                Note = note,
                Commentaire = commentaire,
                DateEnvoi = DateTime.Now
            };

            _context.UserFeedbacks.Add(feedback);
            await _context.SaveChangesAsync();

            return Ok();
        }

        // Ajoute ceci dans ton MarketingController.cs
        public async Task<IActionResult> AdminDashboard()
        {
            // Sécurité : On peut filtrer par ton email spécifique pour être sûr que seul toi y accède
            // if (User.Identity.Name != "ton-email@gmail.com") return Forbid();

            var feedbacks = await _context.UserFeedbacks
                                          .OrderByDescending(f => f.DateEnvoi)
                                          .ToListAsync();

            var waitingList = await _context.WaitingListPros
                                            .OrderByDescending(w => w.DateInscription)
                                            .ToListAsync();

            // On passe les deux listes à la vue via un ViewModel ou un ViewBag
            ViewBag.WaitingList = waitingList;
            return View(feedbacks);
        }
        public async Task<IActionResult> ExportLeads()
        {
            var leads = await _context.WaitingListPros.OrderByDescending(w => w.DateInscription).ToListAsync();

            // Création du contenu CSV
            var builder = new System.Text.StringBuilder();
            builder.AppendLine("Email;Date d'inscription"); // Entêtes

            foreach (var lead in leads)
            {
                builder.AppendLine($"{lead.Email};{lead.DateInscription:dd/MM/yyyy HH:mm}");
            }

            // Envoi du fichier au navigateur
            var csvContent = System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(builder.ToString())).ToArray();
            return File(csvContent, "text/csv", "AdGestionHub_Leads_Pro.csv");
        }
    }
}