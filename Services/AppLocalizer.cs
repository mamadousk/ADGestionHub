using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.AspNetCore.Http;

namespace AdGestionHub.Services
{
    public interface IAppLocalizer
    {
        string CurrentLanguage { get; }
        bool IsEnglish { get; }
        string this[string key] { get; }
        string T(string fr, string en);
    }

    public sealed class AppLocalizer : IAppLocalizer
    {
        public const string LanguageCookieName = "AdGestionHub.Lang";

        private readonly IHttpContextAccessor _httpContextAccessor;

        public AppLocalizer(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public string CurrentLanguage
        {
            get
            {
                var ctx = _httpContextAccessor.HttpContext;
                if (ctx != null && ctx.Items.TryGetValue("ResolvedLanguage", out var item) && item is string s && !string.IsNullOrEmpty(s))
                    return s;

                if (ctx != null && ctx.Request.Cookies.TryGetValue(LanguageCookieName, out var cookieLang))
                {
                    if (string.Equals(cookieLang, "en", StringComparison.OrdinalIgnoreCase))
                        return "en";
                    if (string.Equals(cookieLang, "fr", StringComparison.OrdinalIgnoreCase))
                        return "fr";
                }

                var uiTwo = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
                return string.Equals(uiTwo, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "fr";
            }
        }

        public bool IsEnglish => string.Equals(CurrentLanguage, "en", StringComparison.OrdinalIgnoreCase);

        public string T(string fr, string en) => IsEnglish ? en : fr;

        public string this[string key]
        {
            get
            {
                if (Translations.TryGetValue(key, out var pair))
                    return IsEnglish ? pair.En : pair.Fr;
                return key;
            }
        }

        private static readonly Dictionary<string, (string Fr, string En)> Translations = new(StringComparer.OrdinalIgnoreCase)
        {
            // Navigation & Shell
            ["Nav.Dashboard"] = ("Tableau de bord", "Dashboard"),
            ["Nav.NewSale"] = ("Encaisser", "Checkout"),
            ["Nav.Sales"] = ("Ventes", "Sales"),
            ["Nav.Quotes"] = ("Devis & Proformas", "Quotes & Proformas"),
            ["Nav.Products"] = ("Catalogue & Stocks", "Catalog & Stock"),
            ["Nav.Clients"] = ("Clients", "Customers"),
            ["Nav.Expenses"] = ("Dépenses", "Expenses"),
            ["Nav.Debts"] = ("Créances clients", "Receivables"),
            ["Nav.Reports"] = ("Rapports", "Reports"),
            ["Nav.Staff"] = ("Équipe", "Team"),
            ["Nav.Users"] = ("Équipe & Utilisateurs", "Team & Users"),
            ["Nav.ErrorLogs"] = ("Journal d'incidents", "System Incident Logs"),
            ["Nav.SystemNotices"] = ("Historique des annonces", "System Notices History"),
            ["Nav.Settings"] = ("Paramètres", "Settings"),
            ["Nav.SystemConsole"] = ("Console système", "System Console"),
            ["Nav.Profile"] = ("Mon profil", "My Profile"),
            ["Nav.MyStore"] = ("Paramètres boutique", "Store Settings"),
            ["Nav.Logout"] = ("Déconnexion", "Sign out"),
            ["Nav.Login"] = ("Connexion", "Sign in"),
            ["Nav.Register"] = ("Créer un compte", "Create account"),
            ["Nav.Feedback"] = ("Donner un avis", "Send feedback"),

            // Documents & Fiscalité
            ["Doc.SalesReceipt"] = ("Reçu de vente", "Sales Receipt"),
            ["Doc.Invoice"] = ("Facture", "Invoice"),
            ["Doc.Quote"] = ("Devis / Proforma", "Quote / Proforma"),
            ["Doc.ActivityReport"] = ("Rapport financier", "Financial Report"),
            ["Doc.UnitPrice"] = ("Prix unitaire", "Unit Price"),
            ["Doc.Qty"] = ("Qté", "Qty"),
            ["Doc.Description"] = ("Désignation", "Description"),
            ["Doc.NetTotal"] = ("Total Net", "Net Total"),
            ["Doc.TotalDue"] = ("Total à payer", "Total Due"),
            ["Doc.TotalPaid"] = ("Total encaissé", "Total Paid"),
            ["Doc.SubtotalExclTax"] = ("Total HT", "Subtotal Excl. Tax"),
            ["Doc.VatAmount"] = ("Montant TVA", "VAT Amount"),
            ["Doc.TotalInclTax"] = ("Total TTC", "Total Incl. Tax"),
            ["Doc.VatNotApplicable"] = ("TVA non applicable", "VAT not applicable"),
            ["Doc.WalkInCustomer"] = ("Client comptoir", "Walk-in customer"),
            ["Doc.PaymentMethod"] = ("Mode de règlement", "Payment method"),
            ["Doc.Date"] = ("Date", "Date"),
            ["Doc.Number"] = ("Référence", "Reference"),
            ["Doc.ValidUntil"] = ("Valable jusqu'au", "Valid until"),
            ["Doc.SecondaryEquivalent"] = ("Contre-valeur indicative", "Indicative equivalent"),
            ["Doc.PaidIn"] = ("Réglé en", "Tendered in"),
            ["Doc.Cancelled"] = ("Annulé", "Voided"),

            // Statuts entreprise
            ["Status.Informal"] = ("Non formalisé", "Informal"),
            ["Status.Formalized"] = ("Formalisé", "Formalized"),
            ["Status.VatDisabled"] = ("TVA désactivée", "VAT disabled"),
            ["Status.VatEnabled"] = ("TVA activée", "VAT enabled"),
            ["Status.EInvoicingTitle"] = ("Facturation Électronique Certifiée", "Certified Electronic Invoicing"),
            ["Status.IntegrationInProgress"] = ("En cours d'intégration", "Integration in progress"),
            ["Status.EInvoicingBody"] = (
                "La connexion directe avec l'administration fiscale de votre pays est en cours de déploiement. Vos documents respectent déjà la numérotation séquentielle requise.",
                "Direct connection with your country's tax authority is currently being rolled out. Your documents already comply with required sequential numbering."),
            ["Status.NotifyMeCountry"] = ("M'informer dès la disponibilité dans mon pays", "Notify me when available in my country"),
            ["Status.NotifyRegistered"] = ("Demande enregistrée pour votre pays", "Notification request registered for your country"),

            // Actions communes
            ["Action.Save"] = ("Enregistrer", "Save changes"),
            ["Action.Create"] = ("Créer", "Create"),
            ["Action.Add"] = ("Ajouter", "Add"),
            ["Action.Edit"] = ("Modifier", "Edit"),
            ["Action.Cancel"] = ("Annuler", "Cancel"),
            ["Action.Back"] = ("Retour", "Back"),
            ["Action.Delete"] = ("Supprimer", "Delete"),
            ["Action.Print"] = ("Imprimer", "Print"),
            ["Action.DownloadPdf"] = ("Télécharger PDF", "Download PDF"),
            ["Action.Filter"] = ("Filtrer", "Filter"),
            ["Action.Search"] = ("Rechercher", "Search"),
            ["Action.Reset"] = ("Réinitialiser", "Reset"),
            ["Action.Details"] = ("Détails", "Details"),
            ["Action.Charge"] = ("Encaisser", "Complete sale"),
            ["Action.Collect"] = ("Encaisser", "Collect"),
            ["Action.ConvertToInvoice"] = ("Convertir en facture", "Convert to invoice"),
            ["Dashboard.NetProfit"] = ("Bénéfice Net", "Net Profit"),
            ["Auth.ForgotPassword"] = ("Mot de passe oublié", "Forgot password"),
            ["Auth.Email"] = ("Adresse email", "Email address")
        };
    }
}
